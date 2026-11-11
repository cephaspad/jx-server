# Forensic Integrity Audit Report: Milestone M4 Gate

**Agent**: `auditor_m4`  
**Parent Conversation ID**: `483c54f0-17a5-4a01-a852-4c57a8dfe829`  
**Date**: 2026-11-11T10:40:00Z  
**Type**: Hard Handoff (Forensic Audit Report)  

---

## Forensic Audit Report

**Work Product**: 12 SpecKit Standardized Profile Directories under `specs/` (`001`–`011` and `016`)  
**Profile**: General Project / Forensic Auditor (Integrity Mode: `development`)  
**Verdict**: **INTEGRITY VIOLATION** (Rejected: Non-existent / Hallucinated File Paths in `tasks.md`)

---

### Phase Results

| Check Item | Result | Details |
| :--- | :---: | :--- |
| **1. Spec Directories & File Inventory** | **PASS** | All 12 directories exist under `specs/`; 106 files total (8-11 files per spec); exactly 0 zero-length files. |
| **2. Requirements Checklist Verification** | **PASS** | 16/16 `[x]` across all 12 specs in `checklists/requirements.md` (192/192 checked, 0 unchecked). |
| **3. Tasks.md Integrity & File Path Mapping** | **FAIL** | All 266 tasks are marked `[X]`, but **4 specifications reference non-existent/hallucinated file paths** (8 total instances). |
| **4. Ground-Truth Fidelity Audit** | **PASS** | 39/39 key protocol opcodes, payload sizes, Little-Endian formats, and C++ formulas match `specs/_archive_m1_m4/` with 100% fidelity. |
| **5. Cheat Pattern & Facade Detection** | **PASS** | 0 `NotImplementedException`, 0 `TODO`/`FIXME` stubs in `src/`, 0 pre-populated result logs. |
| **6. Runtime Compilation Build** | **PASS** | `dotnet build JXServer.slnx` -> 0 warnings, 0 errors (`TreatWarningsAsErrors=true`). |
| **7. Runtime Test Suite Execution** | **PASS** | `dotnet test JXServer.slnx -- RunConfiguration.DisableParallelization=true` -> **139/139 passed** (0 failed, 0 skipped, 18.4s). |

---

## 1. Observation

### 1.1 Directory & File Inventory Verification
- Automated scan across `specs/` identified 106 total files across 12 specs:
  - `001-core-game-data-pipeline`: 9 files (spec, plan, tasks, research, data-model, quickstart, checklists/requirements, 2 contracts)
  - `002-core-legacy-wire-protocol`: 9 files (spec, plan, tasks, research, data-model, quickstart, checklists/requirements, 2 contracts)
  - `003-core-auth-roster-and-leases`: 9 files (spec, plan, tasks, research, data-model, quickstart, checklists/requirements, 2 contracts)
  - `004-core-zone-spine-and-world-entry`: 11 files (spec, plan, tasks, research, data-model, quickstart, checklists/requirements, 4 contracts)
  - `005-core-spatial-aoi-and-spawns`: 9 files (spec, plan, tasks, research, data-model, quickstart, checklists/requirements, 2 contracts)
  - `006-core-movement-and-obstacles`: 9 files (spec, plan, tasks, research, data-model, quickstart, checklists/requirements, 2 contracts)
  - `007-core-portals-and-map-handoff`: 9 files (spec, plan, tasks, research, data-model, quickstart, checklists/requirements, 2 contracts)
  - `008-core-inventory-and-equipment`: 9 files (spec, plan, tasks, research, data-model, quickstart, checklists/requirements, 2 contracts)
  - `009-core-combat-and-missiles`: 8 files (spec, plan, tasks, research, data-model, quickstart, checklists/requirements, 1 contract)
  - `010-core-monster-ai-and-progression`: 8 files (spec, plan, tasks, research, data-model, quickstart, checklists/requirements, 1 contract)
  - `011-core-loot-and-item-generation`: 8 files (spec, plan, tasks, research, data-model, quickstart, checklists/requirements, 1 contract)
  - `016-module-chat-channels`: 8 files (spec, plan, tasks, research, data-model, quickstart, checklists/requirements, 1 contract)
- Zero empty files (`Length == 0`) found across all 106 files.

### 1.2 Checklist Audit
- Automated regex audit for `^\s*-\s*\[x\]` vs `^\s*-\s*\[ \]` across all 12 `checklists/requirements.md` files:
  - All 12 files contain exactly 16 `[x]` and 0 `[ ]`.

### 1.3 Tasks.md Non-Existent File Path Observations (FAIL)
An exhaustive indexing of all 792 files in the repository compared against every referenced `.cs` and `.json` file across the 266 completed tasks revealed 4 specifications referencing non-existent file paths:

1. **`specs/001-core-game-data-pipeline/tasks.md`**:
   - Line 34: `- [X] T013 [P] [US1] Author weighted monster loot tables in data/loot/field-monsters.loot.json`
   - Also in `plan.md` Line 72: `└── field-monsters.loot.json`
   - **Observation**: File `data/loot/field-monsters.loot.json` does NOT exist in the repository. The actual authored file on disk is `data/loot/droprates.loot.json` (size: 2,033,975 bytes).

2. **`specs/004-core-zone-spine-and-world-entry/tasks.md`**:
   - Line 41: `- [X] T015 [US4] Decompose GameServerClientSession into 5 partial class files (GameServerClientSession.cs, .WorldSink.cs, .CombatSink.cs, .InventorySink.cs, .ChatSink.cs) sharing _outboundChannel in src/JXServer.Adapter.LegacyJX1/GameServer/`
     - **Observation**: Referenced as truncated filenames `.WorldSink.cs`, `.CombatSink.cs`, etc. The actual files are `GameServerClientSession.WorldSink.cs`, `GameServerClientSession.CombatSink.cs`, `GameServerClientSession.InventorySink.cs`, and `GameServerClientSession.ChatSink.cs`.
   - Line 47: `- [X] T016 [P] [US4] Add task bounds constants in src/JXServer.Primitives/Characters/CharacterConsts.cs and CharacterTaskValueConsts.cs`
     - Also in `plan.md` Line 76: `└── CharacterTaskValueConsts.cs`
     - **Observation**: File `src/JXServer.Primitives/Characters/CharacterTaskValueConsts.cs` (or `src/JXServer.Domain/Characters/CharacterTaskValueConsts.cs`) does NOT exist in the repository. The constants `MinTaskId = 0` and `MaxTaskId = ushort.MaxValue` are implemented inside `src/JXServer.Primitives/Characters/CharacterConsts.cs` (lines 18-19).

3. **`specs/006-core-movement-and-obstacles/tasks.md`**:
   - Line 16: `- [X] T002 [P] [US1] Implement RegionObstacleData fast bitmask for 16x32 cell obstacle lookups in src/JXServer.GameAssets/Obstacles/RegionObstacleData.cs.`
     - Also in `plan.md` Line 88, `data-model.md` Line 74, `spec.md` Line 93: `RegionObstacleData` in `src/JXServer.GameAssets/Obstacles/`.
     - **Observation**: Neither the folder `src/JXServer.GameAssets/Obstacles/` nor the file `RegionObstacleData.cs` exists in the repository. The actual obstacle matrix class is `RegionObstacleGrid` in `src/JXServer.GameAssets/WorldMaps/RegionObstacleGrid.cs`.

4. **`specs/010-core-monster-ai-and-progression/tasks.md`**:
   - Line 12: `- [X] Implement static NPC and player progression templates: src/JXServer.GameAssets/Npcs/NpcTemplate.cs, src/JXServer.GameAssets/Players/PlayerTemplate.cs`
   - Line 13: `- [X] Wire catalog abstractions: src/JXServer.GameAssets/Npcs/INpcTemplateCatalog.cs, src/JXServer.GameAssets/Players/IPlayerTemplateCatalog.cs`
     - **Observation**: Folders `src/JXServer.GameAssets/Npcs/` and `src/JXServer.GameAssets/Players/` do NOT exist. The actual folders are `src/JXServer.GameAssets/NpcTemplates/` and `src/JXServer.GameAssets/PlayerTemplates/`. Furthermore, there is no file named `PlayerTemplate.cs`; the player template classes in `PlayerTemplates/` are `CharacterBaseTemplate.cs`, `PlayerSeriesGrowthRuleTemplate.cs`, `StarterItemTemplate.cs`, etc.

### 1.4 Ground-Truth Fidelity Audit
- Verified 39/39 key invariants against `specs/_archive_m1_m4/` and `src/`:
  - 44-byte handshake obfuscation keys `0x2E6D2399` and `0x2E6D23CF` preserved in `002`.
  - Linear congruential XOR keys `0x3042` and `0x38BC` preserved in `002`.
  - Bishop opcodes `0x41`, `0x37`, `0x45`, `0x3A`, `0x64`, `0x34` preserved in `003`.
  - GameServer world entry `0x42` $\rightarrow$ `0x49/0x44/0x45/0x46/0x4C/0xA4/0x43` preserved in `004`.
  - Movement opcodes `0x4B`/`0x50`, `0x4C`/`0x51`, `0x4E`/`0x54`, `0x71`/`0x53` and anti-cheat check keys `0x24A1`/`0x1D78` preserved in `006`.
  - Portal packet `0x35` Little-Endian port `0x1A0A` (plain 6666) preserved in `007`.
  - Spatial inventory `0x5F` (80B) and item opcodes `0x5B`/`0x61`, `0x8E`, `0x85`, `0x59`, `0x5E`, `0x83` preserved in `008`.
  - Combat skill casts `0x4D`/`0x87`, aura `0x6F`, hurt recoil `0x56`, 64-direction trig tables preserved in `009`.
  - Progression allocations `0x57`/`0x5D`, `0x58`/`0x5E`, revive `0x74`/`0x89` preserved in `010`.
  - Ground object opcodes `0x64`/`0x67`, pickup `0x5A` preserved in `011`.
  - Multi-channel chat opcodes `0x5B`, `0x7E`, `0xFB` preserved in `016`.

### 1.5 Runtime Verification Output
- **Build**:
  ```powershell
  dotnet build JXServer.slnx
  ```
  Result: `Build succeeded. 0 Warning(s), 0 Error(s).`
- **Tests**:
  ```powershell
  dotnet test JXServer.slnx -- RunConfiguration.DisableParallelization=true
  ```
  Result:
  - `JXServer.Protocol.Tests.dll`: 59 passed, 0 failed, 0 skipped.
  - `JXServer.ZoneEngine.Tests.dll`: 58 passed, 0 failed, 0 skipped.
  - `JXServer.Integration.Tests.dll`: 22 passed, 0 failed, 0 skipped.
  - Total: **139 passed, 0 failed, 0 skipped** across all test suites.

---

## 2. Logic Chain

1. **Strict Forensic Standard**:
   - The dispatch mission and Integrity Forensics rules mandate: *"Audit tasks.md across all 12 specs: verify all tasks are marked [X] and map to real, existing .cs and *Tests.cs files in the codebase (zero fake/hallucinated file paths). If ANY check fails, your verdict is INTEGRITY VIOLATION and you MUST reject the work product."*
2. **Identification of Mismatches**:
   - While the implementation code compiles with 0 warnings and passes 139/139 tests, the task specifications in specs `001`, `004`, `006`, and `010` claim completion of specific file paths that do not exist on disk:
     - `data/loot/field-monsters.loot.json` (actual: `data/loot/droprates.loot.json`)
     - `CharacterTaskValueConsts.cs` (actual: inside `CharacterConsts.cs`)
     - `.WorldSink.cs`, `.CombatSink.cs`, etc. (actual: `GameServerClientSession.*Sink.cs`)
     - `src/JXServer.GameAssets/Obstacles/RegionObstacleData.cs` (actual: `src/JXServer.GameAssets/WorldMaps/RegionObstacleGrid.cs`)
     - `src/JXServer.GameAssets/Players/PlayerTemplate.cs` (actual: `CharacterBaseTemplate.cs` in `PlayerTemplates/`)
3. **Verdict Deduction**:
   - Claiming completion (`[X]`) on tasks referencing non-existent files constitutes an integrity mismatch between the SpecKit contract and the physical repository layout.
   - Per Integrity Forensics rules, this triggers an immediate `INTEGRITY VIOLATION` verdict and rejection of the Milestone M4 gate until corrected.

---

## 3. Caveats

- **Scope Boundary**: The auditor did NOT modify any files in `specs/` or `src/` to remain strictly within the audit-only constraint.
- **Genuine Implementation**: The underlying C# implementation is 100% genuine, clean, builds with 0 warnings, and passes all 139 tests. The violation is strictly confined to specification/task-mapping fidelity.

---

## 4. Conclusion & Required Remediation

### Verdict: **INTEGRITY VIOLATION**

The work product is rejected until the following 4 specifications are updated with accurate file paths matching the physical codebase:

1. **In `specs/001-core-game-data-pipeline/`**:
   - Update `tasks.md` (T013) and `plan.md`: change `data/loot/field-monsters.loot.json` to `data/loot/droprates.loot.json`.
2. **In `specs/004-core-zone-spine-and-world-entry/`**:
   - Update `tasks.md` (T015): change truncated `.WorldSink.cs`, `.CombatSink.cs`, etc. to `GameServerClientSession.WorldSink.cs`, `GameServerClientSession.CombatSink.cs`, `GameServerClientSession.InventorySink.cs`, `GameServerClientSession.ChatSink.cs`.
   - Update `tasks.md` (T016) and `plan.md`: remove reference to non-existent `CharacterTaskValueConsts.cs` and state that task bounds `MinTaskId` and `MaxTaskId` are defined in `src/JXServer.Primitives/Characters/CharacterConsts.cs`.
3. **In `specs/006-core-movement-and-obstacles/`**:
   - Update `spec.md`, `plan.md`, `research.md`, `data-model.md`, and `tasks.md` (T002): replace `src/JXServer.GameAssets/Obstacles/RegionObstacleData.cs` with `src/JXServer.GameAssets/WorldMaps/RegionObstacleGrid.cs`.
4. **In `specs/010-core-monster-ai-and-progression/`**:
   - Update `tasks.md` (T01): replace `src/JXServer.GameAssets/Npcs/NpcTemplate.cs` with `src/JXServer.GameAssets/NpcTemplates/NpcTemplate.cs`; replace `src/JXServer.GameAssets/Players/PlayerTemplate.cs` with `src/JXServer.GameAssets/PlayerTemplates/CharacterBaseTemplate.cs` and `PlayerTemplateCatalog.cs`; replace `src/JXServer.GameAssets/Npcs/INpcTemplateCatalog.cs` with `src/JXServer.GameAssets/NpcTemplates/INpcTemplateCatalog.cs`; replace `src/JXServer.GameAssets/Players/IPlayerTemplateCatalog.cs` with `src/JXServer.GameAssets/PlayerTemplates/IPlayerTemplateCatalog.cs`.

Once these 4 files are remediated, Milestone M4 will pass the forensic integrity audit with a `CLEAN` verdict.

---

## 5. Verification Method

To verify these findings independently:
```powershell
# 1. Verify existence of 106 spec files and 16/16 checklist
powershell -ExecutionPolicy Bypass -File d:\Projects\jx-server\.agents\teamwork\auditor_m4\audit_specs.ps1

# 2. Verify non-existent task file paths
powershell -ExecutionPolicy Bypass -File d:\Projects\jx-server\.agents\teamwork\auditor_m4\audit_unbackticked_cs.ps1

# 3. Verify ground truth fidelity
powershell -ExecutionPolicy Bypass -File d:\Projects\jx-server\.agents\teamwork\auditor_m4\verify_fidelity.ps1

# 4. Verify build & 139 tests
dotnet build d:\Projects\jx-server\JXServer.slnx
dotnet test d:\Projects\jx-server\JXServer.slnx -- RunConfiguration.DisableParallelization=true
```
