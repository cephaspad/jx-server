# Succession Soft Handoff: Project Orchestrator (Generation 1 -> Generation 2)

## 1. Observation

### 1.1 Mission & Context
- **Objective**: Execute **Phương án B (Zero-Regression Re-baseline)** for JXServer at `d:\Projects\jx-server`.
- **Governing Architecture & Standards**:
  - Hiến pháp dự án: `.specify/memory/constitution.md` (v1.2.0).
  - Kiến trúc dữ liệu: `docs/data-architecture-standard.md`.
  - Lộ trình: `docs/specs-roadmap.md`.
  - Hợp đồng Modular Spine: `specs/_archive_m1_m4/004-zone-modular-spine/contracts/`.
- **Original Parent**: `a34bb1fe-7681-435a-95cc-878a0ecac390`.

### 1.2 Completed Milestones & Current State
1. **Phase 0 (Survey) — COMPLETED**:
   - 3 parallel survey specialists mapped the entire codebase, contracts, and specs.
   - Cataloged 54 features and 26 edge cases in `PROJECT.md` at `d:\Projects\jx-server\.agents\teamwork\PROJECT.md`.
2. **Milestone M1 (Step 0 Ground-Truth Archival) — COMPLETED & APPROVED**:
   - Safely archived 39 specification files from `001-gateway-world-entry`, `002-spatial-aoi-movement-chat`, `003-combat-missiles-loot-inventory`, and `004-zone-modular-spine` into `specs/_archive_m1_m4/`.
   - Verified 100% SHA-256 byte-for-byte preservation and baseline 97/97 tests passing. Certified by Reviewer.
3. **Milestone M2 (Step 1 Core: Modular Spine, Domain Sinks, Persistence & Harness) — COMPLETED & CERTIFIED CLEAN**:
   - Implemented `IZoneModule`, `IZoneCommandHandler<T>`, `IZoneTickSystem`, `ZoneTickPhase`, `ZoneExecutionContext`, `ZoneAssetServices`, `ZoneExtensionBuilder`, `ZoneExtensionRegistry`, `PlayerFeatureSlotKey<T>`, and fault isolation (EventIds 3009–3012).
   - Decomposed `IPlayerOutboundSink` into `IWorldOutboundSink` (14 methods), `ICombatOutboundSink` (10 methods), `IInventoryOutboundSink` (7 methods), and `IChatOutboundSink` (3 methods).
   - Added persistence fields: `TaskValues` (sparse 0..65535 JSON map), `RepositoryPasswordHash` (MD5 hex), `CharacterItem.IsBroken`, `EnchaseLevel` (0..10), and lease epoch fencing.
   - Built `ZoneSimulationHarness` stepping 1,000 frames in 1.118ms (< 5ms) with `FakeTimeProvider`.
   - Remediation cycle resolved `CharacterItem.cs` constructor field assignment; 16 new automated unit/integration tests added (135/135 tests passing). Certified CLEAN (ACCEPTED) by Forensic Auditor and APPROVED by Reviewer.
4. **Milestone M3 (Step 1 Complete: MapZoneWorker Deconstruction, ChatModule & Assets Cleanup) — IMPLEMENTATION COMPLETE**:
   - `worker_m3` (9263f304-5f03-44e2-84b8-403f845f1448) completed all code modifications (see `worker_m3/handoff.md`):
     - Deconstructed `MapZoneWorker` into 4 domain command handlers (`WorldSessionCommandHandler`, `MovementCommandHandler`, `CombatProgressionCommandHandler`, `InventoryLootCommandHandler`) and 6 phased tick systems (Phases 100–600).
     - Registered in `CoreZoneModule : IZoneModule`.
     - Decoupled `ChatZoneModule : IZoneModule` (Domain 016) with `ChatCommandHandler.cs`; verified `MapZoneWorker.cs` contains zero chat code.
     - Quarantined legacy file readers (`PakFilePathHasher.cs`, `UclNrv2bDecompressor.cs`, `XPackArchiveReader.cs`, `IniFileParser.cs`, `TabFileParser.cs`) in `tools/JXServer.DataExporter/Legacy/`; runtime `src/JXServer.GameAssets` now uses pure MemoryPack & JSON.
     - Solution builds with 0 warnings (`TreatWarningsAsErrors=true`).
     - 139/139 tests pass (59 Protocol, 58 ZoneEngine, 22 Integration; 0 failures).

---

## 2. Logic Chain

1. **Succession Reason**:
   - The top-level orchestrator reached its maximum spawn threshold of 16 subagents (`spec_miner_survey`, `explorer_zone_survey`, `explorer_assets_survey`, `worker_m1`, `reviewer_m1`, `worker_m2`, `reviewer_m2`, `challenger_m2`, `auditor_m2`, 3x remediation explorers, `worker_m2_remediation`, `auditor_m2_remediation`, `reviewer_m2_remediation`, `worker_m3`).
   - All 16 subagents have completed their tasks. No subagents or background tasks are running.
   - Per Succession Protocol, Generation 1 must cleanly hand over to Generation 2 (`orchestrator_gen2`).

2. **Immediate Successor Responsibilities**:
   - **Step 1: Milestone M3 Gate Verification**:
     - Dispatch `teamwork_reviewer` and `teamwork_auditor` to evaluate `worker_m3` deliverables.
     - Verify 0 warnings, 139/139 tests passing, clean modular decoupling, zero chat in `MapZoneWorker.cs`, and pure MemoryPack/JSON assets.
     - Record verdicts in `GATE_STATUS.md`. On PASS, update M3 to `DONE` in `PROJECT.md` and `progress.md`.
   - **Step 2: Milestone M4 (12 SpecKit Standardized Profiles Extraction)**:
     - Generate/extract 12 specification directories under `specs/`:
       1. `specs/001-core-game-data-pipeline/`
       2. `specs/002-core-legacy-wire-protocol/`
       3. `specs/003-core-auth-roster-and-leases/`
       4. `specs/004-core-zone-spine-and-world-entry/`
       5. `specs/005-core-spatial-aoi-and-spawns/`
       6. `specs/006-core-movement-and-obstacles/`
       7. `specs/007-core-portals-and-map-handoff/`
       8. `specs/008-core-inventory-and-equipment/`
       9. `specs/009-core-combat-and-missiles/`
       10. `specs/010-core-monster-ai-and-progression/`
       11. `specs/011-core-loot-and-item-generation/`
       12. `specs/016-module-chat-channels/`
     - Each spec directory must contain full SpecKit artifacts:
       - `spec.md` (technology-agnostic WHAT/WHY, keeping all Clarifications, User Stories, Edge Cases, Functional Requirements, Key Entities, Measurable Success Criteria).
       - `checklists/requirements.md` (16/16 `[x]`).
       - `plan.md` (Technical Context, Constitution Check v1.2.0, actual Project Structure).
       - `research.md`, `data-model.md`, `contracts/*.md` (preserving 100% ground-truth bytes, hex test vectors, Little-Endian offsets, C++ formulas from `specs/_archive_m1_m4/`), `quickstart.md`.
       - `tasks.md` (all tasks marked `[X]` mapping directly to actual `.cs` and `*Tests.cs` files in the codebase).
   - **Step 3: Milestone M5 (Automated Acceptance, Convergence & Final Teardown)**:
     - Execute `/speckit-analyze` and `/speckit-converge` verification across all 12 specs against the C# codebase and constitution v1.2.0.
     - Ensure all 12 specs achieve 100% Converged status.
     - Verify zero regression: full test suite passes.
     - Delete temporary folder `specs/_archive_m1_m4/`.
     - Update `.specify/feature.json` to point to `"specs/016-module-chat-channels"`.
     - Send final completion message to caller agent `a34bb1fe-7681-435a-95cc-878a0ecac390`.

---

## 3. Caveats & Critical Rules

1. **Strict Dispatch-Only Role**:
   - You are a DISPATCH-ONLY orchestrator. NEVER write/edit source code directly. NEVER run build/test commands directly. Require workers to do so.
   - File-editing permissions are restricted strictly to metadata/state `.md` files in `.agents/teamwork/`.
2. **Binary Forensic Audit Veto**:
   - If a Forensic Auditor (`teamwork_auditor`) reports INTEGRITY VIOLATION, the milestone FAILS UNCONDITIONALLY. Do not advance, do not weigh against test scores, do not skip.
3. **Constitution Compliance (v1.2.0)**:
   - .NET 10 / C# 14, 1 type per `.cs` file, sealed concrete classes, pluralized feature folders, explicit unit suffixes (`*MpsX`, `*CellX`, `*RegionX`, `*Frames`), `TreatWarningsAsErrors=true` (0 warnings).
4. **Parent Passthrough**:
   - Your parent conversation ID is `a34bb1fe-7681-435a-95cc-878a0ecac390`. When completing the entire mission or escalating, use `send_message` to this recipient.

---

## 4. Key Artifacts Reference

- Original Request: `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md`
- Master Project Architecture & Milestones: `d:\Projects\jx-server\.agents\teamwork\PROJECT.md`
- Gen1 Progress: `d:\Projects\jx-server\.agents\teamwork\orchestrator\progress.md`
- Gen1 Briefing: `d:\Projects\jx-server\.agents\teamwork\orchestrator\BRIEFING.md`
- Gen1 Gate Status: `d:\Projects\jx-server\.agents\teamwork\orchestrator\GATE_STATUS.md`
- Worker M3 Completion Report: `d:\Projects\jx-server\.agents\teamwork\worker_m3\handoff.md`
- Ground-Truth Archive: `d:\Projects\jx-server\specs\_archive_m1_m4/`
