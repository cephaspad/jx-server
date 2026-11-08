using JXServer.Primitives.Characters;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Pure in-memory runtime player state owned exclusively by a single-threaded 18-Hz <c>MapZoneWorker</c>.
/// Completely decoupled from EF Core entities (Constitution Principle VI).
/// </summary>
public sealed class PlayerZoneState
{
    public uint RuntimeNpcId { get; }

    public Guid AccountId { get; }

    public Guid CharacterId { get; }

    public long LeaseEpoch { get; }

    public string RoleNameUnicode { get; }

    public CharacterGender Gender { get; }

    public ElementSeries Series { get; }

    public byte Level { get; set; }

    public long Experience { get; set; }

    public int LeadExperience { get; set; }

    public ushort MapId { get; set; }

    public int CurrentMpsX { get; set; }

    public int CurrentMpsY { get; set; }

    public int CurrentLife { get; set; }

    public ushort MaxLife { get; set; }

    public int CurrentMana { get; set; }

    public ushort MaxMana { get; set; }

    public ushort CurrentStamina { get; set; }

    public ushort MaxStamina { get; set; }

    public ushort Strength { get; set; }

    public ushort Dexterity { get; set; }

    public ushort Vitality { get; set; }

    public ushort Energy { get; set; }

    public ushort Lucky { get; set; }

    public ushort UnallocatedAttributePoints { get; set; }

    public ushort UnallocatedSkillPoints { get; set; }

    public int InventoryMoney { get; set; }

    public int RepositoryMoney { get; set; }

    public byte CurrentFactionId { get; set; }

    public byte FirstFactionId { get; set; }

    public ushort FactionJoinCount { get; set; }

    public uint ClientToken { get; set; }

    public uint ClientTick { get; set; }

    public bool IsSyncCompleted { get; set; }

    public uint LastSentPingServerTimestamp { get; set; }

    public long LastPingSentTick { get; set; }

    public long LastPingReceivedTick { get; set; }

    public IPlayerOutboundSink OutboundSink { get; }

    public PlayerZoneState(
        uint runtimeNpcId,
        Guid accountId,
        Guid characterId,
        long leaseEpoch,
        string roleNameUnicode,
        CharacterGender gender,
        ElementSeries series,
        byte level,
        long experience,
        int leadExperience,
        ushort mapId,
        int currentMpsX,
        int currentMpsY,
        int currentLife,
        ushort maxLife,
        int currentMana,
        ushort maxMana,
        ushort currentStamina,
        ushort maxStamina,
        ushort strength,
        ushort dexterity,
        ushort vitality,
        ushort energy,
        ushort lucky,
        ushort unallocatedAttributePoints,
        ushort unallocatedSkillPoints,
        int inventoryMoney,
        int repositoryMoney,
        byte currentFactionId,
        byte firstFactionId,
        ushort factionJoinCount,
        long initialTick,
        IPlayerOutboundSink outboundSink)
    {
        ArgumentNullException.ThrowIfNull(outboundSink);

        RuntimeNpcId = runtimeNpcId;
        AccountId = accountId;
        CharacterId = characterId;
        LeaseEpoch = leaseEpoch;
        RoleNameUnicode = roleNameUnicode;
        Gender = gender;
        Series = series;
        Level = level;
        Experience = experience;
        LeadExperience = leadExperience;
        MapId = mapId;
        CurrentMpsX = currentMpsX;
        CurrentMpsY = currentMpsY;
        CurrentLife = currentLife;
        MaxLife = maxLife;
        CurrentMana = currentMana;
        MaxMana = maxMana;
        CurrentStamina = currentStamina;
        MaxStamina = maxStamina;
        Strength = strength;
        Dexterity = dexterity;
        Vitality = vitality;
        Energy = energy;
        Lucky = lucky;
        UnallocatedAttributePoints = unallocatedAttributePoints;
        UnallocatedSkillPoints = unallocatedSkillPoints;
        InventoryMoney = inventoryMoney;
        RepositoryMoney = repositoryMoney;
        CurrentFactionId = currentFactionId;
        FirstFactionId = firstFactionId;
        FactionJoinCount = factionJoinCount;
        LastPingSentTick = initialTick;
        LastPingReceivedTick = initialTick;
        OutboundSink = outboundSink;
    }
}
