# DISPATCH — Worker M4 Batch A (Specs 001 - 004)

## Context
- Milestone: M4 (Step 2 - 12 SpecKit Standardized Profiles Extraction)
- Target Specs:
  1. `specs/001-core-game-data-pipeline/`
  2. `specs/002-core-legacy-wire-protocol/`
  3. `specs/003-core-auth-roster-and-leases/`
  4. `specs/004-core-zone-spine-and-world-entry/`
- State Folder: `d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_a\`
- Original Request: `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md`
- Master Roadmap: `d:\Projects\jx-server\docs\specs-roadmap.md`
- Ground Truth Sources:
  - `specs/_archive_m1_m4/001-gateway-world-entry/`
  - `specs/_archive_m1_m4/004-zone-modular-spine/`
  - `docs/data-architecture-standard.md`
  - Actual C# Codebase:
    - `src/JXServer.GameAssets/`, `tools/JXServer.DataExporter/`
    - `src/JXServer.Protocol/`, `src/JXServer.Adapter.LegacyJX1/Framing/`
    - `src/JXServer.Application/SessionLeases/`, `src/JXServer.Adapter.LegacyJX1/Gateway/`, `src/JXServer.Domain/Characters/`
    - `src/JXServer.ZoneEngine/WorldZones/`, `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/`

## Objective
Author the 4 complete SpecKit profiles for Specs 001, 002, 003, and 004 under `specs/`.

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
   - Data entities, schemas, MemoryPack/JSON formats, database entities.
6. `quickstart.md`:
   - Practical guide on how to build, verify, and run unit tests for this slice.
7. `contracts/*.md`:
   - Preserve 100% of the byte-exact protocol packet definitions, hex test vectors, Little-Endian offsets, C++ Kingsoft formulas, and JSON schemas from `specs/_archive_m1_m4/`.
8. `tasks.md`:
   - Complete, dependency-ordered tasks marked `[X]` mapping directly to the actual existing `.cs` source and `*Tests.cs` test files in the codebase.

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations and specifications must be genuine. DO NOT fabricate false vectors, omit ground-truth details, or make empty placeholders. A reviewer and forensic auditor will independently verify your work against `specs/_archive_m1_m4/` and the codebase.

When complete, write your handoff report to `d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_a\handoff.md` and send a message back.


## 2026-11-11T10:06:44Z
You are worker_m4_batch_a. Read your dispatch instructions at d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_a\DISPATCH.md.
Also read:
- d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
- d:\Projects\jx-server\.agents\teamwork\PROJECT.md
- d:\Projects\jx-server\docs\specs-roadmap.md

Your exclusive write boundaries:
- `d:\Projects\jx-server\specs\001-core-game-data-pipeline\`
- `d:\Projects\jx-server\specs\002-core-legacy-wire-protocol\`
- `d:\Projects\jx-server\specs\003-core-auth-roster-and-leases\`
- `d:\Projects\jx-server\specs\004-core-zone-spine-and-world-entry\`
- State folder: `d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_a\`

DO NOT write to any files outside these directories.
Read ground-truth from `specs/_archive_m1_m4/` and actual codebase to create all 8 SpecKit files for each of your 4 specs (spec.md, checklists/requirements.md with 16/16 [x], plan.md, research.md, data-model.md, quickstart.md, contracts/*.md, tasks.md with all [X] mapping to actual C# and test files).

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations and specifications must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A reviewer and forensic auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

When complete, write your handoff report to d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_a\handoff.md and notify me via send_message.
