using JXServer.Domain.Accounts;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Encodings;
using JXServer.Primitives.ErrorCodes;
using JXServer.Primitives.Items;

namespace JXServer.Domain.Characters;

/// <summary>
/// Domain service enforcing character creation rules (`&lt; 3` per account, `6..30` TCVN3 bytes, `goddess_rolename.flt`,
/// global uniqueness, and starter village resolution with fallback to Map `53` per `FR-005`).
/// Does not call repository save operations directly (`Constitution VI`).
/// </summary>
public sealed class CharacterManager
{
    private readonly ICharacterRepository _characterRepository;
    private readonly IPlayerTemplateCatalog _playerTemplateCatalog;
    private readonly IMapCatalog _mapCatalog;
    private readonly TimeProvider _timeProvider;

    public CharacterManager(
        ICharacterRepository characterRepository,
        IPlayerTemplateCatalog playerTemplateCatalog,
        IMapCatalog mapCatalog,
        TimeProvider timeProvider)
    {
        _characterRepository = characterRepository;
        _playerTemplateCatalog = playerTemplateCatalog;
        _mapCatalog = mapCatalog;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Validates business invariants and constructs a new <see cref="Character"/> initialized from <see cref="IPlayerTemplateCatalog"/> (`FR-005`).
    /// </summary>
    public async Task<Character> CreateAsync(
        Account account,
        string roleNameUnicode,
        CharacterGender gender,
        ElementSeries series,
        ushort requestedStarterVillageMapId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        string trimmedName = (roleNameUnicode ?? string.Empty).TrimEnd('\0').Trim();
        byte[] tcvnBytes = Tcvn3Encoding.EncodeBytes(trimmedName);
        if (tcvnBytes.Length < CharacterConsts.MinNameByteLength ||
            tcvnBytes.Length > CharacterConsts.MaxNameByteLength)
        {
            throw new BusinessException(
                JXServerErrorCodes.CharacterNameProhibited,
                $"Character name TCVN3 byte length must be between {CharacterConsts.MinNameByteLength} and {CharacterConsts.MaxNameByteLength}.");
        }

        if (_playerTemplateCatalog.IsRoleNameProhibited(trimmedName))
        {
            throw new BusinessException(
                JXServerErrorCodes.CharacterNameProhibited,
                "Character name violates prohibited name filter rules (goddess_rolename.flt).");
        }

        if (!_playerTemplateCatalog.TryGetCharacterTemplate(series, gender, out CharacterBaseTemplate? baseTemplate) ||
            baseTemplate is null)
        {
            throw new BusinessException(
                JXServerErrorCodes.CharacterNameProhibited,
                $"Invalid character Series={series} or Gender={gender}.");
        }

        // Verify requested village is a valid starter village in nativeplacelist.ini
        if (!_playerTemplateCatalog.TryGetStarterVillage(requestedStarterVillageMapId, out StarterVillageTemplate? requestedVillage) ||
            requestedVillage is null)
        {
            throw new BusinessException(
                JXServerErrorCodes.CharacterInvalidStarterVillage,
                $"Starter village MapId={requestedStarterVillageMapId} is not a valid starter village.");
        }

        // Verify village map is loaded in worldset.ini (or redirect to default fallback village Map 53 per FR-005)
        if (!_mapCatalog.TryResolveActiveOrFallbackMapId(requestedStarterVillageMapId, out ushort activeVillageMapId, out _))
        {
            throw new BusinessException(
                JXServerErrorCodes.CharacterInvalidStarterVillage,
                $"Starter village MapId={requestedStarterVillageMapId} is not loaded in worldset.ini.");
        }

        StarterVillageTemplate effectiveVillage = requestedVillage;
        if (activeVillageMapId != requestedStarterVillageMapId &&
            _playerTemplateCatalog.TryGetStarterVillage(activeVillageMapId, out StarterVillageTemplate? fallbackVillage) &&
            fallbackVillage is not null)
        {
            effectiveVillage = fallbackVillage;
        }

        List<Character> existingCharacters = await _characterRepository.GetListByAccountIdAsync(account.Id, cancellationToken);
        if (existingCharacters.Count >= CharacterConsts.MaxRolesPerAccount)
        {
            throw new BusinessException(
                JXServerErrorCodes.CharacterLimitReached,
                $"Account already has the maximum of {CharacterConsts.MaxRolesPerAccount} characters.");
        }

        string normalizedRoleName = Character.NormalizeRoleName(trimmedName);
        Character? duplicate = await _characterRepository.FindByNormalizedRoleNameAsync(normalizedRoleName, cancellationToken);
        if (duplicate is not null)
        {
            throw new BusinessException(
                JXServerErrorCodes.CharacterNameDuplicate,
                $"Character name '{trimmedName}' is already taken.");
        }

        byte freeSlotIndex = FindFirstFreeSlotIndex(existingCharacters);
        Guid characterId = Guid.NewGuid();
        IEnumerable<CharacterSkillValue> starterSkills = baseTemplate.StarterSkills
            .Select(s => new CharacterSkillValue(s.SkillId, s.Level, 0, 0));

        var starterItems = new List<CharacterItem>();
        IReadOnlyList<StarterItemTemplate> starterItemTemplates = _playerTemplateCatalog.DefaultStarterItems;
        for (int i = 0; i < starterItemTemplates.Count; i++)
        {
            StarterItemTemplate st = starterItemTemplates[i];
            starterItems.Add(new CharacterItem(
                id: Guid.NewGuid(),
                characterId: characterId,
                genre: st.Genre,
                detailType: st.DetailType,
                particularType: st.ParticularType,
                level: Math.Max((byte)1, st.Level),
                series: ElementSeries.Metal,
                quality: ItemQualityKind.Normal,
                randomSeed: (uint)(1001 + i),
                generatorVersion: ItemConsts.GeneratorVersion,
                luck: 0,
                currentDurability: 1,
                maxDurability: 1,
                stackCount: Math.Max((ushort)1, st.StackCount),
                containerPlace: st.ContainerPlace,
                gridX: st.GridX,
                gridY: st.GridY));
        }

        return new Character(
            characterId,
            account.Id,
            freeSlotIndex,
            trimmedName,
            gender,
            series,
            effectiveVillage.MapId,
            effectiveVillage.MapId,
            effectiveVillage.RevivalId,
            effectiveVillage.SpawnMpsX,
            effectiveVillage.SpawnMpsY,
            baseTemplate.BaseStrength,
            baseTemplate.BaseDexterity,
            baseTemplate.BaseVitality,
            baseTemplate.BaseEnergy,
            baseTemplate.BaseLucky,
            baseTemplate.InitialLifeMax,
            baseTemplate.InitialManaMax,
            baseTemplate.InitialStaminaMax,
            starterSkills,
            _timeProvider.GetUtcNow(),
            starterItems);
    }

    private static byte FindFirstFreeSlotIndex(IReadOnlyList<Character> existing)
    {
        for (byte slot = 0; slot < CharacterConsts.MaxRolesPerAccount; slot++)
        {
            bool used = false;
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i].SlotIndex == slot)
                {
                    used = true;
                    break;
                }
            }

            if (!used)
            {
                return slot;
            }
        }

        return 0;
    }
}
