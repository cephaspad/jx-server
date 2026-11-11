# DISPATCH — Explorer (Milestone M4 Remediation)

## Context
- Milestone: M4 (Iteration 2 — Remediation of Forensic Audit Integrity Violations)
- Audit Report: `d:\Projects\jx-server\.agents\teamwork\auditor_m4\handoff.md` (Read in full, do not summarize or omit)
- Master Architecture: `d:\Projects\jx-server\.agents\teamwork\PROJECT.md`
- Original Request: `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md`
- State Folder: `d:\Projects\jx-server\.agents\teamwork\explorer_m4_remediation\`

## Full Auditor Evidence
The Forensic Auditor reported INTEGRITY VIOLATION due to non-existent / hallucinated file path references in 4 specifications:

1. **`specs/001-core-game-data-pipeline/tasks.md`** & **`plan.md`**:
   - `tasks.md` line 34: `data/loot/field-monsters.loot.json` does NOT exist.
   - `plan.md` line 72: references `field-monsters.loot.json`.
   - On disk: the actual authored loot file is `data/loot/droprates.loot.json`.

2. **`specs/004-core-zone-spine-and-world-entry/tasks.md`** & **`plan.md`**:
   - `tasks.md` line 41: references truncated filenames `.WorldSink.cs`, `.CombatSink.cs`, `.InventorySink.cs`, `.ChatSink.cs`. Actual files are `GameServerClientSession.WorldSink.cs`, `GameServerClientSession.CombatSink.cs`, `GameServerClientSession.InventorySink.cs`, `GameServerClientSession.ChatSink.cs`.
   - `tasks.md` line 47 & `plan.md` line 76: references `CharacterTaskValueConsts.cs`. Actual file does not exist; constants `MinTaskId` and `MaxTaskId` reside inside `src/JXServer.Primitives/Characters/CharacterConsts.cs`.

3. **`specs/006-core-movement-and-obstacles/tasks.md`**, **`plan.md`**, **`data-model.md`**, **`spec.md`**:
   - references `src/JXServer.GameAssets/Obstacles/RegionObstacleData.cs`. Neither the folder `Obstacles/` nor the file `RegionObstacleData.cs` exists.
   - On disk: the actual obstacle grid is `RegionObstacleGrid` in `src/JXServer.GameAssets/WorldMaps/RegionObstacleGrid.cs`.

4. **`specs/010-core-monster-ai-and-progression/tasks.md`**:
   - lines 12-13: references `src/JXServer.GameAssets/Npcs/NpcTemplate.cs` and `src/JXServer.GameAssets/Players/PlayerTemplate.cs`.
   - On disk: folders are `src/JXServer.GameAssets/NpcTemplates/` and `src/JXServer.GameAssets/PlayerTemplates/`.
   - In `PlayerTemplates/`, the player template class is `CharacterBaseTemplate.cs` (along with `PlayerSeriesGrowthRuleTemplate.cs`, etc.).

## Objective
Investigate the actual file paths across `d:\Projects\jx-server` and produce a comprehensive remediation strategy detailing:
1. The exact replacements needed in `specs/001-core-game-data-pipeline/tasks.md` and `plan.md`.
2. The exact replacements needed in `specs/004-core-zone-spine-and-world-entry/tasks.md` and `plan.md`.
3. The exact replacements needed in `specs/006-core-movement-and-obstacles/tasks.md`, `plan.md`, `data-model.md`, and `spec.md`.
4. The exact replacements needed in `specs/010-core-monster-ai-and-progression/tasks.md`.
5. Verify whether any other spec files have similar path discrepancies.

Write your complete analysis and remediation plan to `d:\Projects\jx-server\.agents\teamwork\explorer_m4_remediation\handoff.md`.
Do NOT modify any code or specification files directly (Explorer is read-only).
When complete, notify the orchestrator via send_message.


## 2026-11-11T10:40:42Z
[Message] sender=483c54f0-17a5-4a01-a852-4c57a8dfe829 priority=MESSAGE_PRIORITY_HIGH
You are explorer_m4_remediation. Read your dispatch instructions at d:\Projects\jx-server\.agents\teamwork\explorer_m4_remediation\DISPATCH.md.
Also read:
- d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
- d:\Projects\jx-server\.agents\teamwork\PROJECT.md
- d:\Projects\jx-server\.agents\teamwork\auditor_m4\handoff.md (Full evidence report from the Forensic Auditor)

Investigate the exact file paths across the codebase and determine the precise, verifiable replacements to fix all non-existent file path references in:
1. specs/001-core-game-data-pipeline/ (tasks.md, plan.md)
2. specs/004-core-zone-spine-and-world-entry/ (tasks.md, plan.md)
3. specs/006-core-movement-and-obstacles/ (tasks.md, plan.md, data-model.md, spec.md)
4. specs/010-core-monster-ai-and-progression/ (tasks.md)
Check all other specs to ensure zero additional path discrepancies exist.

Write your findings and actionable remediation plan to d:\Projects\jx-server\.agents\teamwork\explorer_m4_remediation\handoff.md.
Do NOT modify files directly. When complete, notify me via send_message.
