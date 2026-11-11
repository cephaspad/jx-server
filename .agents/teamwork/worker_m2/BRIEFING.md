# BRIEFING — 2026-11-11T08:07:00Z

## Mission
Implement Milestone M2: Modular Spine core types, domain-segregated outbound notification sinks, persistence extensions with lease fencing, and deterministic ZoneSimulationHarness with zero regression.

## 🔒 My Identity
- Archetype: implementer, qa, specialist
- Roles: implementer, qa, specialist
- Working directory: d:\Projects\jx-server
- State folder: d:\Projects\jx-server\.agents\teamwork\worker_m2\
- Original parent: d2392fd8-ba50-46c2-b953-b7059df99b64
- Milestone: M2 (Modular Spine, Domain Sinks, Persistence & Harness)

## 🔒 Key Constraints
- TreatWarningsAsErrors=true (0 warnings).
- Strictly one type per .cs file.
- Sealed concrete classes.
- Pluralized domain folders.
- Unit-explicit naming suffixes (*MpsX, *CellX, *RegionX, *Frames).
- Zero per-tick heap allocations on 18-Hz hot path.
- 100% zero-regression on all 97 existing tests.
- 1,000 virtual frames under 5ms in ZoneSimulationHarness.
- Genuine implementations only - no hardcoding or dummy implementations.

## Current Parent
- Conversation ID: d2392fd8-ba50-46c2-b953-b7059df99b64
- Updated: 2026-11-11T08:07:00Z

## Task Summary
- **What to build**:
  1. Modular Spine core types (`IZoneModule`, `IZoneCommandHandler<T>`, `ZoneTickPhase`, `IZoneTickSystem`, `ZoneAssetServices`, `ZoneExecutionContext`, `IZoneExtensionBuilder`, `ZoneExtensionRegistry`, `PlayerFeatureSlotKey<T>`, `IPlayerFeatureSlot`, fault isolation logging EventIds 3009..3012).
  2. Domain outbound notification sinks (`IWorldOutboundSink`, `ICombatOutboundSink`, `IInventoryOutboundSink`, `IChatOutboundSink`, composite `IPlayerOutboundSink`, `NoOp*Sink`, `SelectivePlayerOutboundSink`, decomposed `GameServerClientSession` into partial classes).
  3. Foundational persistence & lease fencing (`TaskValues`, `RepositoryPasswordHash`, `IsBroken`, `EnchaseLevel`, EF Core mappings with `TaskValuesJsonSerializer`, `EfCoreCharacterRepository`, and `SessionLeaseAppService` epoch fencing).
  4. Deterministic `ZoneSimulationHarness` (`ZoneSimulationHarness`, `PlayerOutboundEventRecorder`, `SimulatedPlayerHandle`, tests running 1000 frames < 5ms, domain event recording, fault isolation).
- **Success criteria**: 0 build warnings, all 97 existing tests pass + 8 new harness tests pass (105 total passed), 1,000 virtual frames execute rapidly without wall-clock sleep.
- **Interface contracts**: `specs/_archive_m1_m4/004-zone-modular-spine/contracts/`
- **Code layout**: Clean Architecture & DDD per `PROJECT.md`.

## Key Decisions Made
- Decomposed `GameServerClientSession` into 4 partial sink files writing directly to `_outboundChannel`.
- Implemented `TaskValuesJsonSerializer` with deterministic ascending key order and sparse 0-value filtering.
- Implemented `ZoneExtensionRegistry` with immutable `FrozenDictionary` command routing and pre-sorted tick systems array to ensure zero heap allocations during the 18-Hz simulation loop.
- Added seeded PRNG derivation formula `(seed * 397) ^ mapId` to `MapZoneWorker` and sub-systems.
- Implemented `ZoneSimulationHarness` backed by `FakeTimeProvider` and `InMemorySessionLeaseAppServiceStub` for instant multi-zone simulation.

## Artifact Index
- `specs/_archive_m1_m4/004-zone-modular-spine/contracts/` — Specification contracts
- `d:\Projects\jx-server\.agents\teamwork\worker_m2\handoff.md` — Final handoff report

## Change Tracker
- **Files modified/added**: 38 files across Primitives, Domain, Contracts, Infrastructure, Application, ZoneEngine, Adapter.LegacyJX1, and Tests.
- **Build status**: Succeeded with 0 errors and 0 warnings.
- **Pending issues**: None.

## Quality Status
- **Build/test result**: 105 passed, 0 failed, 0 skipped.
- **Lint status**: 0 warnings.
- **Tests added/modified**: 8 new tests in `ZoneSimulationHarnessTests.cs`.

## Loaded Skills
- None
