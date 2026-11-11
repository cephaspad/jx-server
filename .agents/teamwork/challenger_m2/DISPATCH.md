# Task Assignment: Milestone M2 Adversarial Challenge

Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\challenger_m2\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Worker handoff path: d:\Projects\jx-server\.agents\teamwork\worker_m2\handoff.md

## Objective
Empirically and adversarially challenge Milestone M2:
1. Challenge the `ZoneSimulationHarness`:
   - Verify that 1,000 virtual frames execute under 5ms.
   - Test determinism with fixed vs different PRNG seeds.
   - Stress-test fault containment: inject faulting commands, tick systems, and feature slots, verify the zone never crashes and faulting sessions are isolated.
2. Challenge persistence and lease fencing:
   - Challenge `TaskValues`: boundary test taskId 0 and 65535, sparse zero-removal, JSON roundtrip.
   - Challenge `RepositoryPasswordHash`: non-hex string rejection, casing handling.
   - Challenge `SessionLeaseAppService` epoch fencing: concurrent eviction vs stale worker snapshot race.
3. Verify test suite: run `dotnet test JXServer.slnx` and confirm 100% pass.
4. Output your formal verdict (APPROVE or REQUEST_CHANGES) in `d:\Projects\jx-server\.agents\teamwork\challenger_m2\handoff.md`.


## 2026-11-11T08:08:42Z
You are the Adversarial Challenger for Milestone M2.
Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\challenger_m2\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md
Project plan path: d:\Projects\jx-server\.agents\teamwork\PROJECT.md
Worker handoff path: d:\Projects\jx-server\.agents\teamwork\worker_m2\handoff.md
Task assignment: d:\Projects\jx-server\.agents\teamwork\challenger_m2\DISPATCH.md

Empirically challenge the ZoneSimulationHarness (1000 frames < 5ms benchmark, seed determinism, fault containment), persistence boundaries (TaskValues, RepositoryPasswordHash, lease fencing), and test suite pass (105 tests). Write your verdict (APPROVE or REQUEST_CHANGES) in `d:\Projects\jx-server\.agents\teamwork\challenger_m2\handoff.md` and send a message.
