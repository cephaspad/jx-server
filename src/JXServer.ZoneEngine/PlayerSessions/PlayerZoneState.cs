using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Chats;
using JXServer.Primitives.Combat;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.Logging;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Pure in-memory runtime player state owned exclusively by a single-threaded 18-Hz <c>MapZoneWorker</c>.
/// Completely decoupled from EF Core entities (Constitution Principle VI).
/// </summary>
public sealed class PlayerZoneState
{
    private readonly Dictionary<int, ushort> _learnedSkills = [];
    private readonly Dictionary<int, long> _skillReadyTickBySkillId = [];
    private readonly Dictionary<ushort, int> _taskValues = [];
    private IPlayerFeatureSlot?[] _featureSlots = [];

    public uint RuntimeNpcId { get; set; }

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

    public int RevivalMapId { get; set; } = CharacterConsts.DefaultSpawnMapId;

    public int RevivalPointId { get; set; } = CharacterConsts.DefaultRevivalPointId;

    public int CurrentMpsX { get; set; }

    public int CurrentMpsY { get; set; }

    public int TargetMpsX { get; set; }

    public int TargetMpsY { get; set; }

    public int RegionX => CurrentMpsX / WorldCoordinateConsts.RegionWidthMps;

    public int RegionY => CurrentMpsY / WorldCoordinateConsts.RegionHeightMps;

    public EntityDoingPosture Doing { get; set; } = EntityDoingPosture.Stand;

    public EntityCamp Camp { get; set; } = EntityCamp.Begin;

    public byte Dir { get; set; }

    public byte FightMode { get; set; }

    public PlayerPkMode PkState { get; set; } = PlayerPkMode.Peace;

    public sbyte PkValue { get; set; }

    public long LastPkModeChangeTick { get; set; }

    public int ActiveAuraSkillId { get; set; }

    public int WalkSpeed { get; set; } = 10;

    public int RunSpeed { get; set; } = 20;

    public uint LastTriggeredTrapId { get; set; }

    public uint SubscribedChatChannelMask { get; set; } = ChatConsts.DefaultSubscribedChannelMask;

    public long LastSitRegenTick { get; set; }

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

    // Combat & Equipment aggregated bonus stats
    public int BonusLifeMax { get; set; }

    public int BonusManaMax { get; set; }

    public int BonusStaminaMax { get; set; }

    public int BonusStrength { get; set; }

    public int BonusDexterity { get; set; }

    public int BonusVitality { get; set; }

    public int BonusEnergy { get; set; }

    public int WeaponMinDamage { get; set; }

    public int WeaponMaxDamage { get; set; }

    public int BonusPhysicsDamage { get; set; }

    public int BonusAttackRating { get; set; }

    public int BonusDefense { get; set; }

    public int PhysicsResistPercent { get; set; }

    public int ColdResistPercent { get; set; }

    public int FireResistPercent { get; set; }

    public int LightResistPercent { get; set; }

    public int PoisonResistPercent { get; set; }

    // Medicine gradual regeneration state
    public int MedicineLifeRegenPerTick { get; set; }

    public int MedicineLifeRemainingFrames { get; set; }

    public int MedicineManaRegenPerTick { get; set; }

    public int MedicineManaRemainingFrames { get; set; }

    public EntityStatusEffectState StatusEffects { get; } = new();

    public PlayerInventoryGrid Inventory { get; } = new();

    public IReadOnlyDictionary<int, ushort> LearnedSkills => _learnedSkills;

    public IPlayerOutboundSink OutboundSink { get; }
    public IWorldOutboundSink WorldSink { get; }
    public ICombatOutboundSink CombatSink { get; }
    public IInventoryOutboundSink InventorySink { get; }
    public IChatOutboundSink ChatSink { get; }

    public TSink? GetOptionalOutboundSink<TSink>() where TSink : class =>
        OutboundSink.TryGetOutboundSink<TSink>(out TSink? sink) ? sink : null;

    public bool TryGetOptionalOutboundSink<TSink>([NotNullWhen(true)] out TSink? sink) where TSink : class =>
        OutboundSink.TryGetOutboundSink(out sink);

    public IReadOnlyDictionary<ushort, int> TaskValues => _taskValues;

    public string? RepositoryPasswordHash { get; set; }

    public int GetTaskValue(ushort taskId) =>
        _taskValues.GetValueOrDefault(taskId, 0);

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
    }

    public Dictionary<ushort, int> ExportTaskValuesSnapshot() => new(_taskValues);

    public int EffectiveStrength => Strength + BonusStrength;

    public int EffectiveDexterity => Dexterity + BonusDexterity;

    public int EffectiveVitality => Vitality + BonusVitality;

    public int EffectiveEnergy => Energy + BonusEnergy;

    public int EffectiveAttackRating => Math.Max(10, (EffectiveDexterity * 4) + (Level * 5) + BonusAttackRating);

    public int EffectiveDefense => Math.Max(0, (EffectiveDexterity * 2) + (Level * 2) + BonusDefense);

    public int EffectiveWalkSpeed => StatusEffects.IsFrozen ? Math.Max(3, WalkSpeed / 2) : WalkSpeed;

    public int EffectiveRunSpeed => StatusEffects.IsFrozen ? Math.Max(6, RunSpeed / 2) : RunSpeed;

    public byte LifePercent => MaxLife > 0
        ? (byte)Math.Clamp((CurrentLife * 100) / MaxLife, 0, 100)
        : (byte)100;

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
        TargetMpsX = currentMpsX;
        TargetMpsY = currentMpsY;
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
        LastSitRegenTick = initialTick;
        OutboundSink = outboundSink;
        WorldSink = outboundSink.TryGetOutboundSink<IWorldOutboundSink>(out var ws) ? ws : NoOpWorldOutboundSink.Instance;
        CombatSink = outboundSink.TryGetOutboundSink<ICombatOutboundSink>(out var cs) ? cs : NoOpCombatOutboundSink.Instance;
        InventorySink = outboundSink.TryGetOutboundSink<IInventoryOutboundSink>(out var isink) ? isink : NoOpInventoryOutboundSink.Instance;
        ChatSink = outboundSink.TryGetOutboundSink<IChatOutboundSink>(out var chs) ? chs : NoOpChatOutboundSink.Instance;
    }

    public void SetLearnedSkill(int skillId, ushort level)
    {
        if (skillId > 0 && level > 0)
        {
            _learnedSkills[skillId] = level;
        }
    }

    public bool TryGetLearnedSkillLevel(int skillId, out ushort level) =>
        _learnedSkills.TryGetValue(skillId, out level);

    public bool IsSkillOffCooldown(int skillId, long currentTick) =>
        !_skillReadyTickBySkillId.TryGetValue(skillId, out long readyTick) || currentTick >= readyTick;

    public void SetSkillCooldown(int skillId, long readyTick) =>
        _skillReadyTickBySkillId[skillId] = readyTick;

    public EntityFullSyncSnapshot CreateFullSyncSnapshot()
    {
        ushort templateId = Gender == CharacterGender.Male ? (ushort)1 : (ushort)2;
        uint packedSettingIdx = ((uint)Level << 16) | templateId;
        return new EntityFullSyncSnapshot(
            NpcId: RuntimeNpcId,
            MpsX: CurrentMpsX,
            MpsY: CurrentMpsY,
            NpcSettingIdx: packedSettingIdx,
            Series: Series,
            LifePer: LifePercent,
            Doing: Doing,
            Camp: Camp,
            Dir: Dir,
            Kind: EntityKind.Player,
            IsSpecialNpc: false,
            NameUtf8: RoleNameUnicode);
    }

    public TSlot GetOrCreateFeatureSlot<TSlot>(PlayerFeatureSlotKey<TSlot> key, Func<TSlot> factory)
        where TSlot : class, IPlayerFeatureSlot
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        EnsureFeatureSlotCapacity(key.Index);
        if (_featureSlots[key.Index] is TSlot existing)
        {
            return existing;
        }

        TSlot created = factory();
        _featureSlots[key.Index] = created;
        return created;
    }

    public bool TryGetFeatureSlot<TSlot>(PlayerFeatureSlotKey<TSlot> key, [NotNullWhen(true)] out TSlot? slot)
        where TSlot : class, IPlayerFeatureSlot
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Index < _featureSlots.Length && _featureSlots[key.Index] is TSlot matched)
        {
            slot = matched;
            return true;
        }

        slot = null;
        return false;
    }

    public bool RemoveFeatureSlot<TSlot>(PlayerFeatureSlotKey<TSlot> key, [NotNullWhen(true)] out TSlot? removedSlot)
        where TSlot : class, IPlayerFeatureSlot
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Index < _featureSlots.Length && _featureSlots[key.Index] is TSlot matched)
        {
            _featureSlots[key.Index] = null;
            removedSlot = matched;
            return true;
        }

        removedSlot = null;
        return false;
    }

    public void DetachAllFeatureSlots(ZoneExecutionContext context, PlayerDetachReason reason)
    {
        for (int i = 0; i < _featureSlots.Length; i++)
        {
            IPlayerFeatureSlot? slot = _featureSlots[i];
            if (slot is not null)
            {
                _featureSlots[i] = null;
                try
                {
                    slot.OnPlayerDetached(this, context, reason);
                }
                catch (Exception ex)
                {
                    ZoneEngineLogMessages.LogPlayerFeatureSlotCleanupFaulted(
                        context.Logger,
                        ex,
                        slot.GetType().Name,
                        CharacterId,
                        context.MapId,
                        reason);
                }
            }
        }
    }

    private void EnsureFeatureSlotCapacity(int requiredIndex)
    {
        if (requiredIndex >= _featureSlots.Length)
        {
            int newSize = Math.Max(requiredIndex + 1, Math.Max(4, _featureSlots.Length * 2));
            Array.Resize(ref _featureSlots, newSize);
        }
    }
}
