# Contract 01: Zone Modular Spine, Per-Player Feature Slots & Simulation Harness

**Feature**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)  
**Requirements Covered**: `FR-001`–`FR-008`, `SC-001`–`SC-004`  
**Target Projects**: `src/JXServer.ZoneEngine`, `tests/JXServer.ZoneEngine.Tests`  

---

## 1. Modular Command Routing (`IZoneCommandHandler<TCommand>`)

All commands dispatched to a zone implement `IZoneCommand`:

```csharp
namespace JXServer.ZoneEngine.WorldZones;

public interface IZoneCommand
{
    Guid? FaultIsolationCharacterId => null;
}

public interface IZoneCommandHandler<in TCommand> where TCommand : class, IZoneCommand
{
    ValueTask HandleAsync(TCommand command, ZoneExecutionContext context, CancellationToken cancellationToken);
}
```

- Handlers are executed in single-threaded lock-free fashion inside `MapZoneWorker`.
- Routing is resolved via `ZoneExtensionRegistry.TryGetRoute(Type commandType, out ZoneCommandRoute route)`.
- Invocation delegates are pre-compiled into `ZoneCommandRoute` using zero-boxing `ValueTask` invocation.

---

## 2. Phased Tick Pipeline (`IZoneTickSystem` & `ZoneTickPhase`)

The 18-Hz simulation loop executes tick subsystems deterministically in phase order:

```csharp
namespace JXServer.ZoneEngine.WorldZones;

public enum ZoneTickPhase : ushort
{
    StatusEffects = 100,
    MonsterAi = 200,
    Missiles = 300,
    GroundObjects = 400,
    MovementAndPosture = 500,
    SessionHeartbeats = 600
}

public interface IZoneTickSystem
{
    string Name { get; }
    ZoneTickPhase Phase { get; }
    int SubOrder => 0;
    ValueTask TickAsync(ZoneExecutionContext context, CancellationToken cancellationToken);
}
```

---

## 3. Per-Player Feature State Slots (`PlayerFeatureSlotKey<TSlot>`)

Modular gameplay features attach isolated state to `PlayerZoneState` without adding fields to the core player record:

```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

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

- Slot access on `PlayerZoneState` uses array indexing ($O(1)$ constant time).
- When a player leaves the zone (logout, timeout, or map transition `PlayerDetachReason.MapTransferred`), `DetachAllFeatureSlotsAsync` guarantees deterministic slot cleanup.

---

## 4. Fault Containment Guarantee (`FR-005`, `SC-002`)

Within `MapZoneWorker`:
1. Every command execution is protected by a per-session `try/catch` block.
2. If an unhandled exception is thrown by a module handler:
   - Diagnostic event `3009` or `3010` is logged with player ID and command type.
   - The fault is contained to the offending session (evicting or detaching that player if necessary).
   - Sibling player sessions, monster AI, projectiles, and subsequent frames continue uninterrupted.

---

## 5. High-Speed Deterministic Simulation Test Harness (`ZoneSimulationHarness`)

```csharp
namespace JXServer.ZoneEngine.Tests.SimulationHarnesses;

public sealed class ZoneSimulationHarness : IAsyncDisposable
{
    public FakeTimeProvider TimeProvider { get; }
    public PlayerOutboundEventRecorder EventRecorder { get; }
    public long CurrentFrame { get; }

    public static Task<ZoneSimulationHarness> CreateAsync(ushort mapId, int? randomSeed = null);
    public SimulatedPlayerHandle SpawnPlayer(Guid characterId, string roleName, int mpsX, int mpsY);
    public void ScheduleCommand(long executeAtFrame, ushort mapId, IZoneCommand command);
    public ValueTask StepFramesAsync(int frameCount);
}
```

- Advances simulation frames using `FakeTimeProvider` and pre-seeded PRNG.
- Capable of stepping `1,000` virtual frames in under `5` milliseconds.
- Records outbound events with frame stamps and domain classifications for assertion.
