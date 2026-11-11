# DISPATCH — Forensic Auditor (Milestone M4 Gate)

## Context
- Milestone: M4 (Step 2 - 12 SpecKit Standardized Profiles Extraction)
- Target: Forensic audit of all 12 SpecKit profile directories under `specs/` (001-011 and 016).
- Worker Reports:
  - `d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_a\handoff.md`
  - `d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_b\handoff.md`
  - `d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_c\handoff.md`
- Original Request: `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md`
- Master Roadmap: `d:\Projects\jx-server\docs\specs-roadmap.md`
- Ground Truth Archive: `d:\Projects\jx-server\specs\_archive_m1_m4/`
- State Folder: `d:\Projects\jx-server\.agents\teamwork\auditor_m4\`

## Objective
Perform forensic integrity verification of all Milestone M4 deliverables:
1. Static analysis & forensic inspection:
   - Verify that all 12 spec directories (001-011 and 016) exist under `d:\Projects\jx-server\specs\`.
   - Verify that each directory contains the required 8 SpecKit files (`spec.md`, `checklists/requirements.md` (16/16 `[x]`), `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `contracts/*.md`, `tasks.md`).
   - Audit tasks integrity: verify that `tasks.md` files contain genuine tasks marked `[X]` mapping to real, existing `.cs` source and `*Tests.cs` test files in the codebase (no fake file paths).
   - Audit ground-truth fidelity: verify that protocol packet layouts, hex opcodes, Little-Endian offsets, and C++ formulas match the ground-truth in `specs/_archive_m1_m4/` without dropped bytes or hallucinated values.
   - Audit checklists integrity: verify that `checklists/requirements.md` has 16/16 `[x]` across all 12 specs.
   - Check for any integrity violations: dummy facades, empty stub files, fabricated test vectors, cheat patterns.
2. Runtime verification:
   - Run `dotnet build JXServer.slnx` -> verify 0 warnings, 0 errors.
   - Run `dotnet test JXServer.slnx -- RunConfiguration.DisableParallelization=true` -> verify 139/139 tests pass legitimately.
3. Issue binary verdict:
   - `CLEAN` or `INTEGRITY VIOLATION`.
   - Write full audit report to `d:\Projects\jx-server\.agents\teamwork\auditor_m4\handoff.md`.


## 2026-11-11T10:29:46Z
You are auditor_m4. Read your dispatch instructions at d:\Projects\jx-server\.agents\teamwork\auditor_m4\DISPATCH.md.
Also read:
- d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
- d:\Projects\jx-server\.agents\teamwork\PROJECT.md
- d:\Projects\jx-server\docs\specs-roadmap.md
- d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_a\handoff.md
- d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_b\handoff.md
- d:\Projects\jx-server\.agents\teamwork\worker_m4_batch_c\handoff.md

Conduct a forensic integrity audit across all 12 SpecKit profile directories under `d:\Projects\jx-server\specs\` (001-011 and 016):
1. Static analysis & forensic inspection:
   - Verify all 12 spec directories exist under specs/ and each contains all 8 SpecKit files.
   - Verify checklists/requirements.md has 16/16 [x] across all 12 specs.
   - Audit tasks.md across all 12 specs: verify all tasks are marked [X] and map to real, existing .cs and *Tests.cs files in the codebase (zero fake/hallucinated file paths).
   - Audit ground-truth fidelity: verify byte-exact preservation of packet opcodes, payload sizes, Little-Endian formats, and C++ Kingsoft formulas against specs/_archive_m1_m4/.
   - Detect cheat patterns: empty files, dummy facades, hardcoded shortcuts, truncated sections.
2. Runtime verification:
   - Run `dotnet build JXServer.slnx` -> verify 0 warnings, 0 errors.
   - Run `dotnet test JXServer.slnx -- RunConfiguration.DisableParallelization=true` -> verify 139/139 tests pass.
3. Verdict:
   - Provide binary verdict: CLEAN or INTEGRITY VIOLATION.
   - Write full audit report to d:\Projects\jx-server\.agents\teamwork\auditor_m4\handoff.md.
When complete, notify me with your verdict and findings via send_message.
