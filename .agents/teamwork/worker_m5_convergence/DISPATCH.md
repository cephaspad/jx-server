# DISPATCH — Worker M5 (Convergence, Teardown & Final Acceptance)

## Context
- Milestone: M5 (Step 3 - Automated Acceptance, Convergence & Final Teardown)
- Original Request: `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md` (R1 & R4)
- Master Architecture: `d:\Projects\jx-server\.agents\teamwork\PROJECT.md`
- Master Roadmap: `d:\Projects\jx-server\docs\specs-roadmap.md`
- State Folder: `d:\Projects\jx-server\.agents\teamwork\worker_m5_convergence\`

## Objective
Execute the final convergence and teardown operations for the JXServer Zero-Regression Re-baseline:
1. **Automated Convergence & Acceptance Audit across all 12 Specs (`001`–`011`, `016`)**:
   - Evaluate each of the 12 specification directories under `specs/` against `/speckit-analyze` and `/speckit-converge` criteria:
     - Cross-artifact consistency across `spec.md`, `plan.md`, `tasks.md`, `data-model.md`.
     - Constitution v1.2.0 compliance check.
     - Requirements coverage: verify all `FR-###` and `SC-###` map to completed `[X]` tasks.
     - Codebase convergence: verify that all tasks map to existing `.cs` files and passing tests.
     - Document that 100% of all 12 specs achieve Converged status with zero remaining gaps.
2. **Teardown of Ground-Truth Archive (`specs/_archive_m1_m4/`)**:
   - Per requirements R1 & R4 in `ORIGINAL_REQUEST.md`: delete the temporary archive directory `d:\Projects\jx-server\specs\_archive_m1_m4/`.
   - Verify that `specs/` contains exclusively the 12 active specification directories: `001-core-game-data-pipeline` through `011-core-loot-and-item-generation`, and `016-module-chat-channels`.
3. **Update `.specify/feature.json`**:
   - Update `d:\Projects\jx-server\.specify\feature.json` to point to the Golden Reference Plug-in Module:
     ```json
     {
       "feature_directory": "specs/016-module-chat-channels"
     }
     ```
4. **Zero-Regression Build and Test Suite Verification**:
   - Execute `dotnet build JXServer.slnx` -> verify 0 warnings, 0 errors (`TreatWarningsAsErrors=true`).
   - Execute `dotnet test JXServer.slnx -- RunConfiguration.DisableParallelization=true` -> verify 139/139 tests pass (Protocol: 59, ZoneEngine: 58, Integration: 22; 0 failed, 0 skipped).
5. **Output**:
   - Author a comprehensive handoff report at `d:\Projects\jx-server\.agents\teamwork\worker_m5_convergence\handoff.md`.
   - Send completion message to orchestrator.

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations, tests, and convergence checks must be genuine. A forensic auditor and reviewer will independently verify your work.


## 2026-11-11T11:13:10Z
[Message] timestamp=2026-11-11T11:13:10Z sender=483c54f0-17a5-4a01-a852-4c57a8dfe829 priority=MESSAGE_PRIORITY_HIGH content=You are worker_m5_convergence. Read your dispatch instructions at d:\Projects\jx-server\.agents\teamwork\worker_m5_convergence\DISPATCH.md.
Also read:
- d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
- d:\Projects\jx-server\.agents\teamwork\PROJECT.md
- d:\Projects\jx-server\docs\specs-roadmap.md

Your tasks:
1. Conduct an automated convergence and acceptance analysis across all 12 specs under specs/ (001-011 and 016) adhering to the criteria in .agents/skills/speckit-analyze/SKILL.md and speckit-converge/SKILL.md. Verify that each spec satisfies the constitution, all requirements are covered, all tasks map to real C# code and tests, and all 12 specs achieve 100% Converged status with zero remaining tasks.
2. Delete the temporary directory d:\Projects\jx-server\specs\_archive_m1_m4/ per requirements R1 & R4.
3. Update d:\Projects\jx-server\.specify\feature.json to point to "specs/016-module-chat-channels".
4. Execute `dotnet build JXServer.slnx` (verify 0 warnings, 0 errors) and `dotnet test JXServer.slnx -- RunConfiguration.DisableParallelization=true` (verify 139/139 tests pass).
5. Write your complete handoff report to d:\Projects\jx-server\.agents\teamwork\worker_m5_convergence\handoff.md and notify me via send_message.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All convergence audits and test runs must be authentic and genuine. Integrity violations WILL be detected and your work WILL be rejected.
