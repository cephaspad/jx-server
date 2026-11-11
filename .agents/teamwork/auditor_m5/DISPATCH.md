# DISPATCH — Forensic Auditor M5 (Final Forensic Integrity Audit)

## Mission
Perform comprehensive forensic integrity audit for Milestone M5 (Final Convergence, Teardown & Acceptance) of JXServer Zero-Regression Re-baseline (Phương án B):
1. **Archive Teardown Verification**: Confirm that `d:\Projects\jx-server\specs\_archive_m1_m4/` is completely removed from disk, and only the 12 active specification directories exist in `specs/`.
2. **SpecKit Configuration Integrity**: Verify `d:\Projects\jx-server\.specify\feature.json` contains `"feature_directory": "specs/016-module-chat-channels"` and validates against `.specify/scripts/powershell/check-prerequisites.ps1`.
3. **Spec Integrity & Convergence**:
   - Verify all 12 specs contain all required SpecKit artifacts (`spec.md`, `plan.md`, `tasks.md`, `data-model.md`, `research.md`, `quickstart.md`, `checklists/requirements.md`, `contracts/`).
   - Verify 0 unchecked `[ ]` tasks remain across all 12 `tasks.md` (all marked `[X]`).
   - Verify 16/16 `[x]` across all 12 `checklists/requirements.md`.
   - Verify 0 missing referenced code or test files across all tasks.
4. **Architecture & Clean Modular Spine Integrity**:
   - Verify `MapZoneWorker.cs` has 0 occurrences of "Chat" and delegates simulation phases (100-600) and domain commands via `_registeredTickSystemsArray` and `_extensionRegistry`.
   - Verify `ChatZoneModule.cs` is an authentic `IZoneModule` implementation.
   - Verify `src/JXServer.GameAssets` contains no legacy archive/ini readers (quarantined to `tools/JXServer.DataExporter/Legacy/`).
   - Verify no dummy, facade, or stub methods (`NotImplementedException`, fake returns).
5. **Compilation & Test Suite**:
   - Execute `dotnet build d:\Projects\jx-server\JXServer.slnx` -> verify 0 warnings, 0 errors (`TreatWarningsAsErrors=true`).
   - Execute `dotnet test d:\Projects\jx-server\JXServer.slnx -- RunConfiguration.DisableParallelization=true` -> verify 139/139 tests pass.
6. **Verdict**: Author handoff report at `d:\Projects\jx-server\.agents\teamwork\auditor_m5\handoff.md` with explicit verdict: `CLEAN` or `INTEGRITY VIOLATION`.

## Input Artifacts
- `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md` (read first)
- `d:\Projects\jx-server\.agents\teamwork\PROJECT.md`
- `d:\Projects\jx-server\.agents\teamwork\worker_m5_convergence\handoff.md`


## 2026-11-11T11:20:33Z
You are the Forensic Integrity Auditor for Milestone M5 of JXServer Zero-Regression Re-baseline.
Working directory: d:\Projects\jx-server
Your state directory: d:\Projects\jx-server\.agents\teamwork\auditor_m5\
Read your task instructions at d:\Projects\jx-server\.agents\teamwork\auditor_m5\DISPATCH.md.
Also read d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md, d:\Projects\jx-server\.agents\teamwork\PROJECT.md, and d:\Projects\jx-server\.agents\teamwork\worker_m5_convergence\handoff.md.

Perform forensic audit:
1. Verify complete teardown of specs/_archive_m1_m4/
2. Verify .specify/feature.json
3. Verify all 12 specs (001-011, 016) are genuine, complete, 0 pending tasks, 0 missing file paths
4. Verify architectural integrity: MapZoneWorker modular spine (0 chat references, phased tick systems), ChatZoneModule decoupled, runtime GameAssets clean of legacy readers, zero stubs/facades
5. Verify dotnet build JXServer.slnx (0 warnings, 0 errors) and dotnet test JXServer.slnx -- RunConfiguration.DisableParallelization=true (139/139 pass)
Deliver verdict CLEAN or INTEGRITY VIOLATION in handoff.md and send a completion message to your parent.
