# Task Assignment: Forensic Audit Remediation Exploration (Explorer 3)

Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_3\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Auditor full evidence report path: d:\Projects\jx-server\.agents\teamwork\auditor_m2\handoff.md

## Objective
Read `d:\Projects\jx-server\.agents\teamwork\auditor_m2\handoff.md` in full. A FORENSIC AUDIT FAILURE (INTEGRITY VIOLATION) was detected:
- In `src/JXServer.Domain/Characters/CharacterItem.cs`, constructor parameters `bool isBroken = false, byte enchaseLevel = 0` are validated but omitted from property assignment, causing silent data reset during persistence (`CharacterAppService.ApplyWorldSnapshot` and `EfCoreCharacterRepository.UpdateAsync`).
- Lack of test coverage for `CharacterItem.IsBroken` and `EnchaseLevel` persistence roundtripping allowed this defect to pass unnoticed behind 105 passing tests.

Your Task (Read-Only Exploration):
1. Design the exact test suite to be added in `tests/JXServer.ZoneEngine.Tests/` and/or `tests/JXServer.Integration.Tests/` to permanently eliminate regressions in item persistence:
   - Unit tests for `CharacterItem` constructor, property mutators, durability zero-detection.
   - EF Core database persistence roundtrip tests for `CharacterItem` with `IsBroken = true` and `EnchaseLevel = 1..10`.
   - Snapshot serialization and deserialization parity tests between `PlayerItemSlot`, `CharacterItemDto`, and `CharacterItem`.
2. Output your test and verification blueprint in `d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_3\handoff.md`.


## 2026-11-11T08:23:13Z
You are Explorer 3 for Forensic Audit Remediation.
Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_3\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Auditor full evidence report path: d:\Projects\jx-server\.agents\teamwork\auditor_m2\handoff.md
Task assignment: d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_3\DISPATCH.md

Read the auditor's full evidence report. Design comprehensive automated regression tests to verify that `CharacterItem.IsBroken` and `EnchaseLevel` are properly set and persisted through EF Core and snapshots. Do NOT implement code directly. Write your handoff to `d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_3\handoff.md` and send a message.
