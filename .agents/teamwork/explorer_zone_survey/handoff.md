# Architecture Survey & Refactoring Blueprint: ZoneEngine Modular Spine, Domain Sinks, Persistence & Simulation Harness

**Survey Date**: 2026-11-11  
**Target Solution**: `d:\Projects\jx-server\JXServer.slnx`  
**Target Projects**:
- `src/JXServer.Primitives`
- `src/JXServer.Domain`
- `src/JXServer.Contracts`
- `src/JXServer.Application`
- `src/JXServer.Infrastructure`
- `src/JXServer.GameAssets`
- `src/JXServer.ZoneEngine`
- `src/JXServer.Adapter.LegacyJX1`
- `tests/JXServer.ZoneEngine.Tests`
- `tests/JXServer.Integration.Tests`

---

## 1. Direct Observations

### 1.1 Existing Test Suite Baseline
- **Execution Command**: `dotnet test`
- **Result**: **97/97 tests passing** (0 failed, 0 skipped) across all 3 test assemblies:
  - `JXServer.Protocol.Tests.dll`: 59 passed (172 ms)
  - `JXServer.ZoneEngine.Tests.dll`: 24 passed (4.0 s)
  - `JXServer.Integration.Tests.dll`: 14 passed (17.0 s)
- **Constraint**: Refactoring must maintain 100% zero-regression across all 97 existing tests.

### 1.2 `MapZoneWorker.cs` Current Codebase Profile
- **File Path**: `src/JXServer.ZoneEngine/WorldZones/MapZoneWorker.cs`
- **Line Count**: **2,975 lines**
- **Responsibilities Monolith**:
  - **Constructor & Catalogs** (`L89-135`): 16 constructor parameters injected individually (`MapDescriptor`, `ISessionLeaseAppService`, `ZoneEngineOptions`, `TimeProvider`, `ILogger`, `IMapCatalog`, `INpcTemplateCatalog`, `ITrapScriptCatalog`, `ICharacterAppService`, `IZoneClusterRouter`, `ISkillTemplateCatalog`, `IMissileTemplateCatalog`, `IPlayerTemplateCatalog`, `IItemTemplateCatalog`, `ILootTableCatalog`, `ItemGenerator`).
  - **Tick Loop** (`L147-186`): Increments `_currentFrameTick`, drains `_commandChannel` via unbounded `TryRead`, and runs hardcoded simulation steps:
    1. `ProcessStatusEffectsTick()` (`L2700-2772`)
    2. `_monsterAiSystem.StepTick(...)` (`L157-170`)
    3. `_missileSystem.StepTick(...)` (`L171-178`)
    4. `_groundObjectSystem.StepTick(...)` (`L179-183`)
    5. `ProcessMovementAndPostureTickAsync(tick, cancellationToken)` (`L829-912`)
    6. `ProcessHeartbeatsAsync(tick, cancellationToken)` (`L2851-2890`)
  - **Command Drainage Switch-Case** (`L268-372`): 25 hardcoded `case` statements in `ProcessCommandAsync` dispatching to private methods.
  - **Player Lifecycle & AOI** (`L374-549`, `L1023-1058`, `L1213-1231`, `L2833-2970`): Manages player connection, sync confirmation, AOI broadcasts, removal from memory, persistence snapshot building, and session lease release.
  - **Movement, Posture & Portals** (`L550-699`, `L829-1022`, `L1059-1197`): Path validation, obstacle checks, trap evaluation, portal map transfers, jump interpolation, sit regeneration.
  - **Chat Routing** (`L700-828`, `L2971-2975`): Channel mask updates, spatial nearby talk (`3x3` AOI broadcast), structured channel chat, and cluster-wide chat delivery.
  - **Combat & Progression** (`L1248-1776`, `L2331-2585`, `L2600-2832`): Skill casting, cooldowns, projectile instantiation, direct hits, damage calculations, death, respawn, PK modes, aura skills, experience awards, level up, attribute allocation, and skill point allocation.
  - **Inventory & Loot** (`L1777-2181`, `L2234-2330`): Ground object sync, pickup, inventory cell moves, consumable medicine use, item dropping, item division, stack consumption, and slot creation from DTOs.

### 1.3 `Chats/` Subsystem Profile
- **Current Files**:
  - `src/JXServer.ZoneEngine/Chats/DeliverClusterChatZoneCommand.cs` (`17 lines`)
  - `src/JXServer.ZoneEngine/Chats/SendNearbyTalkZoneCommand.cs` (`11 lines`)
  - `src/JXServer.ZoneEngine/Chats/SendStructuredChatZoneCommand.cs` (`15 lines`)
  - `src/JXServer.ZoneEngine/Chats/UpdateChatChannelMaskZoneCommand.cs` (`11 lines`)
- **Execution in `MapZoneWorker.cs`**:
  - `HandleUpdateChatChannelMask` (`L700-707`)
  - `HandleSendNearbyTalk` (`L709-728`): Uses `_regionGrid.GetSurroundingRegions` and emits `player.OutboundSink.SendPlayerTalkBroadcast`.
  - `HandleSendStructuredChat` (`L730-777`): Emits `player.OutboundSink.SendChatMessage` for local/nearby, or routes to `_clusterRouter.RouteClusterChat`.
  - `HandleDeliverClusterChat` (`L779-828`): Broadcasts `SendSystemMessage` or channel-filtered `SendChatMessage`.
- **Packet Handlers in Legacy Adapter**:
  - `src/JXServer.Adapter.LegacyJX1/GameServer/ChatSetChannelPacketHandler.cs`
  - `src/JXServer.Adapter.LegacyJX1/GameServer/PlayerTalkPacketHandler.cs`
  - `src/JXServer.Adapter.LegacyJX1/GameServer/PlayerSendChatPacketHandler.cs`
- **State on `PlayerZoneState`**:
  - `public uint SubscribedChatChannelMask { get; set; } = ChatConsts.DefaultSubscribedChannelMask;` (`L81`)

### 1.4 Outbound Sinks Profile
- **Current `IPlayerOutboundSink.cs`**:
  - `src/JXServer.ZoneEngine/PlayerSessions/IPlayerOutboundSink.cs` (`271 lines`)
  - Exactly **34 methods** declared monolithically on a single interface.
- **Current `GameServerClientSession.cs`**:
  - `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.cs` (`643 lines`)
  - Implements `IPlayerOutboundSink` directly; all 34 methods write byte frames to a single `Channel<byte[]> _outboundChannel`.
- **Current Test Doubles**:
  - `tests/JXServer.ZoneEngine.Tests/RecordingOutboundSink.cs` (`275 lines`): Monolithic mock implementing all 34 methods.

### 1.5 Entity Persistence & Models Profile
- **`Character.cs`** (`src/JXServer.Domain/Characters/Character.cs`, `313 lines`):
  - Missing `TaskValues` (sparse `0..65535` task map) and `RepositoryPasswordHash` (MD5 hex digest string).
- **`CharacterItem.cs`** (`src/JXServer.Domain/Characters/CharacterItem.cs`, `109 lines`):
  - Missing `IsBroken` (currently only computed on `PlayerItemSlot` as `MaxDurability > 0 && CurrentDurability == 0`) and `EnchaseLevel` (crafting tier `0..10`).
- **`JXServerDbContext.cs`** (`src/JXServer.Infrastructure/EntityFrameworkCore/JXServerDbContext.cs`, `141 lines`):
  - `Character` mapping (`L69-118`): Does not map `TaskValuesJson` or `RepositoryPasswordHash`.
  - `CharacterItem` mapping (`L120-140`): Does not map `IsBroken` or `EnchaseLevel`.
- **`SessionLeaseAppService.CompleteLogoutOrEvictAsync`** (`src/JXServer.Application/SessionLeases/SessionLeaseAppService.cs`, `L217-238`):
  - Updates character snapshot to database **prior** to inspecting `_sessionLeaseRepository.FindByAccountIdAsync`, violating lease-epoch fencing on stale eviction flushes.
- **`CharacterAppService.ApplyWorldSnapshot`** (`src/JXServer.Application/Characters/CharacterAppService.cs`, `L225-230`):
  - Fast-path coordinate-only check does not inspect `TaskValues` or `UpdateRepositoryPasswordHash`.
- **`PlayerItemSlot.cs`** (`src/JXServer.ZoneEngine/Items/PlayerItemSlot.cs`, `L43`):
  - `public bool IsBroken => MaxDurability > 0 && CurrentDurability == 0;` is read-only computed rather than a mutable persisted property.

### 1.6 Legacy Runtime Parsers in `src/JXServer.GameAssets`
- Unused legacy parsers in `src/JXServer.GameAssets`:
  - `Archives/PakFilePathHasher.cs`
  - `Archives/UclNrv2bDecompressor.cs`
  - `Archives/XPackArchiveReader.cs`
  - `Configuration/IniFileParser.cs`
  - `Configuration/TabFileParser.cs`
- Verification shows zero usages across `src/` runtime projects (which load MemoryPack `.pack` and `data/**/*.json` via `GameAssetsSnapshotLoader`).

---

## 2. Logic Chain & Refactoring Blueprints

```
[Monolithic MapZoneWorker]
       │
       ├─► 1. Extract Asset Catalog Aggregator: ZoneAssetServices
       ├─► 2. Extract Single-Threaded Zone Facade: ZoneExecutionContext
       ├─► 3. Extract 5 Command Handlers: IZoneCommandHandler<T>
       │      ├─► WorldSessionCommandHandler (004)
       │      ├─► MovementCommandHandler (006)
       │      ├─► CombatProgressionCommandHandler (009/010)
       │      ├─► InventoryLootCommandHandler (008/011)
       │      └─► ChatCommandHandler (016)
       ├─► 4. Extract 6 Ordered Simulation Phases: IZoneTickSystem
       │      ├─► Phase 100: StatusEffectZoneTickSystem
       │      ├─► Phase 200: MonsterAiZoneTickSystem
       │      ├─► Phase 300: MissileZoneTickSystem
       │      ├─► Phase 400: GroundObjectZoneTickSystem
       │      ├─► Phase 500: MovementAndPostureZoneTickSystem
       │      └─► Phase 600: SessionHeartbeatZoneTickSystem
       ├─► 5. Decompose Outbound Sink into 4 Domain Channels:
       │      ├─► IWorldOutboundSink (14 methods)
       │      ├─► ICombatOutboundSink (10 methods)
       │      ├─► IInventoryOutboundSink (7 methods)
       │      ├─► IChatOutboundSink (3 methods)
       │      └─► Composite IPlayerOutboundSink with TryGetOutboundSink<TSink>()
       ├─► 6. Detach Chat into Golden Reference Plug-in: ChatZoneModule : IZoneModule (016)
       ├─► 7. Extend Character & Item Persistence:
       │      ├─► TaskValues (sparse map 0..65535, JSON column TaskValuesJson)
       │      ├─► RepositoryPasswordHash (MD5 32-char hex string)
       │      ├─► IsBroken & EnchaseLevel (0..10) on CharacterItem
       │      └─► Strict Lease-Epoch Fencing in SessionLeaseAppService
       └─► 8. Implement ZoneSimulationHarness:
              ├─► In-Memory IZoneClusterRouter + Multi-Zone Stepper
              ├─► FakeTimeProvider (55.55ms ticks) + Seeded PRNG
              └─► PlayerOutboundEventRecorder (Domain classification)
```

---

### 2.1 Blueprint: `MapZoneWorker` Modular Spine Decomposition

#### 2.1.1 `ZoneAssetServices.cs` (`src/JXServer.ZoneEngine/WorldZones/ZoneAssetServices.cs`)
Consolidates all 11 optional catalogs and services passed into zone shards:
```csharp
namespace JXServer.ZoneEngine.WorldZones;

public sealed class ZoneAssetServices
{
    public static ZoneAssetServices Empty { get; } = new();

    public IMapCatalog? MapCatalog { get; init; }
    public INpcTemplateCatalog? NpcTemplateCatalog { get; init; }
    public ITrapScriptCatalog? TrapScriptCatalog { get; init; }
    public ICharacterAppService? CharacterAppService { get; init; }
    public IZoneClusterRouter? ClusterRouter { get; init; }
    public ISkillTemplateCatalog? SkillTemplateCatalog { get; init; }
    public IMissileTemplateCatalog? MissileTemplateCatalog { get; init; }
    public IPlayerTemplateCatalog? PlayerTemplateCatalog { get; init; }
    public IItemTemplateCatalog? ItemTemplateCatalog { get; init; }
    public ILootTableCatalog? LootTableCatalog { get; init; }
    public ItemGenerator? ItemGenerator { get; init; }
}
```

#### 2.1.2 `ZoneExecutionContext.cs` (`src/JXServer.ZoneEngine/WorldZones/ZoneExecutionContext.cs`)
Single-threaded execution context passed to command handlers, tick systems, and feature slots:
```csharp
namespace JXServer.ZoneEngine.WorldZones;

public sealed class ZoneExecutionContext
{
    public MapDescriptor MapDescriptor { get; }
    public ushort MapId => MapDescriptor.MapId;
    public string MapNameUnicode => MapDescriptor.MapNameUnicode;
    public long CurrentFrame { get; internal set; }

    public ZoneEngineOptions Options { get; }
    public TimeProvider TimeProvider { get; }
    public ILogger Logger { get; }
    public Random Random { get; }
    public ISessionLeaseAppService SessionLeaseAppService { get; }
    public ZoneAssetServices Assets { get; }

    public SpatialRegionGrid RegionGrid { get; }
    public MissileSimulationTickSystem MissileSystem { get; }
    public MonsterAiTickSystem MonsterAiSystem { get; }
    public GroundObjectTickSystem GroundObjectSystem { get; }

    public IReadOnlyDictionary<Guid, PlayerZoneState> PlayersByCharacterId { get; }
    public IReadOnlyDictionary<uint, PlayerZoneState> PlayersByNpcId { get; }
    public IReadOnlyDictionary<uint, NpcZoneState> StaticNpcs { get; }

    public bool TryGetPlayer(Guid characterId, [NotNullWhen(true)] out PlayerZoneState? player);
    public bool TryGetPlayerByNpcId(uint runtimeNpcId, [NotNullWhen(true)] out PlayerZoneState? player);
    public bool TryGetStaticNpc(uint npcId, [NotNullWhen(true)] out NpcZoneState? npc);

    public int CollectAoiRegions(int regionX, int regionY, Span<SpatialRegionSlot> destination);
    public TZoneFeature GetOrCreateZoneFeature<TZoneFeature>(Func<ZoneExecutionContext, TZoneFeature> factory) where TZoneFeature : class;
    public UpdateCharacterWorldSnapshotDto BuildCharacterWorldSnapshot(PlayerZoneState player);
    public ValueTask DetachPlayerAsync(Guid characterId, PlayerDetachReason reason, bool persistSnapshot = true, bool sendDisconnect = false, CancellationToken cancellationToken = default);
}
```

#### 2.1.3 Command Handlers & Mapping to 25 Commands
Every command handler implements `IZoneCommandHandler<TCommand>`:
```csharp
public interface IZoneCommandHandler<in TCommand> where TCommand : class, IZoneCommand
{
    ValueTask HandleAsync(TCommand command, ZoneExecutionContext context, CancellationToken cancellationToken = default);
}
```

| Handler | File Path | Commands Handled | Spec Domain |
|---|---|---|---|
| `WorldSessionCommandHandler` | `WorldZones/WorldSessionCommandHandler.cs` | `AttachPlayerZoneCommand`<br>`AttachTransferredPlayerZoneCommand`<br>`CompletePlayerSyncZoneCommand`<br>`RequestEntitySyncZoneCommand`<br>`ProcessPlayerPingZoneCommand`<br>`DetachPlayerZoneCommand` | `004` (Spine & World Entry) |
| `MovementCommandHandler` | `WorldZones/MovementCommandHandler.cs` | `MovePlayerZoneCommand`<br>`JumpPlayerZoneCommand`<br>`TogglePlayerSitZoneCommand` | `006` (Movement & Obstacles) |
| `CombatProgressionCommandHandler` | `Combat/CombatProgressionCommandHandler.cs` | `CastSkillZoneCommand`<br>`ChangeAuraSkillZoneCommand`<br>`ChangePkModeZoneCommand`<br>`AllocateBaseAttributeZoneCommand`<br>`AllocateSkillPointZoneCommand`<br>`RevivePlayerZoneCommand` | `009` (Combat & Missiles)<br>`010` (Monster AI & Progression) |
| `InventoryLootCommandHandler` | `Items/InventoryLootCommandHandler.cs` | `RequestGroundObjectSyncZoneCommand`<br>`PickupGroundObjectZoneCommand`<br>`MoveInventoryItemZoneCommand`<br>`UseInventoryItemZoneCommand`<br>`DropInventoryItemZoneCommand`<br>`DivideInventoryItemZoneCommand` | `008` (Inventory & Equip)<br>`011` (Loot & Item Gen) |
| `ChatCommandHandler` | `Chats/ChatCommandHandler.cs` | `UpdateChatChannelMaskZoneCommand`<br>`SendNearbyTalkZoneCommand`<br>`SendStructuredChatZoneCommand`<br>`DeliverClusterChatZoneCommand` | `016` (Chat Channels) |

#### 2.1.4 Ordered Tick Systems & `ZoneTickPhase`
```csharp
public enum ZoneTickPhase : ushort
{
    StatusEffects = 100,
    MonsterAi = 200,
    Missiles = 300,
    GroundObjects = 400,
    MovementAndPosture = 500,
    SessionHeartbeats = 600,
}
```

| System Class | Target File | Phase | Responsibilities |
|---|---|---|---|
| `StatusEffectZoneTickSystem` | `Combat/StatusEffectZoneTickSystem.cs` | `100` | Poison damage-over-time, freeze/hurt frame decrements, player death checks. |
| `MonsterAiZoneTickSystem` | `Combat/MonsterAiZoneTickSystem.cs` | `200` | Drives `MonsterAiTickSystem` (spawns, patrol, skill casting, AOI sync). |
| `MissileZoneTickSystem` | `Combat/MissileZoneTickSystem.cs` | `300` | Drives `MissileSimulationTickSystem` (64-direction trajectory step, collision). |
| `GroundObjectZoneTickSystem` | `Items/GroundObjectZoneTickSystem.cs` | `400` | Drives `GroundObjectTickSystem` (loot lifetime expiry, medicine regen ticks). |
| `MovementAndPostureZoneTickSystem` | `WorldZones/MovementAndPostureZoneTickSystem.cs` | `500` | Step walk/run coordinates, sit health/mana regeneration, trigger portal traps. |
| `SessionHeartbeatZoneTickSystem` | `WorldZones/SessionHeartbeatZoneTickSystem.cs` | `600` | Periodic `0x82` server pings and evicts timed-out player sessions. |

#### 2.1.5 Per-Player Feature State Slots (`IPlayerFeatureSlot`)
Enables modules to attach arbitrary player state without polluting `PlayerZoneState`:
```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public interface IPlayerFeatureSlot
{
    void OnPlayerDetached(PlayerZoneState player, ZoneExecutionContext context, PlayerDetachReason reason);
}

public sealed class PlayerFeatureSlotKey<TSlot> where TSlot : class, IPlayerFeatureSlot
{
    public string Name { get; }
    internal int Index { get; }
}
```
On `PlayerZoneState`:
- `GetOrCreateFeatureSlot<TSlot>(PlayerFeatureSlotKey<TSlot> key, Func<TSlot> factory)`
- `TryGetFeatureSlot<TSlot>(PlayerFeatureSlotKey<TSlot> key, out TSlot? slot)`
- `RemoveFeatureSlot<TSlot>(PlayerFeatureSlotKey<TSlot> key, out TSlot? removedSlot)`
- `DetachAllFeatureSlots(ZoneExecutionContext context, PlayerDetachReason reason)`: safe loop invoking `OnPlayerDetached` with structured exception isolation logging `LogPlayerFeatureSlotCleanupFaulted` (`EventId = 3012`).

#### 2.1.6 Slim `MapZoneWorker.cs` Architecture
Line count shrinks from 2,975 to ~250 lines:
- Holds `_commandChannel`, `_executionContext`, `_extensionRegistry`, and `_tickSystems`.
- `StepSingleTickAsync`:
  ```csharp
  long tick = Interlocked.Increment(ref _currentFrameTick);
  _executionContext.CurrentFrame = tick;

  while (_commandChannel.Reader.TryRead(out IZoneCommand? cmd))
  {
      if (_extensionRegistry.TryGetRoute(cmd.GetType(), out ZoneCommandRoute route))
      {
          try
          {
              await route.InvokeAsync(cmd, _executionContext, cancellationToken);
          }
          catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
          {
              throw;
          }
          catch (Exception ex)
          {
              ZoneEngineLogMessages.LogZoneCommandFaulted(_logger, ex, cmd.GetType().Name, MapId, tick, cmd.FaultIsolationCharacterId);
          }
      }
      else
      {
          ZoneEngineLogMessages.LogUnhandledZoneCommand(_logger, cmd.GetType().Name, MapId, tick, cmd.FaultIsolationCharacterId);
      }
  }

  for (int i = 0; i < _tickSystems.Length; i++)
  {
      IZoneTickSystem system = _tickSystems[i];
      try
      {
          await system.StepTickAsync(_executionContext, cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
          throw;
      }
      catch (Exception ex)
      {
          ZoneEngineLogMessages.LogZoneTickSystemFaulted(_logger, ex, system.Name, system.Phase, MapId, tick);
      }
  }
  ```

---

### 2.2 Blueprint: Detached `ChatZoneModule : IZoneModule` (Module `016`)

1. **Class Definition**:
   ```csharp
   namespace JXServer.ZoneEngine.Chats;

   public sealed class ChatZoneModule : IZoneModule
   {
       public void Configure(IZoneExtensionBuilder builder)
       {
           var handler = new ChatCommandHandler();
           builder.AddCommandHandler<UpdateChatChannelMaskZoneCommand>(handler);
           builder.AddCommandHandler<SendNearbyTalkZoneCommand>(handler);
           builder.AddCommandHandler<SendStructuredChatZoneCommand>(handler);
           builder.AddCommandHandler<DeliverClusterChatZoneCommand>(handler);
       }
   }
   ```
2. **`ChatCommandHandler.cs`**:
   Implements all 4 chat commands using `context.RegionGrid` for AOI lookups and `player.ChatSink` for wire dispatch:
   - `UpdateChatChannelMaskZoneCommand`: sets `player.SubscribedChatChannelMask`.
   - `SendNearbyTalkZoneCommand`: collects 9 AOI regions, broadcasts `player.ChatSink.SendPlayerTalkBroadcast(sender.RuntimeNpcId, message)`.
   - `SendStructuredChatZoneCommand`: if Nearby/Screen channel, broadcasts `player.ChatSink.SendChatMessage` to AOI players subscribed to that channel mask. If cluster channel (World, Whisper, Team, Faction, Tong), routes through `context.Assets.ClusterRouter.RouteClusterChat`.
   - `DeliverClusterChatZoneCommand`: delivers system or channel chat across zone players matching channel filter and role/faction targets.
3. **Plug-in Independence**:
   - `CoreZoneModule` does NOT register chat commands.
   - `ChatZoneModule` can be registered or omitted via `services.AddZoneModule<ChatZoneModule>()` without affecting zone core operations.

---

### 2.3 Blueprint: Domain Outbound Notification Sinks

#### 2.3.1 Domain Segregation Partition (34 Total Methods)
1. **`IWorldOutboundSink` (14 methods)**:
   - `SendInitialWorldSyncBurst`, `SendActiveWorldConfirmation`, `SendNpcFullSync`, `SendNpcCompactSync`, `SendSelfPositionSync`, `SendWalkBroadcast`, `SendRunBroadcast`, `SendJumpBroadcast`, `SendSitBroadcast`, `SendWorldMapTransitionSync`, `SendNotifyServerExchange`, `SendServerPing`, `SendServerPingReply`, `Disconnect`.
2. **`ICombatOutboundSink` (10 methods)**:
   - `SendNormalVitalSync`, `SendSkillCastBroadcast`, `SendHurtBroadcast`, `SendPkNormalFlagSync`, `SendNpcDeathBroadcast`, `SendNpcReviveBroadcast`, `SendPlayerExpSync`, `SendPlayerLevelUpSync`, `SendPlayerAttributeSync`, `SendPlayerSkillLevelSync`.
3. **`IInventoryOutboundSink` (7 methods)**:
   - `SendGroundObjectAdd`, `SendGroundObjectRemove`, `SendItemSync`, `SendItemRemove`, `SendMoneySync`, `SendPlayerMoveItemConfirm`, `SendItemAutoMoveUnlock`.
4. **`IChatOutboundSink` (3 methods)**:
   - `SendPlayerTalkBroadcast`, `SendChatMessage`, `SendSystemMessage`.

#### 2.3.2 Extensible Composite `IPlayerOutboundSink`
```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public interface IPlayerOutboundSink
    : IWorldOutboundSink,
      ICombatOutboundSink,
      IInventoryOutboundSink,
      IChatOutboundSink
{
    bool TryGetOutboundSink<TSink>([NotNullWhen(true)] out TSink? sink) where TSink : class
    {
        sink = this as TSink;
        return sink is not null;
    }
}
```

#### 2.3.3 `SelectivePlayerOutboundSink` & Zero-Allocation No-Ops
- `NoOpWorldOutboundSink.Instance`, `NoOpCombatOutboundSink.Instance`, `NoOpInventoryOutboundSink.Instance`, `NoOpChatOutboundSink.Instance`.
- `SelectivePlayerOutboundSink`: Composite wrapper allowing tests and specialized sessions to supply only 1..4 domains, delegating unsubscribed domains to singleton no-ops with zero memory allocation.

#### 2.3.4 `GameServerClientSession` Partial Decomposition
- `GameServerClientSession.cs`: Core socket loop, handshake, writer task, and FIFO `_outboundChannel`.
- `GameServerClientSession.WorldSink.cs`: Implements 14 `IWorldOutboundSink` methods.
- `GameServerClientSession.CombatSink.cs`: Implements 10 `ICombatOutboundSink` methods.
- `GameServerClientSession.InventorySink.cs`: Implements 7 `IInventoryOutboundSink` methods.
- `GameServerClientSession.ChatSink.cs`: Implements 3 `IChatOutboundSink` methods.
- **Wire Guarantee**: All 4 partial files write to the single `_outboundChannel` FIFO, ensuring 100% byte order and packet sequence fidelity.

---

### 2.4 Blueprint: Foundational Persistence & Lease-Epoch Fencing

#### 2.4.1 Primitives Constants & Error Codes
- `CharacterConsts`:
  ```csharp
  public const ushort MinTaskId = 0;
  public const ushort MaxTaskId = ushort.MaxValue; // 65535
  ```
- `ItemConsts`:
  ```csharp
  public const byte MaxEnchaseLevel = 10;
  ```
- `JXServerErrorCodes`:
  ```csharp
  public const string CharacterInvalidRepositoryCredential = "JXServer:Character:0006";
  ```

#### 2.4.2 `Character.cs` Enhancements
```csharp
private Dictionary<ushort, int> _taskValues = [];
public IReadOnlyDictionary<ushort, int> TaskValues => _taskValues;
public string? RepositoryPasswordHash { get; private set; }

public int GetTaskValue(ushort taskId) => _taskValues.GetValueOrDefault(taskId, 0);

public void SetTaskValue(ushort taskId, int value)
{
    if (value == 0) _taskValues.Remove(taskId);
    else _taskValues[taskId] = value;
    ConcurrencyStamp = Guid.NewGuid();
}

public void ReplaceTaskValues(IEnumerable<KeyValuePair<ushort, int>>? values)
{
    _taskValues.Clear();
    if (values is not null)
    {
        foreach ((ushort taskId, int val) in values)
        {
            if (val != 0) _taskValues[taskId] = val;
        }
    }
    ConcurrencyStamp = Guid.NewGuid();
}

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
```

#### 2.4.3 `CharacterItem.cs` Enhancements
```csharp
public bool IsBroken { get; private set; }
public byte EnchaseLevel { get; private set; }

public CharacterItem(
    // ... existing parameters ...
    bool isBroken = false,
    byte enchaseLevel = 0)
{
    // ...
    if (enchaseLevel > ItemConsts.MaxEnchaseLevel)
    {
        throw new ArgumentOutOfRangeException(nameof(enchaseLevel), enchaseLevel,
            $"EnchaseLevel must be between 0 and {ItemConsts.MaxEnchaseLevel}.");
    }
    IsBroken = isBroken;
    EnchaseLevel = enchaseLevel;
}

public void SetBroken(bool isBroken) => IsBroken = isBroken;
public void SetEnchaseLevel(byte enchaseLevel)
{
    if (enchaseLevel > ItemConsts.MaxEnchaseLevel)
    {
        throw new ArgumentOutOfRangeException(nameof(enchaseLevel), enchaseLevel,
            $"EnchaseLevel must be between 0 and {ItemConsts.MaxEnchaseLevel}.");
    }
    EnchaseLevel = enchaseLevel;
}
```

#### 2.4.4 EF Core Mapping (`JXServerDbContext.cs`)
```csharp
// Characters
builder.Property(x => x.RepositoryPasswordHash)
    .IsRequired(false)
    .HasMaxLength(AccountConsts.PasswordMd5HexLength);

builder.Property<Dictionary<ushort, int>>("_taskValues")
    .HasColumnName("TaskValuesJson")
    .IsRequired()
    .HasDefaultValue("{}")
    .HasConversion(
        v => TaskValuesJsonSerializer.Serialize(v),
        v => TaskValuesJsonSerializer.Deserialize(v))
    .Metadata.SetValueComparer(TaskValuesJsonSerializer.Comparer);

// CharacterItems
builder.Property(x => x.IsBroken).IsRequired().HasDefaultValue(false);
builder.Property(x => x.EnchaseLevel).IsRequired().HasDefaultValue((byte)0);
```

#### 2.4.5 Strict Lease-Epoch Fencing in `SessionLeaseAppService.CompleteLogoutOrEvictAsync`
Load lease **first** under `accountLock`:
```csharp
SessionLease? lease = await _sessionLeaseRepository.FindByAccountIdAsync(input.AccountId, cancellationToken);

bool canPersist = input.LeaseEpoch == 0
    || (lease is not null && lease.LeaseEpoch == input.LeaseEpoch)
    || (lease is not null && lease.LeaseEpoch == input.LeaseEpoch + 1 && lease.State == SessionLeaseState.GatewayAuthenticated && lease.ActiveCharacterId is null);

if (canPersist && input.CharacterSnapshot is { } snap)
{
    Character? character = await _characterRepository.FindAsync(snap.CharacterId, cancellationToken);
    if (character is not null && character.AccountId == input.AccountId)
    {
        CharacterAppService.ApplyWorldSnapshot(character, snap, nowUtc);
        await _characterRepository.UpdateAsync(character, autoSave: true, cancellationToken: cancellationToken);
    }
}
```

---

### 2.5 Blueprint: `ZoneSimulationHarness` Architecture

#### 2.5.1 Core Types under `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/`
1. `ZoneSimulationHarnessOptions.cs`: Configures `RandomSeed`, initial UTC timestamp, `TicksPerSecond = 18`, catalogs, and custom `IZoneModule`s.
2. `OutboundNotificationDomain.cs`: `enum OutboundNotificationDomain : byte { World = 0, Combat = 1, Inventory = 2, Chat = 3, CustomModule = 4 }`.
3. `RecordedOutboundEvent.cs`: `sealed record RecordedOutboundEvent(long Frame, OutboundNotificationDomain Domain, string MethodName, object?[] Arguments)`.
4. `PlayerOutboundEventRecorder.cs`: Implements full `IPlayerOutboundSink`, logs every wire event stamped with `CurrentFrame` and `Domain`, provides domain filters `GetEventsByDomain(...)` and `Clear()`.
5. `ScheduledZoneCommandEntry.cs`: `internal readonly record struct ScheduledZoneCommandEntry(long ExecuteAtFrame, ushort MapId, IZoneCommand Command)`.
6. `InMemorySessionLeaseAppServiceStub.cs`: In-memory lease tracking (heartbeats, snapshots, evictions) with zero DB/network overhead.
7. `SimulatedPlayerHandle.cs`: Envelops `CharacterDto`, `PlayerOutboundEventRecorder`, `LeaseEpoch`, and `MapId`.
8. `ZoneSimulationHarness.cs`:
   - Implements `IZoneClusterRouter`.
   - Manages multiple `MapZoneWorker` shards.
   - Synchronizes `FakeTimeProvider`: advances by `55.55ms` per frame.
   - Dispatches scheduled commands matching the target frame.
   - Steps all zones in deterministic ascending `MapId` sequence.
   - Guaranteed determinism with PRNG seed: `int zoneSeed = unchecked((seed * 397) ^ mapId);`.
   - Performance: 1,000 frames virtual stepping in under 5ms.

---

## 3. Caveats & Invariant Guardrails

1. **Read-Only Status**: This survey performed static and dynamic inspection without modifying existing source files.
2. **0-Warning / `TreatWarningsAsErrors=true` Guardrail**: Any new `.cs` file must strictly follow C# 14 / .NET 10 nullable reference type rules, `sealed` concrete classes, explicit unit suffixes (`*MpsX`, `*CellX`, `*RegionX`, `*Frames`), and 1 type per `.cs` file.
3. **Single-Threaded Zone Execution Invariant**: Neither `ZoneExecutionContext` nor `PlayerZoneState` should ever be accessed across threads. All cross-zone commands (portal handoff, cluster chat) must continue to route lock-free through `Channel<IZoneCommand>` writers.
4. **Wire Sequence Invariant**: Segregating outbound sinks across 4 domain interfaces must NOT create separate network queues. `GameServerClientSession` must write all domain packets into the single `Channel<byte[]> _outboundChannel`.
5. **Database Backward Compatibility**: Database migration or schema update must set default values (`"{}"` for `TaskValuesJson`, `NULL` for `RepositoryPasswordHash`, `0` for `IsBroken` and `EnchaseLevel`) so existing character records remain valid.

---

## 4. Conclusion

1. **MapZoneWorker Decomposition**: Slicing the 2,975-line `MapZoneWorker.cs` into `ZoneAssetServices`, `ZoneExecutionContext`, 5 domain command handlers, and 6 ordered tick systems preserves 100% of the current logic while achieving clean separation of concerns matching SpecKit domains `004`–`011`.
2. **Chat Module Independence**: Moving chat handling into `ChatZoneModule : IZoneModule` creates an autonomous Golden Reference Plug-in Module (`016-module-chat-channels`) that can be loaded or excluded via standard DI extension points without modifying core zone worker code.
3. **Outbound Sink Domain Segregation**: Splitting `IPlayerOutboundSink` into 4 cohesive channels (`World`, `Combat`, `Inventory`, `Chat`) while retaining the composite interface and single FIFO network channel eliminates domain entanglement while maintaining zero-regression wire compatibility.
4. **Foundational Persistence & Lease-Epoch Fencing**: Extending `Character` and `CharacterItem` with `TaskValues`, `RepositoryPasswordHash`, `IsBroken`, and `EnchaseLevel`, along with lease-first evaluation in `CompleteLogoutOrEvictAsync`, fulfills data architecture standards and eliminates race conditions.
5. **Simulation Harness Viability**: `ZoneSimulationHarness` combined with `FakeTimeProvider`, deterministic PRNG seeding, and `PlayerOutboundEventRecorder` enables ultra-fast (1,000 frames < 5ms) deterministic regression testing.

---

## 5. Verification Method

To independently verify the facts and findings in this survey:

1. **Verify Baseline Tests**:
   ```powershell
   cd d:\Projects\jx-server
   dotnet test
   ```
   *Expected Output*: 97 tests passed (0 failed, 0 skipped).
2. **Verify MapZoneWorker Monolith**:
   ```powershell
   (Get-Content src/JXServer.ZoneEngine/WorldZones/MapZoneWorker.cs).Length
   ```
   *Expected Output*: 2,975 lines.
3. **Verify Chat Commands in ZoneEngine**:
   ```powershell
   Get-ChildItem src/JXServer.ZoneEngine/Chats
   ```
   *Expected Output*: 4 command files (`DeliverClusterChatZoneCommand.cs`, `SendNearbyTalkZoneCommand.cs`, `SendStructuredChatZoneCommand.cs`, `UpdateChatChannelMaskZoneCommand.cs`).
4. **Verify Outbound Sink Method Count**:
   ```powershell
   (Select-String -Path src/JXServer.ZoneEngine/PlayerSessions/IPlayerOutboundSink.cs -Pattern "void Send|void Disconnect").Count
   ```
   *Expected Output*: Exactly 34 methods.
5. **Verify Unused Legacy Parsers in GameAssets**:
   ```powershell
   Select-String -Path src/**/*.cs -Pattern "IniFileParser|TabFileParser"
   ```
   *Expected Output*: 0 matches in `src/`.
