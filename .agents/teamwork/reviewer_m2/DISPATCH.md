# Task Assignment: Milestone M2 Review

Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\reviewer_m2\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Worker handoff path: d:\Projects\jx-server\.agents\teamwork\worker_m2\handoff.md

## Objective
Independently review Milestone M2 (Modular Spine, Domain Sinks, Persistence & Simulation Harness):
1. Review code changes across `src/` and `tests/`:
   - `IZoneModule`, `IZoneCommandHandler<T>`, `IZoneTickSystem`, `ZoneTickPhase`, `ZoneAssetServices`, `ZoneExecutionContext`, `ZoneExtensionBuilder`, `ZoneExtensionRegistry`, `PlayerFeatureSlotKey<T>`, `IPlayerFeatureSlot`.
   - Domain sinks: `IWorldOutboundSink`, `ICombatOutboundSink`, `IInventoryOutboundSink`, `IChatOutboundSink`, and `GameServerClientSession.*Sink.cs`.
   - Persistence: `Character.TaskValues`, `RepositoryPasswordHash`, `CharacterItem.IsBroken`, `EnchaseLevel`, `TaskValuesJsonSerializer`, `JXServerDbContext`, `SessionLeaseAppService` epoch fencing.
   - Simulation Harness: `ZoneSimulationHarness.cs`, `PlayerOutboundEventRecorder.cs`, `SimulatedPlayerHandle.cs`, `ZoneSimulationHarnessTests.cs`.
2. Verify code quality standards:
   - 1 type per `.cs` file
   - Sealed concrete classes
   - Unit suffix naming conventions
3. Run `dotnet build JXServer.slnx` -> verify 0 warnings (`TreatWarningsAsErrors=true`).
4. Run `dotnet test JXServer.slnx` -> verify all 105 tests pass.
5. Provide your formal verdict (APPROVE or REQUEST_CHANGES) in `d:\Projects\jx-server\.agents\teamwork\reviewer_m2\handoff.md`.


## 2026-11-11T08:08:34Z
You are the Reviewer for Milestone M2 (Modular Spine, Domain Sinks, Persistence & Simulation Harness).
Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\reviewer_m2\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Worker handoff path: d:\Projects\jx-server\.agents\teamwork\worker_m2\handoff.md
Task assignment: d:\Projects\jx-server\.agents\teamwork\reviewer_m2\DISPATCH.md

Review all code changes, verify 1 type per .cs file, sealed classes, unit suffixes, build clean (0 warnings), and all 105 tests pass. Write your verdict (APPROVE or REQUEST_CHANGES) in `d:\Projects\jx-server\.agents\teamwork\reviewer_m2\handoff.md` and send a message.
