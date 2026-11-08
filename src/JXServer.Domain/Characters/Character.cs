using JXServer.Primitives.Characters;
using JXServer.Primitives.ErrorCodes;

namespace JXServer.Domain.Characters;

/// <summary>
/// Aggregate root representing a player character stored in the persistence store.
/// </summary>
public sealed class Character
{
    private readonly List<CharacterSkillValue> _skills = [];

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    public byte SlotIndex { get; private set; }

    public string RoleName { get; private set; } = string.Empty;

    public string NormalizedRoleName { get; private set; } = string.Empty;

    public CharacterGender Gender { get; private set; }

    public ElementSeries Series { get; private set; }

    public byte Level { get; private set; }

    public long Experience { get; private set; }

    public int LeadExperience { get; private set; }

    public ushort MapId { get; private set; }

    public int RevivalMapId { get; private set; }

    public int RevivalPointId { get; private set; }

    public int MapMpsX { get; private set; }

    public int MapMpsY { get; private set; }

    public int CurrentLife { get; private set; }

    public ushort MaxLife { get; private set; }

    public int CurrentMana { get; private set; }

    public ushort MaxMana { get; private set; }

    public ushort CurrentStamina { get; private set; }

    public ushort MaxStamina { get; private set; }

    public ushort Strength { get; private set; }

    public ushort Dexterity { get; private set; }

    public ushort Vitality { get; private set; }

    public ushort Energy { get; private set; }

    public ushort Lucky { get; private set; }

    public ushort UnallocatedAttributePoints { get; private set; }

    public ushort UnallocatedSkillPoints { get; private set; }

    public int InventoryMoney { get; private set; }

    public int RepositoryMoney { get; private set; }

    public byte CurrentFactionId { get; private set; }

    public byte FirstFactionId { get; private set; }

    public ushort FactionJoinCount { get; private set; }

    public IReadOnlyList<CharacterSkillValue> Skills => _skills;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid ConcurrencyStamp { get; private set; }

    private Character()
    {
    }

    public Character(
        Guid id,
        Guid accountId,
        byte slotIndex,
        string roleName,
        CharacterGender gender,
        ElementSeries series,
        ushort mapId,
        int revivalMapId,
        int revivalPointId,
        int mapMpsX,
        int mapMpsY,
        ushort strength,
        ushort dexterity,
        ushort vitality,
        ushort energy,
        ushort lucky,
        ushort maxLife,
        ushort maxMana,
        ushort maxStamina,
        IEnumerable<CharacterSkillValue>? starterSkills = null,
        DateTimeOffset? createdAtUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Character Id cannot be empty.", nameof(id));
        }

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("AccountId cannot be empty.", nameof(accountId));
        }

        if (slotIndex >= CharacterConsts.MaxRolesPerAccount)
        {
            throw new BusinessException(
                JXServerErrorCodes.CharacterLimitReached,
                $"Character SlotIndex must be between 0 and {CharacterConsts.MaxRolesPerAccount - 1}.");
        }

        string trimmedName = (roleName ?? string.Empty).TrimEnd('\0').Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new BusinessException(
                JXServerErrorCodes.CharacterNameProhibited,
                "Character RoleName cannot be empty.");
        }

        DateTimeOffset now = createdAtUtc ?? DateTimeOffset.UtcNow;

        Id = id;
        AccountId = accountId;
        SlotIndex = slotIndex;
        RoleName = trimmedName;
        NormalizedRoleName = NormalizeRoleName(trimmedName);
        Gender = gender;
        Series = series;
        Level = 1;
        Experience = 0;
        LeadExperience = 0;
        MapId = mapId;
        RevivalMapId = revivalMapId;
        RevivalPointId = revivalPointId;
        MapMpsX = mapMpsX;
        MapMpsY = mapMpsY;
        CurrentLife = maxLife;
        MaxLife = maxLife;
        CurrentMana = maxMana;
        MaxMana = maxMana;
        CurrentStamina = maxStamina;
        MaxStamina = maxStamina;
        Strength = strength;
        Dexterity = dexterity;
        Vitality = vitality;
        Energy = energy;
        Lucky = lucky;
        UnallocatedAttributePoints = 0;
        UnallocatedSkillPoints = 0;
        InventoryMoney = 0;
        RepositoryMoney = 0;
        CurrentFactionId = CharacterConsts.NoFactionId;
        FirstFactionId = CharacterConsts.NoFactionId;
        FactionJoinCount = 0;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
        ConcurrencyStamp = Guid.NewGuid();

        if (starterSkills is not null)
        {
            foreach (CharacterSkillValue skill in starterSkills)
            {
                _skills.Add(skill);
            }
        }
    }

    public static string NormalizeRoleName(string roleName) =>
        (roleName ?? string.Empty).TrimEnd('\0').Trim().ToUpperInvariant();

    public void UpdateWorldSnapshot(
        ushort mapId,
        int mpsX,
        int mpsY,
        int currentLife,
        int currentMana,
        ushort currentStamina,
        DateTimeOffset? updatedAtUtc = null)
    {
        MapId = mapId;
        MapMpsX = mpsX;
        MapMpsY = mpsY;
        CurrentLife = Math.Clamp(currentLife, 1, MaxLife);
        CurrentMana = Math.Clamp(currentMana, 0, MaxMana);
        CurrentStamina = (ushort)Math.Clamp((int)currentStamina, 0, MaxStamina);
        UpdatedAtUtc = updatedAtUtc ?? DateTimeOffset.UtcNow;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
