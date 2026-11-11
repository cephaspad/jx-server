# DISPATCH — Worker M4 Batch C (Specs 009 - 011 and 016)

## Context
- Milestone: M4 (Step 2 - 12 SpecKit Standardized Profiles Extraction)
- Target Specs:
  1. `specs/009-core-combat-and-missiles/`
  2. `specs/010-core-monster-ai-and-progression/`
  3. `specs/011-core-loot-and-item-generation/`
  4. `specs/016-module-chat-channels/`
- State Folder: `d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_c\`
- Original Request: `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md`
- Master Roadmap: `d:\Projects\jx-server\docs\specs-roadmap.md`
- Ground Truth Sources:
  - `specs/_archive_m1_m4/003-combat-missiles-loot-inventory/`
  - `specs/_archive_m1_m4/002-spatial-aoi-movement-chat/contracts/chat-protocol.md`
  - Actual C# Codebase:
    - `src/JXServer.ZoneEngine/Combat/` (Skills, Missiles, Five-Elements damage, Status effects)
    - `src/JXServer.ZoneEngine/Combat/MonsterAiZoneTickSystem.cs`, `CombatProgressionCommandHandler.cs`
    - `src/JXServer.ZoneEngine/Items/GroundObjectZoneTickSystem.cs`, `src/JXServer.GameAssets/Items/KItemGenerator.cs`, `LootTableCatalog.cs`
    - `src/JXServer.ZoneEngine/Chats/ChatZoneModule.cs`, `ChatCommandHandler.cs`

## Objective
Author the 4 complete SpecKit profiles for Specs 009, 010, 011, and 016 under `specs/`.

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
   - Data entities, schemas, combat/missile formulas, loot tables, chat channels, database entities.
6. `quickstart.md`:
   - Practical guide on how to build, verify, and run unit tests for this slice.
7. `contracts/*.md`:
   - Preserve 100% of the byte-exact protocol packet definitions, hex test vectors, Little-Endian offsets, C++ Kingsoft formulas, and JSON schemas from `specs/_archive_m1_m4/`.
8. `tasks.md`:
   - Complete, dependency-ordered tasks marked `[X]` mapping directly to the actual existing `.cs` source and `*Tests.cs` test files in the codebase.

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations and specifications must be genuine. DO NOT fabricate false vectors, omit ground-truth details, or make empty placeholders. A reviewer and forensic auditor will independently verify your work against `specs/_archive_m1_m4/` and the codebase.

When complete, write your handoff report to `d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_c\handoff.md` and send a message back.


## 2026-11-11T10:07:03Z
[Message] timestamp=2026-11-11T10:07:03Z sender=483c54f0-17a5-4a01-a852-4c57a8dfe829 priority=MESSAGE_PRIORITY_HIGH content=You are worker_m4_batch_c. Read your dispatch instructions at d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_c\DISPATCH.md.
Also read:
- d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
- d:\Projects\jx-server\.agents\teamwork\PROJECT.md
- d:\Projects\jx-server\docs\specs-roadmap.md

Your exclusive write boundaries:
- `d:\Projects\jx-server\specs\009-core-combat-and-missiles\`
- `d:\Projects\jx-server\specs\010-core-monster-ai-and-progression\`
- `d:\Projects\jx-server\specs\011-core-loot-and-item-generation\`
- `d:\Projects\jx-server\specs\016-module-chat-channels\`
- State folder: `d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_c\`

DO NOT write to any files outside these directories.
Read ground-truth from `specs/_archive_m1_m4/` and actual codebase to create all 8 SpecKit files for each of your 4 specs (spec.md, checklists/requirements.md with 16/16 [x], plan.md, research.md, data-model.md, quickstart.md, contracts/*.md, tasks.md with all [X] mapping to actual C# and test files).
