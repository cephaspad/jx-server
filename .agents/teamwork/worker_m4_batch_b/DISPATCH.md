# DISPATCH — Worker M4 Batch B (Specs 005 - 008)

## Context
- Milestone: M4 (Step 2 - 12 SpecKit Standardized Profiles Extraction)
- Target Specs:
  1. `specs/005-core-spatial-aoi-and-spawns/`
  2. `specs/006-core-movement-and-obstacles/`
  3. `specs/007-core-portals-and-map-handoff/`
  4. `specs/008-core-inventory-and-equipment/`
- State Folder: `d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_b\`
- Original Request: `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md`
- Master Roadmap: `d:\Projects\jx-server\docs\specs-roadmap.md`
- Ground Truth Sources:
  - `specs/_archive_m1_m4/002-spatial-aoi-movement-chat/`
  - `specs/_archive_m1_m4/003-combat-missiles-loot-inventory/`
  - Actual C# Codebase:
    - `src/JXServer.ZoneEngine/Spatial/` (SpatialRegionGrid, AOI 3x3)
    - `src/JXServer.ZoneEngine/WorldZones/MovementCommandHandler.cs`, `MovementAndPostureZoneTickSystem.cs`, `src/JXServer.GameAssets/Obstacles/`
    - `src/JXServer.ZoneEngine/WorldZones/` (Portals, Traps, Map Handoff, FightMode)
    - `src/JXServer.ZoneEngine/Items/InventoryLootCommandHandler.cs`, `src/JXServer.Domain/Characters/CharacterItem.cs`

## Objective
Author the 4 complete SpecKit profiles for Specs 005, 006, 007, and 008 under `specs/`.

For each specification directory:
1. `spec.md`:
   - Technology-agnostic WHAT and WHY.
   - User Stories with Acceptance Scenarios.
   - Functional Requirements (`FR-001`, `FR-002`, etc.) with clear, measurable outcomes.
   - Measurable Success Criteria (`SC-001`, `SC-002`, etc.).
   - Edge Cases.
2. `checklists/requirements.md`:
   - Complete 16/16 requirements checklist, all marked `[x]`.
3. `plan.md`:
   - Technical Context, Architecture, Constitution Check v1.2.0 (.NET 10 / C# 14, 1 type per `.cs` file, sealed concrete classes, pluralized folders, unit suffixes, TreatWarningsAsErrors).
   - Real Project Structure mapping actual source files in `src/` and test files in `tests/`.
4. `research.md`:
   - Technical research, design rationale, and ground-truth references.
5. `data-model.md`:
   - Data entities, schemas, spatial/container memory formats, database entities.
6. `quickstart.md`:
   - Practical guide on how to build, verify, and run unit tests for this slice.
7. `contracts/*.md`:
   - Preserve 100% of the byte-exact protocol packet definitions, hex test vectors, Little-Endian offsets, C++ Kingsoft formulas, and JSON schemas from `specs/_archive_m1_m4/`.
8. `tasks.md`:
   - Complete, dependency-ordered tasks marked `[X]` mapping directly to the actual existing `.cs` source and `*Tests.cs` test files in the codebase.

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations and specifications must be genuine. DO NOT fabricate false vectors, omit ground-truth details, or make empty placeholders. A reviewer and forensic auditor will independently verify your work against `specs/_archive_m1_m4/` and the codebase.

When complete, write your handoff report to `d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_b\handoff.md` and send a message back.


## 2026-11-11T10:06:52Z
From: 483c54f0-17a5-4a01-a852-4c57a8dfe829
Priority: MESSAGE_PRIORITY_HIGH
Content:
You are worker_m4_batch_b. Read your dispatch instructions at d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_b\DISPATCH.md.
Also read:
- d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
- d:\Projects\jx-server\.agents\teamwork\PROJECT.md
- d:\Projects\jx-server\docs\specs-roadmap.md

Your exclusive write boundaries:
- `d:\Projects\jx-server\specs\005-core-spatial-aoi-and-spawns\`
- `d:\Projects\jx-server\specs\006-core-movement-and-obstacles\`
- `d:\Projects\jx-server\specs\007-core-portals-and-map-handoff\`
- `d:\Projects\jx-server\specs\008-core-inventory-and-equipment\`
- State folder: `d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_b\`

DO NOT write to any files outside these directories.
Read ground-truth from `specs/_archive_m1_m4/` and actual codebase to create all 8 SpecKit files for each of your 4 specs (spec.md, checklists/requirements.md with 16/16 [x], plan.md, research.md, data-model.md, quickstart.md, contracts/*.md, tasks.md with all [X] mapping to actual C# and test files).

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations and specifications must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A reviewer and forensic auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

When complete, write your handoff report to d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_b\handoff.md and notify me via send_message.
