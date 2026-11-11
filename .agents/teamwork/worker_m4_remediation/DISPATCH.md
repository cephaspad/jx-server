# DISPATCH — Worker M4 Remediation (Specs Path Corrections)

## Context
- Milestone: M4 (Iteration 2 — Remediation Implementation)
- Remediation Plan: `d:\Projects\jx-server\.agents\teamwork\explorer_m4_remediation\handoff.md` (Read Section 4 for exact line-by-line before/after replacements)
- Auditor Report: `d:\Projects\jx-server\.agents\teamwork\auditor_m4\handoff.md`
- Master Architecture: `d:\Projects\jx-server\.agents\teamwork\PROJECT.md`
- Original Request: `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md`
- State Folder: `d:\Projects\jx-server\.agents\teamwork\worker_m4_remediation\`

## Objective
Apply the exact file path and naming corrections documented in Section 4 of `explorer_m4_remediation\handoff.md` across the affected specification files:
1. `specs/001-core-game-data-pipeline/`:
   - `tasks.md` (replace `field-monsters.loot.json` with `droprates.loot.json`)
   - `plan.md` (replace `game-assets.pack` with `jxdata.pack`, update loot filename)
   - `data-model.md` (update binary snapshot filename and parity report)
   - `quickstart.md` (update snapshot filename)
   - `research.md` (update snapshot filename)
   - `contracts/jxdata-schemas-and-serialization.md` (update snapshot filename)
2. `specs/004-core-zone-spine-and-world-entry/`:
   - `tasks.md` (replace truncated `.WorldSink.cs` etc. with `GameServerClientSession.*Sink.cs`; replace `CharacterTaskValueConsts.cs` reference with `CharacterConsts.cs`; fix `CharacterItemUnitTests.cs` to `Items/CharacterItemUnitTests.cs`)
   - `plan.md` (remove non-existent `CharacterTaskValueConsts.cs`; add `Items/` subdirectory to `CharacterItemUnitTests.cs`)
3. `specs/006-core-movement-and-obstacles/`:
   - `tasks.md`, `plan.md`, `data-model.md`, `spec.md`, `research.md` (replace `RegionObstacleData` in `Obstacles/` with `RegionObstacleGrid` in `WorldMaps/`)
4. `specs/007-core-portals-and-map-handoff/`:
   - `tasks.md`, `plan.md`, `data-model.md` (replace `TrapCellRun` with `RegionTrapGrid`)
5. `specs/008-core-inventory-and-equipment/`:
   - `tasks.md`, `plan.md`, `data-model.md` (replace `ItemEquipPartKind` with `EquipmentPartKind`)
6. `specs/010-core-monster-ai-and-progression/`:
   - `tasks.md`, `plan.md`, `data-model.md` (replace `Npcs/` and `Players/` with `NpcTemplates/` and `PlayerTemplates/`; reference real classes `CharacterBaseTemplate`, `PlayerSeriesGrowthRuleTemplate`, `PlayerTemplateCatalog`)

## Verification
After applying the edits, run:
1. `powershell -ExecutionPolicy Bypass -File d:\Projects\jx-server\.agents\teamwork\auditor_m4\audit_task_paths_precise.ps1`
2. `powershell -ExecutionPolicy Bypass -File d:\Projects\jx-server\.agents\teamwork\auditor_m4\audit_unbackticked_cs.ps1`
3. `powershell -ExecutionPolicy Bypass -File d:\Projects\jx-server\.agents\teamwork\explorer_m4_remediation\scan_all_specs.ps1`
4. `dotnet build JXServer.slnx` -> verify 0 warnings, 0 errors.
5. `dotnet test JXServer.slnx -- RunConfiguration.DisableParallelization=true` -> verify 139/139 pass.

Write your handoff report to `d:\Projects\jx-server\.agents\teamwork\worker_m4_remediation\handoff.md` and notify the orchestrator.


## 2026-11-11T10:54:49Z
Sender: 483c54f0-17a5-4a01-a852-4c57a8dfe829
Priority: MESSAGE_PRIORITY_HIGH
Content:
You are worker_m4_remediation. Read your dispatch instructions at d:\Projects\jx-server\.agents\teamwork\worker_m4_remediation\DISPATCH.md.
Also read:
- d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
- d:\Projects\jx-server\.agents\teamwork\PROJECT.md
- d:\Projects\jx-server\.agents\teamwork\explorer_m4_remediation\handoff.md (Contains exact before/after replacements in Section 4)

Your task:
Apply the exact replacements from Section 4 of explorer_m4_remediation/handoff.md to the spec files across specs/001, 004, 006, 007, 008, 010.
Then run the verification scripts and test suite:
1. powershell -ExecutionPolicy Bypass -File d:\Projects\jx-server\.agents\teamwork\auditor_m4\audit_task_paths_precise.ps1
2. powershell -ExecutionPolicy Bypass -File d:\Projects\jx-server\.agents\teamwork\auditor_m4\audit_unbackticked_cs.ps1
3. powershell -ExecutionPolicy Bypass -File d:\Projects\jx-server\.agents\teamwork\explorer_m4_remediation\scan_all_specs.ps1
4. dotnet build JXServer.slnx
5. dotnet test JXServer.slnx -- RunConfiguration.DisableParallelization=true

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations and specifications must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A reviewer and forensic auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Write your handoff report to d:\Projects\jx-server\.agents\teamwork\worker_m4_remediation\handoff.md and notify me via send_message.
