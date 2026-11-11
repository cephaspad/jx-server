# Research & Ground-Truth Rationale: Core Zone Modular Spine, World Entry & Simulation Harness

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  
**Ground-Truth Sources**: `jx-swarm/docs/01-session-lifecycle/gameserver-world-entry.md`, `SwordOnline/Sources/Core/Src/KSubWorld.cpp`, `KPlayer.cpp`  

---

## 1. Architectural Decisions & Ground-Truth Findings

### Decision 1: Open/Closed Modular Zone Extension Spine
- **Problem**: Monolithic zone implementations handle all commands and tick systems via sprawling `switch` statements and hardcoded method sequences. Adding new gameplay mechanics (such as shops, quest chains, parties, and trade) directly modified `MapZoneWorker`, introducing regression risks and merge conflicts across parallel teams.
- **Selected Solution**:
  - `IZoneModule`: Pluggable registration interface exposing `Register(IZoneExtensionBuilder builder)`.
  - `ZoneExtensionRegistry`: Pre-compiled immutable registry storing:
    - Command routes: `FrozenDictionary<Type, ZoneCommandRoute>` enabling zero-boxing, zero-allocation dispatch via typed `IZoneCommandHandler<TCommand>`.
    - Phased tick pipeline: `IZoneTickSystem[]` sorted deterministically by `(ZoneTickPhase, SubOrder, RegistrationOrder)`.
    - Player feature state slots: Pre-indexed array of `PlayerFeatureSlotKey<TSlot>` slots providing $O(1)$ state lookups per player.
  - Fail-Fast Validation: Registration of duplicate command types, duplicate tick system names, or duplicate feature slot keys fails immediately during startup before players are accepted.
  - Fault Isolation: Command dispatch and per-player tick execution are wrapped in per-session `try/catch` boundaries. If an unhandled exception occurs in a module, it is logged with structured diagnostic telemetry (`ZoneEngineLogMessages`) and isolated to that player session, leaving all other sessions and the 18-Hz zone loop completely unaffected.

### Decision 2: Deterministic High-Speed Zone Simulation Test Harness
- **Problem**: Verifying time-dependent game mechanics (skill cooldowns, DoT ticks, monster respawn delays, and loot despawns) over live network sockets required waiting on real-world clocks. A 10-minute combat scenario required 10 minutes of wall-clock execution time, making automated CI suites slow, expensive, and flaky.
- **Selected Solution**:
  - `ZoneSimulationHarness` instantiates an in-memory `MapZoneWorker` using a controllable `FakeTimeProvider` and deterministic fixed `RandomSeed`.
  - Stepping virtual frames (`StepFramesAsync(1000)`) advances simulation ticks without network sleeps, external databases, or socket polling.
  - The entire 1,000-frame batch (~55.5 seconds of game time) executes in **`< 5` milliseconds** on standard hardware.
  - An inspectable `PlayerOutboundEventRecorder` captures all emitted notifications per player, tagged by frame number and gameplay domain, enabling exact frame-boundary assertions.

### Decision 3: Domain-Segregated Outbound Player Notification Channels
- **Problem**: Monolithic `IPlayerOutboundSink` contracts exposed 34+ methods on a single interface. Downstream modules collided on this single file, and test stubs had to implement dozens of unrelated methods.
- **Selected Solution**:
  - Partitioned into 4 cohesive functional domain sinks:
    1. `IWorldOutboundSink` (14 methods): Map entry, sync bursts, region visibility, vitals, postures.
    2. `ICombatOutboundSink` (10 methods): Skill casting, damage numbers, status effects, hurt recoil.
    3. `IInventoryOutboundSink` (7 methods): Container sync, item movement, equip state, durability updates.
    4. `IChatOutboundSink` (3 methods): Nearby talk (`0x5B`), structured multi-channel chat (`0x7E`), system notices (`0xFB`).
  - `IPlayerOutboundSink` inherits from all 4 domain sinks, preserving backward compatibility.
  - `GameServerClientSession` is decomposed into 5 partial class files sharing its single FIFO `_outboundChannel`.

### Decision 4: Foundational Persistence Baseline
- **Problem**: Downstream modules (quests, repository box, durability, crafting) require specific persistent fields on `Character` and `CharacterItem`. Delivering these incrementally would cause repeated schema migrations and breaking changes.
- **Selected Solution**:
  - `Character.TaskValues`: Sparse `Dictionary<ushort, int>` serialized to `TaskValuesJson`, representing quest progress across IDs `0..65535`. Unassigned keys default to `0`, and zero-valued entries are pruned to keep storage compact.
  - `Character.RepositoryPasswordHash`: Optional 32-character uppercase MD5 hex digest for secondary password protection, defaulting to `null` (unlocked).
  - `CharacterItem.IsBroken`: Boolean flag indicating zero-durability broken state (disabling combat bonuses until repaired), defaulting to `false`.
  - `CharacterItem.EnchaseLevel`: Byte value (`0..10`) representing Purple equipment enchasing tier, defaulting to `0`.
  - Stored and hydrated losslessly across world entry, map transfers, and lease-epoch-fenced logout saves.

### Decision 5: Authentic GameServer World Entry Handshake
- **Handshake Packet Sequence**:
  1. Client sends `LogicLoginC2SPacket` (`0x42`, 17 bytes) carrying the single-use 16-byte handoff ticket.
  2. World server redeems ticket against `ISessionLeaseAppService`, binds session to target map, and transmits initial sync burst:
     - `SyncWorldS2CPacket` (`0x49`): Map ID, subworld dimensions, server time.
     - `SyncCurPlayerS2CPacket` (`0x44`): Character ID, role name, elemental series, level, attributes.
     - `SyncCurPlayerSkillS2CPacket` (`0x45`): Learned skills, levels, and experience.
     - `SyncCurPlayerNormalS2CPacket` (`0x46`): Current life, mana, stamina vitals.
     - `SyncNpcFullS2CPacket` (`0x4C`): Full player appearance, equipment visuals, and name.
     - `DisabledFuncFlagS2CPacket` (`0xA4`): Server disabled function bitmasks.
     - `SyncEndS2CPacket` (`0x43`, 1 byte): End of initial synchronization signal.
  3. Client replies with `SyncEndC2SPacket` (`0x43`, 9 bytes).
  4. Server activates character session in 18-Hz simulation loop and initiates periodic 5-second heartbeats (`0x82` $\rightarrow$ `0x70` $\rightarrow$ `0x99`).
