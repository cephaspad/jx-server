# Task Assignment: Modular Spine, Domain Sinks, Persistence & Simulation Harness (Milestone M2)

Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\worker_m2\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Architecture blueprint path: d:\Projects\jx-server\.agents\teamwork\explorer_zone_survey\handoff.md
Modular spine contracts:
- `specs/_archive_m1_m4/004-zone-modular-spine/contracts/zone-modular-spine-and-harness.md`
- `specs/_archive_m1_m4/004-zone-modular-spine/contracts/domain-outbound-sinks.md`
- `specs/_archive_m1_m4/004-zone-modular-spine/contracts/character-persistence-contracts.md`

## Objective
Implement Milestone M2 according to the contracts and blueprint:

### 1. Modular Spine Core Types (`src/JXServer.ZoneEngine/WorldZones/` and `PlayerSessions/`)
- `IZoneModule.cs`: `void ConfigureExtensions(IZoneExtensionBuilder builder)`
- `IZoneCommandHandler.cs`: `ValueTask HandleAsync(TCommand command, ZoneExecutionContext context, CancellationToken cancellationToken)`
- `IZoneTickSystem.cs`: `ZoneTickPhase Phase { get; }`, `ValueTask StepTickAsync(ZoneExecutionContext context, CancellationToken cancellationToken)`
- `ZoneTickPhase.cs`: enum with `StatusEffects = 100`, `MonsterAi = 200`, `Missiles = 300`, `GroundObjects = 400`, `MovementAndPosture = 500`, `SessionHeartbeats = 600`
- `ZoneAssetServices.cs`: encapsulates all catalogs and template providers
- `ZoneExecutionContext.cs`: facade for zone state, region grid, player lookups, command scheduling, and asset services
- `IZoneExtensionBuilder.cs` & `ZoneExtensionRegistry.cs`: type-safe registration of handlers, tick systems, and player feature slots
- `PlayerFeatureSlotKey.cs` & `IPlayerFeatureSlot.cs`: typed feature slot attachments on `PlayerZoneState` with detachment lifecycle hooks
- Fault containment: handlers and tick systems wrapped in try/catch logging EventIds 3009..3012.

### 2. Domain Outbound Notification Sinks
- Split `IPlayerOutboundSink.cs` into:
  - `IWorldOutboundSink.cs` (14 methods: world entry, sync bursts, vitals, postures, presence)
  - `ICombatOutboundSink.cs` (10 methods: skills, damage, hurts, status effects)
  - `IInventoryOutboundSink.cs` (7 methods: container sync, item move, equip state, durability)
  - `IChatOutboundSink.cs` (3 methods: local speech, structured chat, system notices)
  - Composite `IPlayerOutboundSink` inheriting from all 4 domain sinks and offering `bool TryGetOutboundSink<TSink>(out TSink? sink)`
- Decompose `GameServerClientSession.cs` into partial classes:
  - `GameServerClientSession.WorldSink.cs`
  - `GameServerClientSession.CombatSink.cs`
  - `GameServerClientSession.InventorySink.cs`
  - `GameServerClientSession.ChatSink.cs`
  All writing to the existing single `_outboundChannel` to preserve wire FIFO ordering.
- Update `RecordingOutboundSink` in `tests/JXServer.ZoneEngine.Tests/` to maintain compatibility with existing tests.

### 3. Foundational Persistence Extensions & Lease Fencing
- `src/JXServer.Domain/Characters/Character.cs`:
  - Add `TaskValues` (sparse `Dictionary<int, int>` 0..65535, setting 0 removes key)
  - Add `RepositoryPasswordHash` (MD5 hex digest string, validation for 32 hex chars)
- `src/JXServer.Domain/Characters/CharacterItem.cs`:
  - Add `IsBroken` (mutable bool)
  - Add `EnchaseLevel` (int 0..10)
- `src/JXServer.Infrastructure/EntityFrameworkCore/JXServerDbContext.cs`:
  - Map `TaskValuesJson` column with JSON conversion for `TaskValues`
  - Map `RepositoryPasswordHash` (max 32 chars)
  - Map `IsBroken` and `EnchaseLevel` on `CharacterItem`
- `src/JXServer.Application/SessionLeases/SessionLeaseAppService.cs`:
  - In `CompleteLogoutOrEvictAsync`, check lease epoch BEFORE saving world snapshot to prevent stale worker writes overwriting a superseding lease.

### 4. Deterministic `ZoneSimulationHarness`
- In `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/`:
  - `ZoneSimulationHarness.cs`: multi-zone in-memory runner with `FakeTimeProvider` (55.55ms ticks) and fixed PRNG seed formula.
  - `PlayerOutboundEventRecorder.cs`: records events categorized by domain sink.
  - `SimulatedPlayerHandle.cs`: test client handle for simulated players.
  - `ZoneSimulationHarnessTests.cs`:
    - Test running 1,000 virtual frames deterministically in under 5ms.
    - Test domain outbound event recording.
    - Test fault isolation: a failing command/tick isolates to the throwing session, logs EventId 3010/3011, and does not crash the zone.

### 5. Verification
- `dotnet build JXServer.slnx`: 0 warnings, `TreatWarningsAsErrors=true`.
- `dotnet test JXServer.slnx`: all 97 existing tests pass + all new harness tests pass.
- Produce `handoff.md` with complete evidence.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.


## 2026-11-11T07:16:06Z
You are the Modular Spine, Domain Sinks, Persistence & Harness Specialist for Milestone M2.
Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\worker_m2\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Architecture blueprint path: d:\Projects\jx-server\.agents\teamwork\explorer_zone_survey\handoff.md
Task assignment: d:\Projects\jx-server\.agents\teamwork\worker_m2\DISPATCH.md
Modular spine contracts:
- `specs/_archive_m1_m4/004-zone-modular-spine/contracts/zone-modular-spine-and-harness.md`
- `specs/_archive_m1_m4/004-zone-modular-spine/contracts/domain-outbound-sinks.md`
- `specs/_archive_m1_m4/004-zone-modular-spine/contracts/character-persistence-contracts.md`
