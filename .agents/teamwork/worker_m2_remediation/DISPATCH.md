# Task Assignment: Milestone M2 Audit Remediation Implementation

Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\worker_m2_remediation\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Auditor full evidence report: d:\Projects\jx-server\.agents\teamwork\auditor_m2\handoff.md
Remediation blueprints:
- d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_1\handoff.md
- d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_2\handoff.md
- d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_3\handoff.md

## Objective
Execute the remediation blueprint to resolve the forensic integrity violation:

### 1. Fix `CharacterItem.cs`
In `src/JXServer.Domain/Characters/CharacterItem.cs`:
In the constructor, assign the missing properties:
```csharp
IsBroken = isBroken;
EnchaseLevel = enchaseLevel;
```

### 2. Implement Automated Regression Tests
Implement the comprehensive test suites designed by the remediation explorers:
1. `tests/JXServer.ZoneEngine.Tests/Items/CharacterItemUnitTests.cs`:
   - `Constructor_ShouldAssign_IsBrokenAndEnchaseLevel_Correctly`
   - `Constructor_WhenEnchaseLevelExceedsMax_ShouldThrowArgumentOutOfRangeException`
   - `SetDurability_WhenDurabilityZero_ShouldMarkIsBroken`
   - `SetEnchaseLevel_ShouldUpdateEnchaseLevel_AndThrowOnOverflow`
2. `tests/JXServer.Integration.Tests/CharacterItemPersistenceIntegrationTests.cs`:
   - `CharacterItem_WithIsBrokenAndEnchaseLevel_ShouldRoundtripThroughDatabase`
   - `ApplyWorldSnapshot_WithBrokenAndEnchasedItems_ShouldPersistCorrectlyAcrossEvictions`

### 3. Verification
- `dotnet build JXServer.slnx`: 0 warnings, `TreatWarningsAsErrors=true`.
- `dotnet test JXServer.slnx`: all tests pass with 100% pass rate.
- Write a complete handoff report to `d:\Projects\jx-server\.agents\teamwork\worker_m2_remediation\handoff.md`.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.


## 2026-11-11T08:38:34Z
You are the Remediation Implementation Worker for Milestone M2.
Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\worker_m2_remediation\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Task assignment: d:\Projects\jx-server\.agents\teamwork\worker_m2_remediation\DISPATCH.md
Auditor full evidence report: d:\Projects\jx-server\.agents\teamwork\auditor_m2\handoff.md
Remediation blueprints:
- d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_1\handoff.md
- d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_2\handoff.md
- d:\Projects\jx-server\.agents\teamwork\explorer_m2_remediation_3\handoff.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

Your Task:
1. In `src/JXServer.Domain/Characters/CharacterItem.cs`, add the missing assignments in constructor:
   `IsBroken = isBroken;`
   `EnchaseLevel = enchaseLevel;`
2. Implement automated regression tests:
   - Unit tests in `tests/JXServer.ZoneEngine.Tests/Items/CharacterItemUnitTests.cs` (or appropriate test project).
   - EF Core persistence integration tests in `tests/JXServer.Integration.Tests/CharacterItemPersistenceIntegrationTests.cs`.
3. Verify `dotnet build JXServer.slnx` (0 warnings).
4. Verify `dotnet test JXServer.slnx` (all tests pass).
5. Write your report to `d:\Projects\jx-server\.agents\teamwork\worker_m2_remediation\handoff.md` and send a message.
