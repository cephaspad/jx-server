# Project: JXServer Zero-Regression Re-baseline (Phương án B)

## Architecture
- **Layering**: Clean Architecture & DDD (`Primitives` -> `Domain` -> `Contracts` -> `Application` -> `GameAssets` -> `ZoneEngine` -> `Adapter.LegacyJX1` -> `Host`).
- **Zone Modular Spine**: Single-threaded 18-Hz shard loop driven by `MapZoneWorker` hosting decoupled `IZoneModule`, `IZoneCommandHandler<T>`, `IZoneTickSystem` phases (100..600), `PlayerFeatureSlotKey<T>`, and domain-segregated sinks (`IWorldOutboundSink`, `ICombatOutboundSink`, `IInventoryOutboundSink`, `IChatOutboundSink`).
- **Golden Reference Plug-in**: `ChatZoneModule : IZoneModule` dynamically registered via `ZoneExtensionRegistry`.
- **Dual-Tier Data Architecture**: JSON canonical authoring (`data/**/*.json`) + zero-copy `MemoryPack` binary delivery snapshots (`artifacts/*.pack`), zero legacy file readers in runtime `JXServer.GameAssets`.
- **E2E Deterministic Harness**: `ZoneSimulationHarness` stepping 1,000 frames under 5ms with `FakeTimeProvider` and fixed PRNG seed.

## Feature Inventory
| # | Feature | Category | Milestone | Source |
|---|---------|----------|-----------|--------|
| 1 | JSON Schema 2020-12 Contracts | Data Pipeline | M3 | survey |
| 2 | Dual-Tier MemoryPack Snapshots | Data Pipeline | M3 | survey |
| 3 | Immutable Catalogs & Dual IDs | Data Pipeline | M3 | survey |
| 4 | Standalone Offline Exporter CLI | Data Pipeline | M3 | survey |
| 5 | 2-Byte Length-Prefixed Framing | Wire Transport | M2 | survey |
| 6 | 44-Byte Handshake & Key Obfuscation | Wire Transport | M2 | survey |
| 7 | Rolling KSG_DecodeEncode Stream Cipher | Wire Transport | M2 | survey |
| 8 | Second-Layer XOR Obfuscators | Wire Transport | M2 | survey |
| 9 | Boundary Encodings Transcoding | Wire Transport | M2 | survey |
| 10 | Account Login (0x41) | Auth & Leases | M2 | survey |
| 11 | Character Roster Query (0x37) | Auth & Leases | M2 | survey |
| 12 | Character Creation (0x45) | Auth & Leases | M2 | survey |
| 13 | Character Deletion (0x3A) | Auth & Leases | M2 | survey |
| 14 | Character Selection (0x64) | Auth & Leases | M2 | survey |
| 15 | Monotonic Lease-Epoch Fencing | Auth & Leases | M2 | survey |
| 16 | Single-Use World Handoff Ticket | Auth & Leases | M2 | survey |
| 17 | Modular Command Routing | Zone Spine | M2 | survey |
| 18 | Phased 18-Hz Tick Pipeline | Zone Spine | M2 | survey |
| 19 | Per-Player Feature State Slots | Zone Spine | M2 | survey |
| 20 | Per-Session Fault Isolation | Zone Spine | M2 | survey |
| 21 | Domain-Segregated Outbound Sinks | Zone Spine | M2 | survey |
| 22 | Foundational Persistence Fields (TaskValues, etc.) | Zone Spine | M2 | survey |
| 23 | World Entry Handshake (0x42..0x43) | Zone Spine | M2 | survey |
| 24 | Continuous Heartbeat Pings | Zone Spine | M2 | survey |
| 25 | Deterministic ZoneSimulationHarness | Zone Spine | M2 | survey |
| 26 | Spatial Region Grid Partitioning | Spatial AOI | M3 | survey |
| 27 | 3x3 Region AOI Visibility | Spatial AOI | M3 | survey |
| 28 | Entity Compact Sync (0x4D/0x4E) | Spatial AOI | M3 | survey |
| 29 | On-Demand Entity Detail Queries | Spatial AOI | M3 | survey |
| 30 | Static NPC & Monster Spawning | Spatial AOI | M3 | survey |
| 31 | Movement Anti-Cheat Verification | Movement | M3 | survey |
| 32 | Authoritative Walk (0x4B -> 0x50) | Movement | M3 | survey |
| 33 | Authoritative Run (0x4C -> 0x51) | Movement | M3 | survey |
| 34 | Player Jump (0x4E -> 0x54) | Movement | M3 | survey |
| 35 | Posture & Sitting Regen (0x71 -> 0x53) | Movement | M3 | survey |
| 36 | Edge-Triggered Cell Trap Detection | Portals | M3 | survey |
| 37 | Town-Gate FightMode Toggles | Portals | M3 | survey |
| 38 | Same-Node Map Handoff (NewWorld) | Portals | M3 | survey |
| 39 | Cross-Node Map Redirect (0x35) | Portals | M3 | survey |
| 40 | Level-Gate Rejection & Bounce | Portals | M3 | survey |
| 41 | Spatial Container Grids | Inventory | M3 | survey |
| 42 | Item Move & Swap (0x5B -> 0x61 + 0x8E) | Inventory | M3 | survey |
| 43 | Item Stack Splitting (0x85) | Inventory | M3 | survey |
| 44 | Equipping & Five-Elements Affixes | Inventory | M3 | survey |
| 45 | Medicine Consumption (0x59) | Inventory | M3 | survey |
| 46 | Martial Skill Cast (0x4D / 0x87) | Combat | M3 | survey |
| 47 | Aura Skill Toggle (0x6F) | Combat | M3 | survey |
| 48 | 64-Direction Missile Simulation | Combat | M3 | survey |
| 49 | Five-Elements Damage & Resistances | Combat | M3 | survey |
| 50 | Status Effects & Hurt Recoil | Combat | M3 | survey |
| 51 | 18-Hz Monster AI State Machine | Monster AI | M3 | survey |
| 52 | Progression & Allocation | Progression | M3 | survey |
| 53 | KItemGenerator Blue Affix Rolling & Drops | Loot & Items | M3 | survey |
| 54 | Golden Reference Plug-in ChatZoneModule (016) | Chat Module | M3 | survey |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Ground-Truth Archival (Step 0) | Move existing specs 001-004 to specs/_archive_m1_m4/ | none | DONE |
| M2 | Modular Spine, Domain Sinks, Persistence & Harness (Step 1 Core) | Implement IZoneModule spine, 4 domain sinks, 4 character/item persistence properties, lease fencing, ZoneSimulationHarness | M1 | DONE |
| M3 | MapZoneWorker Deconstruction, ChatModule & Assets Cleanup (Step 1 Complete) | Deconstruct MapZoneWorker into domain handlers and tick systems; decouple ChatZoneModule; cleanup legacy file readers | M2 | DONE |
| M4 | 12 SpecKit Standardized Profiles Extraction (Step 2) | Generate specs 001-011 and 016 with spec.md, plan.md, checklists, contracts, data-model, quickstart, tasks.md | M3 | DONE |
| M5 | Automated Acceptance, Convergence & Final Teardown (Step 3) | Run /speckit-analyze & /speckit-converge; zero-regression verification; delete _archive_m1_m4; update feature.json | M4 | DONE |

## Interface Contracts
### Core Modular Spine
- `IZoneModule`: registers command handlers, tick systems, and feature slots via `IZoneExtensionBuilder`.
- `IZoneCommandHandler<TCommand>`: zero-boxing async handler with `ValueTask HandleAsync(TCommand command, ZoneExecutionContext context, CancellationToken cancellationToken)`.
- `IZoneTickSystem`: phased execution with `ZoneTickPhase` (StatusEffects=100, MonsterAi=200, Missiles=300, GroundObjects=400, MovementAndPosture=500, SessionHeartbeats=600).
- `PlayerFeatureSlotKey<TSlot>`: typed O(1) state attachment on `PlayerZoneState`.

### Domain Outbound Notification Sinks
- `IWorldOutboundSink`: 14 methods for world entry, sync bursts, vitals, postures, and player presence.
- `ICombatOutboundSink`: 10 methods for skill casting, damage numbers, status effects, and hurt recoil.
- `IInventoryOutboundSink`: 7 methods for container sync, item move, equip state, and durability updates.
- `IChatOutboundSink`: 3 methods for local speech (`0x5B`), structured chat (`0x7E`), and system notices (`0xFB`).
- All sinks route into `GameServerClientSession`'s single FIFO `_outboundChannel`.

### Entity Persistence Extensions
- `Character.TaskValues`: sparse map `Dictionary<int, int>` serialized to `TaskValuesJson` column.
- `Character.RepositoryPasswordHash`: MD5 hex string.
- `CharacterItem.IsBroken`: mutable boolean.
- `CharacterItem.EnchaseLevel`: integer 0..10.
- `SessionLeaseAppService`: epoch-fenced eviction prior to database snapshot commit.

## Code Layout
- `src/JXServer.Domain/Characters/`: `Character.cs`, `CharacterItem.cs`, `CharacterTaskValueConsts.cs`.
- `src/JXServer.Infrastructure/EntityFrameworkCore/`: `JXServerDbContext.cs` entity configurations.
- `src/JXServer.Application/SessionLeases/`: `SessionLeaseAppService.cs` fencing.
- `src/JXServer.ZoneEngine/WorldZones/`: `IZoneModule.cs`, `IZoneCommandHandler.cs`, `IZoneTickSystem.cs`, `ZoneTickPhase.cs`, `ZoneExecutionContext.cs`, `ZoneAssetServices.cs`, `ZoneExtensionBuilder.cs`, `ZoneExtensionRegistry.cs`, `CoreZoneModule.cs`, `MapZoneWorker.cs`.
- `src/JXServer.ZoneEngine/PlayerSessions/`: `IWorldOutboundSink.cs`, `ICombatOutboundSink.cs`, `IInventoryOutboundSink.cs`, `IChatOutboundSink.cs`, `IPlayerOutboundSink.cs`, `PlayerFeatureSlotKey.cs`, `IPlayerFeatureSlot.cs`.
- `src/JXServer.ZoneEngine/Chats/`: `ChatZoneModule.cs`, `ChatCommandHandler.cs`.
- `src/JXServer.Adapter.LegacyJX1/GameServer/`: `GameServerClientSession.cs`, `GameServerClientSession.*Sink.cs`.
- `tools/JXServer.DataExporter/`: legacy asset parsing and canonical export tools.
- `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/`: `ZoneSimulationHarness.cs`, `PlayerOutboundEventRecorder.cs`, `SimulationHarnessTests.cs`.
- `specs/001-core-game-data-pipeline/` .. `specs/011-core-loot-and-item-generation/`, `specs/016-module-chat-channels/`.
