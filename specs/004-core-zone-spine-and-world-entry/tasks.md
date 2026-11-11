# Tasks: Core Zone Modular Spine, World Entry & Simulation Harness

**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (`[US1]`, `[US2]`, `[US3]`, `[US4]`)
- Every task specifies the exact target file path(s) in `d:\Projects\jx-server`

---

## Phase 1: Zone Modular Extension Spine (`JXServer.ZoneEngine`)

- [X] T001 [P] [US1] Define command marker `IZoneCommand` and typed handler `IZoneCommandHandler<TCommand>` in `src/JXServer.ZoneEngine/WorldZones/IZoneCommand.cs` and `IZoneCommandHandler.cs`
- [X] T002 [P] [US1] Define deterministic pipeline phase enum `ZoneTickPhase` and phased subsystem contract `IZoneTickSystem` in `src/JXServer.ZoneEngine/WorldZones/ZoneTickPhase.cs` and `IZoneTickSystem.cs`
- [X] T003 [P] [US1] Implement per-player feature state slot contracts `PlayerFeatureSlotKey<TSlot>` and `IPlayerFeatureSlot` in `src/JXServer.ZoneEngine/PlayerSessions/`
- [X] T004 [P] [US1] Implement `ZoneExecutionContext`, `ZoneAssetServices`, and route struct `ZoneCommandRoute` in `src/JXServer.ZoneEngine/WorldZones/`
- [X] T005 [P] [US1] Implement module registration builder `ZoneExtensionBuilder` and immutable pre-compiled router `ZoneExtensionRegistry` in `src/JXServer.ZoneEngine/WorldZones/`
- [X] T006 [US1] Implement `CoreZoneModule`, `MovementCommandHandler`, `WorldSessionCommandHandler`, `MovementAndPostureZoneTickSystem`, and `SessionHeartbeatZoneTickSystem` in `src/JXServer.ZoneEngine/WorldZones/`
- [X] T007 [US1] Refactor `MapZoneWorker` to execute single-threaded 18-Hz ticks via `ZoneExtensionRegistry` with per-session fault isolation (`try/catch` wrapping) in `src/JXServer.ZoneEngine/WorldZones/MapZoneWorker.cs`

---

## Phase 2: High-Speed Deterministic Simulation Test Harness

- [X] T008 [P] [US2] Implement immutable event recording models `OutboundNotificationDomain` and `RecordedOutboundEvent` in `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/`
- [X] T009 [P] [US2] Implement domain-tagged outbound sink recorder `PlayerOutboundEventRecorder` in `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/PlayerOutboundEventRecorder.cs`
- [X] T010 [P] [US2] Implement in-memory stub `InMemorySessionLeaseAppServiceStub` in `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/InMemorySessionLeaseAppServiceStub.cs`
- [X] T011 [US2] Implement in-memory virtual time stepper `ZoneSimulationHarness` (stepping `1,000` frames under 5ms with `FakeTimeProvider` and `RandomSeed`) in `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/ZoneSimulationHarness.cs`

---

## Phase 3: Domain-Segregated Outbound Sinks

- [X] T012 [P] [US4] Define domain notification interfaces `IWorldOutboundSink`, `ICombatOutboundSink`, `IInventoryOutboundSink`, and `IChatOutboundSink` in `src/JXServer.ZoneEngine/PlayerSessions/`
- [X] T013 [P] [US4] Implement composite `IPlayerOutboundSink`, `SelectivePlayerOutboundSink`, and singleton `NoOp*OutboundSink` implementations in `src/JXServer.ZoneEngine/PlayerSessions/`
- [X] T014 [P] [US4] Cache non-null domain sinks and feature slots on `PlayerZoneState` in `src/JXServer.ZoneEngine/PlayerSessions/PlayerZoneState.cs`
- [X] T015 [US4] Decompose `GameServerClientSession` into 5 partial class files (`GameServerClientSession.cs`, `GameServerClientSession.WorldSink.cs`, `GameServerClientSession.CombatSink.cs`, `GameServerClientSession.InventorySink.cs`, `GameServerClientSession.ChatSink.cs`) sharing `_outboundChannel` in `src/JXServer.Adapter.LegacyJX1/GameServer/`

---

## Phase 4: Foundational Character & Item Persistence

- [X] T016 [P] [US4] Add task bounds constants (`MinTaskId`, `MaxTaskId`) in `src/JXServer.Primitives/Characters/CharacterConsts.cs`
- [X] T017 [P] [US4] Add `TaskValues` sparse map and `RepositoryPasswordHash` to `Character` aggregate root in `src/JXServer.Domain/Characters/Character.cs`
- [X] T018 [P] [US4] Add `IsBroken` flag and `EnchaseLevel` to `CharacterItem` entity in `src/JXServer.Domain/Characters/CharacterItem.cs`
- [X] T019 [P] [US4] Implement `TaskValuesJsonSerializer` and configure EF Core mappings in `src/JXServer.Infrastructure/EntityFrameworkCore/JXServerDbContext.cs`
- [X] T020 [US4] Wire foundational persistence properties through `CharacterAppService`, `SessionLeaseAppService`, and `EfCoreCharacterRepository`

---

## Phase 5: GameServer World Entry & Heartbeat Wire Protocol

- [X] T021 [P] [US3] Implement GameServer wire packet structs (`LogicLoginC2SPacket`, `SyncWorldS2CPacket`, `SyncCurPlayerS2CPacket`, `SyncCurPlayerSkillS2CPacket`, `SyncCurPlayerNormalS2CPacket`, `SyncNpcFullS2CPacket`, `SyncNpcMinPlayerS2CPacket`, `DisabledFuncFlagS2CPacket`, `SyncEndS2CPacket`, `SyncEndC2SPacket`, `PingS2CPacket`, `PingReplyC2SPacket`, `PingReplyS2CPacket`, `CloseConnectionC2SPacket`) in `src/JXServer.Adapter.LegacyJX1/Packets/`
- [X] T022 [P] [US3] Implement `LogicLoginPacketHandler`, `SyncEndPacketHandler`, `PingReplyPacketHandler`, and `CloseConnectionPacketHandler` in `src/JXServer.Adapter.LegacyJX1/GameServer/`
- [X] T023 [US3] Implement `GameServerTcpListenerHostedService` (port 6666) in `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerTcpListenerHostedService.cs`

---

## Phase 6: Verification & Simulation Tests

- [X] T024 [P] [US2] Write benchmark tests confirming `ZoneSimulationHarness` steps 1,000 frames under 5ms with deterministic replay in `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/ZoneSimulationHarnessTests.cs`
- [X] T025 [P] [US2] Write adversarial fault-injection and boundary tests in `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/ZoneSimulationHarnessAdversarialTests.cs`
- [X] T026 [P] [US1] Write unit tests verifying modular module registration, route collision rejection, and per-session fault isolation in `tests/JXServer.ZoneEngine.Tests/ModularZoneDeconstructionTests.cs`
- [X] T027 [P] [US4] Write unit tests for item broken durability, repair, and enchase level in `tests/JXServer.ZoneEngine.Tests/Items/CharacterItemUnitTests.cs`
- [X] T028 [P] [US3] Write integration tests for end-to-end TCP world entry handshake and continuous heartbeat synchronization in `tests/JXServer.Integration.Tests/GatewayWorldEntryIntegrationTests.cs`
- [X] T029 [US4] Write integration tests for SQLite persistence round-trip of task values, repository password hashes, and item attributes in `tests/JXServer.Integration.Tests/CharacterItemPersistenceIntegrationTests.cs`
