# Data Model: Core Zone Modular Spine, World Entry & Simulation Harness

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  
**Research**: [research.md](research.md)  

---

## 1. Modular Zone Spine Architecture (`JXServer.ZoneEngine`)

### 1.1 `ZoneTickPhase` Enum
Defines deterministic execution order for 18-Hz per-frame subsystems:

```csharp
public enum ZoneTickPhase
{
    StatusEffects = 100,      // Poision, cold, stun, buffs
    MonsterAi = 200,          // Monster state machine, aggro, pursuit
    Missiles = 300,           // 64-direction projectile flight and collision
    GroundObjects = 400,      // Ground loot expiration and ownership windows
    MovementAndPosture = 500, // Player walk/run progression, sit regeneration
    SessionHeartbeats = 600   // Periodic pings, lease synchronization, timeouts
}
```

---

### 1.2 `PlayerFeatureSlotKey<TSlot>` & `IPlayerFeatureSlot`
Typed O(1) state attachment mechanism for player sessions inside map zones:

```csharp
public sealed class PlayerFeatureSlotKey<TSlot> where TSlot : class, IPlayerFeatureSlot
{
    public string FeatureName { get; }
    public int SlotIndex { get; internal set; }
}

public interface IPlayerFeatureSlot
{
    ValueTask OnPlayerAttachedAsync(PlayerZoneState player, ZoneExecutionContext context, CancellationToken ct);
    ValueTask OnPlayerDetachedAsync(PlayerZoneState player, PlayerDetachReason reason, ZoneExecutionContext context, CancellationToken ct);
}
```

---

### 1.3 `PlayerZoneState` Authoritative Runtime Model
Authoritative in-memory state of an active player inside a map zone:

```csharp
public sealed class PlayerZoneState
{
    public required int RuntimeNpcId { get; init; }
    public required Guid AccountId { get; init; }
    public required Guid CharacterId { get; init; }
    public required long LeaseEpoch { get; init; }
    public required string RoleNameUnicode { get; init; }
    public required ushort MapId { get; set; }
    public required int CurrentMpsX { get; set; }
    public required int CurrentMpsY { get; set; }

    // Vitals & Progression
    public int CurrentLife { get; set; }
    public int MaxLife { get; set; }
    public int CurrentMana { get; set; }
    public int MaxMana { get; set; }
    public int CurrentStamina { get; set; }
    public int MaxStamina { get; set; }
    public byte Level { get; set; }
    public ElementSeries Series { get; set; }
    public CharacterGender Gender { get; set; }

    // Session & Outbound Sinks
    public bool IsSyncCompleted { get; set; }
    public long LastPingReceivedTick { get; set; }
    public IPlayerOutboundSink OutboundSink { get; init; }

    // Domain-Segregated Sinks (Cached)
    public IWorldOutboundSink WorldSink { get; }
    public ICombatOutboundSink CombatSink { get; }
    public IInventoryOutboundSink InventorySink { get; }
    public IChatOutboundSink ChatSink { get; }

    // Modular Feature State Slots
    public TSlot? GetFeatureSlot<TSlot>(PlayerFeatureSlotKey<TSlot> key);
    public void SetFeatureSlot<TSlot>(PlayerFeatureSlotKey<TSlot> key, TSlot slot);
}
```

---

## 2. Foundational Character & Item Persistence Attributes

### 2.1 `Character` Persistence Extensions (`JXServer.Domain.Characters`)
- **`TaskValues`**: Sparse dictionary `Dictionary<ushort, int>` representing quest progress variables across keys `0..65535`.
  - Serialized to `TaskValuesJson` column via `TaskValuesJsonSerializer`.
  - Reading an unassigned key returns `0`.
  - Keys reset to `0` are pruned from JSON serialization.
- **`RepositoryPasswordHash`**: Optional 32-character uppercase hexadecimal MD5 digest string (`string?`), validated via `KsgPasswordHasher.IsHexDigest32`. Default: `null`.

### 2.2 `CharacterItem` Persistence Extensions (`JXServer.Domain.Characters`)
- **`IsBroken`** (`bool`): Mutable boolean indicating equipment zero-durability broken status. When `true`, equipment combat bonuses are suspended until repaired. Default: `false`.
- **`EnchaseLevel`** (`byte`): Integer `0..10` representing Purple equipment crafting enhancement tier. Default: `0`.

---

## 3. Domain-Segregated Outbound Sinks

- **`IWorldOutboundSink`** (14 methods): `SendSyncWorld`, `SendSyncCurPlayer`, `SendSyncCurPlayerSkill`, `SendSyncCurPlayerNormal`, `SendSyncNpcFull`, `SendSyncNpcMinPlayer`, `SendDisabledFuncFlag`, `SendSyncEnd`, `SendPing`, `SendPingReply`, `SendWalkBroadcast`, `SendRunBroadcast`, `SendJumpBroadcast`, `SendSitBroadcast`.
- **`ICombatOutboundSink`** (10 methods): `SendSkillCastBroadcast`, `SendAuraChangeBroadcast`, `SendDamageNumber`, `SendStatusEffectApplied`, `SendHurtRecoil`, `SendDeath`, `SendRevival`, `SendExperienceGained`, `SendAttributePointsUpdated`, `SendSkillPointsUpdated`.
- **`IInventoryOutboundSink`** (7 methods): `SendContainerSync`, `SendItemMoved`, `SendItemSwapped`, `SendItemStackSplit`, `SendItemDurabilityUpdated`, `SendEquipmentUpdated`, `SendItemRemoved`.
- **`IChatOutboundSink`** (3 methods): `SendNearbySpeech`, `SendChannelMessage`, `SendSystemBroadcast`.
- **`IPlayerOutboundSink`**: Unified composite interface inheriting from all 4 domain sinks.

---

## 4. GameServer World Entry Wire Packets

| Opcode | Direction | Wire Size | Name | Payload Structure |
| :--- | :--- | :--- | :--- | :--- |
| `0x42` | C2S | 17B | `LogicLoginC2SPacket` | `+0x00` (`1B`): `0x42`<br>`+0x01` (`16B`): `Guid` handoff ticket |
| `0x49` | S2C | 34B | `SyncWorldS2CPacket` | Map ID, subworld width/height, server timestamp |
| `0x44` | S2C | 68B | `SyncCurPlayerS2CPacket` | Character ID, role name, series, gender, attributes |
| `0x45` | S2C | 483B | `SyncCurPlayerSkillS2CPacket` | Up to 80 learned skills, levels, and experience |
| `0x46` | S2C | 14B | `SyncCurPlayerNormalS2CPacket` | Current life, mana, stamina vitals |
| `0x4C` | S2C | Var | `SyncNpcFullS2CPacket` | Visual equipment appearance and name |
| `0x4E` | S2C | 13B | `SyncNpcMinPlayerS2CPacket` | Minimal player sync (coordinates and state) |
| `0xA4` | S2C | 5B | `DisabledFuncFlagS2CPacket` | Server feature flags bitmask |
| `0x43` | S2C | 1B | `SyncEndS2CPacket` | Single opcode byte signaling sync completion |
| `0x43` | C2S | 9B | `SyncEndC2SPacket` | Confirmation reply from client |
| `0x82` | S2C | 5B | `PingS2CPacket` | Server periodic ping request |
| `0x70` | C2S | 9B | `PingReplyC2SPacket` | Client ping reply |
| `0x99` | S2C | 5B | `PingReplyS2CPacket` | Server ping acknowledgment |
| `0x9B` | C2S | 1B | `CloseConnectionC2SPacket` | Client clean logout notification |
