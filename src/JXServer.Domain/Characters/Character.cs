using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using JXServer.Primitives.Encodings;
using JXServer.Primitives.ErrorCodes;

namespace JXServer.Domain.Characters;

/// <summary>
/// Aggregate root representing a player character stored in the persistence store.
/// </summary>
public sealed class Character
{
    private readonly List<CharacterSkillValue> _skills = [];
    private readonly List<CharacterItem> _items = [];
    private readonly Dictionary<ushort, int> _taskValues = [];

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

    public PlayerPkMode PkState { get; private set; }

    public sbyte PkValue { get; private set; }

    public int Repute { get; private set; }

    public int FuYuan { get; private set; }

    public int Honor { get; private set; }

    public byte TransLifeCount { get; private set; }

    public int ZhenYuan { get; private set; }

    public ushort ActiveAuraSkillId { get; private set; }

    public IReadOnlyList<CharacterSkillValue> Skills => _skills;

    public IReadOnlyList<CharacterItem> Items => _items;

    public IReadOnlyDictionary<ushort, int> TaskValues => _taskValues;

    /// <summary>
    /// Optional 32-character uppercase hexadecimal MD5 digest protecting the character's storage repository box.
    /// <see langword="null"/> indicates no password is set (repository unlocked).
    /// </summary>
    public string? RepositoryPasswordHash { get; private set; }

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
        DateTimeOffset? createdAtUtc = null,
        IEnumerable<CharacterItem>? starterItems = null,
        IEnumerable<KeyValuePair<ushort, int>>? starterTaskValues = null,
        string? repositoryPasswordHash = null)
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
        PkState = PlayerPkMode.Peace;
        PkValue = 0;
        Repute = 0;
        FuYuan = 0;
        Honor = 0;
        TransLifeCount = 0;
        ZhenYuan = 0;
        ActiveAuraSkillId = 0;
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

        if (starterItems is not null)
        {
            foreach (CharacterItem item in starterItems)
            {
                _items.Add(item);
            }
        }

        if (starterTaskValues is not null)
        {
            ReplaceTaskValues(starterTaskValues);
        }

        if (repositoryPasswordHash is not null)
        {
            SetRepositoryPasswordHash(repositoryPasswordHash);
        }
    }

    public static string NormalizeRoleName(string roleName) =>
        (roleName ?? string.Empty).TrimEnd('\0').Trim().ToUpperInvariant();

    /// <summary>
    /// Returns the persisted value for <paramref name="taskId"/> (<c>0..65535</c>), or <c>0</c> if unset.
    /// </summary>
    public int GetTaskValue(ushort taskId) =>
        _taskValues.GetValueOrDefault(taskId, 0);

    /// <summary>
    /// Sets <paramref name="taskId"/> to <paramref name="value"/>. Setting <c>0</c> removes the key so sparse storage
    /// never accumulates zero-valued entries.
    /// </summary>
    public void SetTaskValue(ushort taskId, int value)
    {
        if (value == 0)
        {
            _taskValues.Remove(taskId);
        }
        else
        {
            _taskValues[taskId] = value;
        }

        ConcurrencyStamp = Guid.NewGuid();
    }

    public void ReplaceTaskValues(IEnumerable<KeyValuePair<ushort, int>>? values)
    {
        _taskValues.Clear();
        if (values is not null)
        {
            foreach ((ushort taskId, int val) in values)
            {
                if (val != 0)
                {
                    _taskValues[taskId] = val;
                }
            }
        }

        ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>
    /// Sets or clears the normalized repository password digest.
    /// Null or whitespace clears the lock (<see langword="null"/>). Any non-empty input MUST already be a valid
    /// 32-character hexadecimal MD5 digest; raw cleartext or malformed strings throw <see cref="BusinessException"/>.
    /// </summary>
    public void SetRepositoryPasswordHash(string? normalizedMd5Hex)
    {
        ReadOnlySpan<char> trimmed = (normalizedMd5Hex ?? string.Empty).AsSpan().TrimEnd('\0').Trim();
        if (trimmed.IsEmpty)
        {
            RepositoryPasswordHash = null;
            ConcurrencyStamp = Guid.NewGuid();
            return;
        }

        if (!KsgPasswordHasher.IsHexDigest32(trimmed))
        {
            throw new BusinessException(
                JXServerErrorCodes.CharacterInvalidRepositoryCredential,
                "RepositoryPasswordHash must be null/empty or a valid 32-character hexadecimal MD5 digest.");
        }

        RepositoryPasswordHash = trimmed.ToString().ToUpperInvariant();
        ConcurrencyStamp = Guid.NewGuid();
    }

    public void SetRepositoryPasswordFromPlaintext(ReadOnlySpan<char> plaintextPassword)
    {
        ReadOnlySpan<char> trimmed = plaintextPassword.TrimEnd('\0').Trim();
        SetRepositoryPasswordHash(trimmed.IsEmpty ? null : KsgPasswordHasher.HashPlaintext(trimmed));
    }

    public bool VerifyRepositoryPassword(ReadOnlySpan<char> candidatePasswordOrHash) =>
        RepositoryPasswordHash is null || KsgPasswordHasher.Verify(RepositoryPasswordHash, candidatePasswordOrHash);

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

    public void UpdateWorldSnapshot(
        ushort mapId,
        int mpsX,
        int mpsY,
        int currentLife,
        int currentMana,
        ushort currentStamina,
        byte level,
        long experience,
        int leadExperience,
        ushort strength,
        ushort dexterity,
        ushort vitality,
        ushort energy,
        ushort unallocatedAttributePoints,
        ushort unallocatedSkillPoints,
        ushort maxLife,
        ushort maxMana,
        ushort maxStamina,
        int inventoryMoney,
        int repositoryMoney,
        PlayerPkMode pkState,
        sbyte pkValue,
        ushort activeAuraSkillId,
        IEnumerable<CharacterSkillValue>? skills,
        IEnumerable<CharacterItem>? items,
        DateTimeOffset? updatedAtUtc = null,
        IEnumerable<KeyValuePair<ushort, int>>? taskValues = null,
        string? repositoryPasswordHash = null,
        bool updateRepositoryPasswordHash = false)
    {
        MapId = mapId;
        MapMpsX = mpsX;
        MapMpsY = mpsY;
        Level = Math.Max((byte)1, level);
        Experience = Math.Max(0, experience);
        LeadExperience = Math.Max(0, leadExperience);
        Strength = strength;
        Dexterity = dexterity;
        Vitality = vitality;
        Energy = energy;
        UnallocatedAttributePoints = unallocatedAttributePoints;
        UnallocatedSkillPoints = unallocatedSkillPoints;
        MaxLife = Math.Max((ushort)1, maxLife);
        MaxMana = Math.Max((ushort)1, maxMana);
        MaxStamina = Math.Max((ushort)1, maxStamina);
        CurrentLife = Math.Clamp(currentLife, 1, MaxLife);
        CurrentMana = Math.Clamp(currentMana, 0, MaxMana);
        CurrentStamina = (ushort)Math.Clamp((int)currentStamina, 0, MaxStamina);
        InventoryMoney = Math.Max(0, inventoryMoney);
        RepositoryMoney = Math.Max(0, repositoryMoney);
        PkState = pkState;
        PkValue = pkValue;
        ActiveAuraSkillId = activeAuraSkillId;

        if (skills is not null)
        {
            _skills.Clear();
            _skills.AddRange(skills);
        }

        if (items is not null)
        {
            _items.Clear();
            _items.AddRange(items);
        }

        if (taskValues is not null)
        {
            ReplaceTaskValues(taskValues);
        }

        if (updateRepositoryPasswordHash)
        {
            SetRepositoryPasswordHash(repositoryPasswordHash);
        }

        UpdatedAtUtc = updatedAtUtc ?? DateTimeOffset.UtcNow;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
