# Task Assignment: Specification & Architecture Survey

Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\spec_miner_survey\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md

## Objective
Read `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md` and thoroughly survey all authoritative specification documents:
- `docs/specs-roadmap.md`
- `specs/004-zone-modular-spine/contracts/` (`zone-modular-spine-and-harness.md`, `domain-outbound-sinks.md`, `character-persistence-contracts.md`)
- `.specify/memory/constitution.md` (v1.2.0)
- `docs/data-architecture-standard.md`
- Existing specs under `specs/001-gateway-world-entry`, `specs/002-spatial-aoi-movement-chat`, `specs/003-combat-missiles-loot-inventory`, `specs/004-zone-modular-spine`.

Enumerate the full specifications, contracts, entity extensions, wire protocol byte mappings, and mapping requirements for the 12 target spec profiles (001-011 and 016).
Produce a detailed handoff report in `d:\Projects\jx-server\.agents\teamwork\spec_miner_survey\handoff.md`.


## 2026-11-11T06:53:10Z
[Message] timestamp=2026-11-11T06:53:10Z sender=d2392fd8-ba50-46c2-b953-b7059df99b64 priority=MESSAGE_PRIORITY_HIGH content=You are the Specification & Architecture Surveyor.
Working directory: d:\Projects\jx-server
State folder: d:\Projects\jx-server\.agents\teamwork\spec_miner_survey\
Original request path: d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md

Task:
1. Read `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md`.
2. Read and investigate:
   - `docs/specs-roadmap.md`
   - `specs/004-zone-modular-spine/contracts/` (`zone-modular-spine-and-harness.md`, `domain-outbound-sinks.md`, `character-persistence-contracts.md`)
   - `.specify/memory/constitution.md` (v1.2.0)
   - `docs/data-architecture-standard.md`
   - All existing specifications under `specs/001-gateway-world-entry`, `specs/002-spatial-aoi-movement-chat`, `specs/003-combat-missiles-loot-inventory`, `specs/004-zone-modular-spine`.
3. Map out:
   - All required features, constraints, formulas, wire protocol byte mappings, and dependencies.
   - The exact mapping of requirements into the 12 target spec profiles:
     1. specs/001-core-game-data-pipeline
     2. specs/002-core-legacy-wire-protocol
     3. specs/003-core-auth-roster-and-leases
     4. specs/004-core-zone-spine-and-world-entry
     5. specs/005-core-spatial-aoi-and-spawns
     6. specs/006-core-movement-and-obstacles
     7. specs/007-core-portals-and-map-handoff
     8. specs/008-core-inventory-and-equipment
     9. specs/009-core-combat-and-missiles
     10. specs/010-core-monster-ai-and-progression
     11. specs/011-core-loot-and-item-generation
     12. specs/016-module-chat-channels
   - Ground truth preservation plan for moving existing specs to `specs/_archive_m1_m4/` and how to retain 100% of bytes, test vectors, and clarifications.
4. Output your comprehensive findings and evidence into `d:\Projects\jx-server\.agents\teamwork\spec_miner_survey\handoff.md` and send a completion message with summary.
