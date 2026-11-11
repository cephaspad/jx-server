# Task Assignment: Forensic Audit Remediation Exploration (Explorer 1)

Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_1\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Auditor full evidence report path: d:\Projects\jx-server\.agents\teamwork\auditor_m2\handoff.md

## Objective
Read `d:\Projects\jx-server\.agents\teamwork\auditor_m2\handoff.md` in full. A FORENSIC AUDIT FAILURE (INTEGRITY VIOLATION) was detected:
- In `src/JXServer.Domain/Characters/CharacterItem.cs`, constructor parameters `bool isBroken = false, byte enchaseLevel = 0` are validated but omitted from property assignment, causing silent data reset during persistence (`CharacterAppService.ApplyWorldSnapshot` and `EfCoreCharacterRepository.UpdateAsync`).
- Lack of test coverage for `CharacterItem.IsBroken` and `EnchaseLevel` persistence roundtripping allowed this defect to pass unnoticed behind 105 passing tests.

Your Task (Read-Only Exploration):
1. Read `auditor_m2/handoff.md` and examine `src/JXServer.Domain/Characters/CharacterItem.cs`.
2. Inspect `src/JXServer.Application/Characters/CharacterAppService.cs`, `src/JXServer.Infrastructure/Characters/EfCoreCharacterRepository.cs`, and `src/JXServer.Infrastructure/EntityFrameworkCore/JXServerDbContext.cs` to identify all affected instantiation and persistence paths.
3. Formulate a comprehensive, non-circumventing remediation strategy:
   - Exact constructor property assignments required in `CharacterItem.cs`.
   - Comprehensive test design: unit tests in `Domain.Tests` / `ZoneEngine.Tests` and integration persistence tests in `Integration.Tests` to verify `IsBroken` and `EnchaseLevel` are genuinely preserved through DTO conversion, EF Core saving, and SQLite/PostgreSQL loading.
4. Output your detailed remediation blueprint in `d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_1\handoff.md`.


## 2026-11-11T08:22:59Z
You are Explorer 1 for Forensic Audit Remediation.
Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_1\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Auditor full evidence report path: d:\Projects\jx-server\.agents\teamwork\auditor_m2\handoff.md
Task assignment: d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_1\DISPATCH.md

Read the auditor's full evidence report. Investigate `CharacterItem.cs` constructor omission and all persistence call sites (`CharacterAppService`, `EfCoreCharacterRepository`, `JXServerDbContext`). Propose the exact property assignment fix and persistence test requirements. Do NOT implement code directly. Write your handoff to `d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_1\handoff.md` and send a message.
