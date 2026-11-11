# Task Assignment: MapZoneWorker Deconstruction, ChatModule Separation & Assets Cleanup (Milestone M3)

Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\worker_m3\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Architecture blueprint path: d:\Projects\jx-server\.agents\teamwork\explorer_zone_survey\handoff.md
Assets survey path: d:\Projects\jx-server\.agents\teamwork\explorer_assets_survey\handoff.md

## Objective
Execute Milestone M3 (Step 1 Complete):

### 1. MapZoneWorker Deconstruction into Domain Handlers & Tick Systems
In `src/JXServer.ZoneEngine/WorldZones/`:
- Extract domain command handlers implementing `IZoneCommandHandler<TCommand>`:
  - `WorldSessionCommandHandler.cs` (handling world entry, session detach, entity queries)
  - `MovementCommandHandler.cs` (handling walk, run, jump, sit)
  - `CombatProgressionCommandHandler.cs` (handling skill cast, aura toggle, stat/skill point allocations)
  - `InventoryLootCommandHandler.cs` (handling item moves, divide, medicine eat, drop, loot pickup)
- Extract tick systems implementing `IZoneTickSystem`:
  - `StatusEffectZoneTickSystem.cs` (Phase 100)
  - `MonsterAiZoneTickSystem.cs` (Phase 200, wrapping `_monsterAiSystem`)
  - `MissileZoneTickSystem.cs` (Phase 300, wrapping `_missileSystem`)
  - `GroundObjectZoneTickSystem.cs` (Phase 400, wrapping `_groundObjectSystem`)
  - `MovementAndPostureZoneTickSystem.cs` (Phase 500, movement vector advancement, obstacle checks, trap detection)
  - `SessionHeartbeatZoneTickSystem.cs` (Phase 600, heartbeat timeouts and pings)
- Register these in `CoreZoneModule : IZoneModule`.
- Refactor `MapZoneWorker.cs` to delegate all command handling to `_extensionRegistry.GetRoute` and all tick processing to `_extensionRegistry.TickSystems` in order of `Phase`.

### 2. Golden Reference Plug-in `ChatZoneModule : IZoneModule` (Domain 016)
In `src/JXServer.ZoneEngine/Chats/`:
- Implement `ChatCommandHandler.cs` for `UpdateChatChannelMaskZoneCommand`, `SendNearbyTalkZoneCommand`, `SendStructuredChatZoneCommand`, `DeliverClusterChatZoneCommand`.
- Implement `ChatZoneModule : IZoneModule` that registers `ChatCommandHandler`.
- Wire `ChatZoneModule` via `ZoneExtensionRegistry` so that `MapZoneWorker.cs` has ZERO lines of chat code.

### 3. GameAssets Runtime Cleanup
In `src/JXServer.GameAssets`:
- Move legacy file parsers (`PakFilePathHasher.cs`, `UclNrv2bDecompressor.cs`, `XPackArchiveReader.cs`, `IniFileParser.cs`, `TabFileParser.cs`) to `tools/JXServer.DataExporter/` or remove them from runtime project compilation so that runtime only loads MemoryPack `.pack` and `data/**/*.json`, satisfying Constitution v1.2.0 Principle V and `docs/data-architecture-standard.md`.

### 4. Build & Test Verification
- `dotnet build JXServer.slnx` -> 0 warnings, 0 errors (`TreatWarningsAsErrors=true`).
- `dotnet test JXServer.slnx` -> all 135+ tests pass (100% pass rate, zero regressions).
- Write handoff report to `d:\Projects\jx-server\.agents\teamwork\worker_m3\handoff.md`.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.


## 2026-11-11T08:59:40Z
You are the MapZoneWorker Deconstruction, ChatModule & Assets Cleanup Specialist for Milestone M3.
Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\worker_m3\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Task assignment: d:\Projects\jx-server\.agents\teamwork\worker_m3\DISPATCH.md
Architecture blueprint path: d:\Projects\jx-server\.agents\teamwork\explorer_zone_survey\handoff.md
Assets survey path: d:\Projects\jx-server\.agents\teamwork\explorer_assets_survey\handoff.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Your Task:
1. Deconstruct `MapZoneWorker.cs` by extracting single-responsibility domain command handlers (`WorldSessionCommandHandler`, `MovementCommandHandler`, `CombatProgressionCommandHandler`, `InventoryLootCommandHandler`) and tick systems (`StatusEffectZoneTickSystem`, `MonsterAiZoneTickSystem`, `MissileZoneTickSystem`, `GroundObjectZoneTickSystem`, `MovementAndPostureZoneTickSystem`, `SessionHeartbeatZoneTickSystem`). Register them in `CoreZoneModule : IZoneModule`.
2. Decouple `Chats/` completely into an independent Golden Reference plug-in module `ChatZoneModule : IZoneModule` (Domain 016) with `ChatCommandHandler.cs`. Ensure `MapZoneWorker.cs` contains zero chat code.
3. Clean up legacy file readers (`PakFilePathHasher.cs`, `UclNrv2bDecompressor.cs`, `XPackArchiveReader.cs`, `IniFileParser.cs`, `TabFileParser.cs`) from runtime `src/JXServer.GameAssets` so only MemoryPack and JSON are used at runtime.
4. Verify `dotnet build JXServer.slnx` (0 warnings, 0 errors, `TreatWarningsAsErrors=true`).
5. Verify `dotnet test JXServer.slnx` (all 135+ tests pass with zero regression).
6. Write a detailed handoff report to `d:\Projects\jx-server\.agents\teamwork\worker_m3\handoff.md` and send a message.
