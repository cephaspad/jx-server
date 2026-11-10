# Implementation Plan: Five-Elements Combat, 64-Direction Missiles, Monster AI, Loot Generation & Inventory Management

**Branch**: `003-combat-missiles-loot-inventory` | **Date**: 2026-11-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-combat-missiles-loot-inventory/spec.md`

## Summary

Deliver **Milestone 3 (`003-combat-missiles-loot-inventory`)** of the modern `.NET 10` (`net10.0`, C# 14) Võ Lâm Truyền Kỳ server (`JXServer`) in full compliance with **Constitution v1.2.0** and **`JXData Standard v1`** (`docs/data-architecture-standard.md`):
1. **Dual-Tier Canonical Game Data (`JXData Standard v1`) & Offline Ground-Truth Exporter (`tools/JXServer.DataExporter` & `JXServer.GameAssets`)**: Extract legacy maps, player progression tables, NPC/monster templates (with evaluated `npclevelscript/*.lua` curves), skill/missile catalogs (with evaluated `script/skill/**/*.lua` 3-parameter curves), item/magic-affix catalogs (`settings/item/004/*.txt`, `m_wVersion = 4`), and monster drop tables (`settings/droprate/*.ini`) from `jx-linux-bin/server1` into schema-validated UTF-8 JSON documents under `data/` (`maps/`, `npcs/`, `players/`, `skills/`, `items/`, `loot/`) and pre-compiled `MemoryPack` snapshots (`artifacts/*.pack`). Transition `JXServer.GameAssets` to load exclusively from `JXData Standard v1` with zero runtime dependency on `.txt`/`.ini`/`.dat`/`.lua` files.
2. **Five-Elements Skill Casting, 64-Direction Missiles & Combat Engine (`JXServer.ZoneEngine` & `JXServer.Primitives`)**: Implement the exact 64-direction fixed-point trigonometric tables (`SixtyFourDirectionTrigTable` from `KMath.cpp`), process `0x4D` (`c2s_npcskill` with `SkillCheckKey = 0x3AB9`), `0x87` (`c2s_autoattacknpc`), `0x6F` (`c2s_changeauraskill`), and `0x76` (`c2s_pkapplychangenormalflag`), step pooled `ActiveMissileSlot` projectiles on the 18-Hz tick loop (`MissileSimulationTickSystem`), evaluate `KNpcSet::GetRelation` and `KNpc::CheckHitTarget` / `ReceiveDamage` / `CalcDamage` across all 6 damage channels (`Physics`, `Cold`, `Fire`, `Light`, `Poison`, `Magic`) with Cold slow, Poison DoT blending, and Lightning stun, and broadcast `0x5A` (`s2c_skillcast`), `0x85` (`s2c_castskilldirectly`), and `0x56` (`s2c_broadcasenetcommand` `do_hurt`).
3. **Monster AI State Machine, Experience/Leveling & Revival (`JXServer.ZoneEngine`)**: Step field monsters (`MonsterAiTickSystem`) deterministically through `IdlePatrol` $\rightarrow$ `Pursuit` $\rightarrow$ `Attack` $\rightarrow$ `LeashReturn` $\rightarrow$ `DeadRespawnWait`, broadcast `0x57` (`s2c_npcdeath`), award experience (`0x59` `s2c_playerexp`) and level-ups (`0x5B` `s2c_playerlevelup` granting `+5` attribute points and `+1` skill point per level), process base attribute allocation (`0x57` $\rightarrow$ `0x5D`) and skill point allocation (`0x58` $\rightarrow$ `0x5E`), and handle player death and town revival (`0x74` `c2s_playerrevive` $\rightarrow$ `0x89` `s2c_npcrevive`).
4. **6-Affix Blue Equipment Generation, Ground Loot & `6x10` Inventory Management (`JXServer.GameAssets`, `JXServer.ZoneEngine`, `JXServer.Domain`, `JXServer.Infrastructure`)**: Implement `ItemGenerator` (`KItemGenerator::Gen_Equipment` & `Gen_MagicAttrib` with `m_wVersion = 4`) rolling up to 3 explicit and 3 hidden Five-Elements magic affixes, spawn regional `GroundObjectSlot` drops (`0x64` `s2c_objadd`, `0x67` `s2c_objremove`, `0x4A`, `0x72`) with killer ownership protection, manage each player's `PlayerInventoryGrid` (`pos_hand`, `pos_equip`, `pos_equiproom` `6x10`, `pos_immediacy` belt) with `KItemList::ms_ActivedEquip` Five-Elements hidden affix activation, process pickup (`0x5A`), move/equip (`0x5B` $\rightarrow$ `0x61` + `0x8E`), consume medicine/Town Portal (`0x59` $\rightarrow$ `0x60`), drop (`0x5E`, `0x83`), and split (`0x85`), and persist `Character` progression, skills, gold, and `CharacterItem` entities via EF Core 10 under lease-epoch fencing.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (`net10.0`), XML solution (`JXServer.slnx`), `Directory.Build.props` (`Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `AllowUnsafeBlocks=true`), `Directory.Packages.props` (Central Package Management).

**Primary Dependencies**:
- `Microsoft.Extensions.Hosting` (`10.0.0`)
- `System.IO.Pipelines` (`10.0.0`)
- `System.Text.Encoding.CodePages` (`10.0.0`)
- `MemoryPack` (`1.21.4` — for Tier 2 zero-copy runtime asset snapshots `artifacts/*.pack`)
- `Microsoft.EntityFrameworkCore.SqlServer` (`10.0.0`) & `Microsoft.EntityFrameworkCore.Sqlite` (`10.0.0`)
- `Microsoft.Extensions.TimeProvider.Testing` (`10.0.0`) & `xunit.v3` (`2.0.0`)

**Storage**:
- Relational database via EF Core 10 (**Microsoft SQL Server** for production; **SQLite WAL / InMemory** for local development and automated tests) storing `Account`, `Character`, and `CharacterItem` tables.
- Dual-Tier static game data (`JXData Standard v1`): canonical UTF-8 JSON documents under `data/**/*.json` validated against `schemas/*.schema.json` + compiled `MemoryPack` binary snapshots under `artifacts/*.pack`.

**Testing**: `xunit.v3` unit tests, JSON schema & `MemoryPack` snapshot validation tests, protocol byte-symmetry tests, deterministic `FakeTimeProvider` 18-Hz combat/missile/AI/inventory simulation tests, and end-to-end TCP integration tests (plus live 50-bot `JXSwarm.Drone.Host` validation).

**Target Platform**: Cross-platform .NET 10 Server (Windows Server / Windows 11 & Linux x64/ARM64).

**Project Type**: Modular Monolith to Distributed Cluster MMORPG Server (`JXServer.slnx`) + Offline Data Exporter CLI (`tools/JXServer.DataExporter`).

**Performance Goals**:
- Maintain `18-Hz` (`~55.5ms` tick) simulation loop with 99th-percentile tick duration `< 15ms` under 50 concurrent hunting/casting/looting bots and active monsters/projectiles in Map `53`.
- Sub-millisecond zero-allocation catalog lookup from `FrozenDictionary` / `MemoryPack` snapshots.

**Constraints**:
- Zero compiler warnings (`-warnaserror`), strict `One-Type-Per-File` rule, pluralized feature folders, sealed concrete classes, ABP DDD role suffixes, and the 5 Real-Time 18-Hz Game Server & `JXData Standard v1` naming conventions (`*C2SPacket`/`*S2CPacket`, `*ZoneState`/`*Slot`/`*ZoneCommand`/`*TickSystem`, unit-explicit `*MpsX`/`*CellX`/`*RegionX`/`*Frames`, `*Template`/`I*Catalog` with dual `Id` + `Code`, `*LogMessages`).
- Zero runtime parsing of legacy `.txt`/`.ini`/`.dat`/`.lua` files inside `JXServer.GameAssets` or `JXServer.ZoneEngine`.
- Zero blocking I/O or `DbContext` calls inside the 18-Hz `MapZoneWorker` tick thread.

**Scale/Scope**: Milestone 3 covers `JXData Standard v1` migration (`tools/JXServer.DataExporter` + `data/**/*.json` + `artifacts/*.pack`), Five-Elements skill execution, 64-direction missiles, monster AI & respawns, experience/leveling, attribute/skill allocation, player death/revival, 6-affix Blue equipment loot generation (`m_wVersion = 4`), ground objects, and `6x10` player inventory management (`0x4A`, `0x4D`, `0x56`, `0x57`, `0x58`, `0x59`, `0x5A`, `0x5B`, `0x5D`, `0x5E`, `0x5F`, `0x60`, `0x61`, `0x64`, `0x67`, `0x6F`, `0x72`, `0x74`, `0x76`, `0x83`, `0x85`, `0x87`, `0x89`, `0x8E`).

## Constitution Check

*GATE: Passed before Phase 0 research and re-verified after Phase 1 design against Constitution `v1.2.0`.*

| Constitution Principle / Gate | Status | Verification Evidence in Design |
| :--- | :--- | :--- |
| **I. Hybrid Distributed Control Plane & Sharded 18-Hz Zone Architecture** | **PASS** | Each `MapZoneWorker` owns its pooled `ActiveMissileSlot` array, `MonsterAiTickSystem`, `GroundObjectSlot` region lists, and `PlayerInventoryGrid` instances on a single-threaded lock-free 18-Hz loop; persistence runs asynchronously via `ICharacterAppService`. |
| **II. Multi-Protocol Transport Abstraction** | **PASS** | All 30 C2S/S2C binary packet structs (`0x4D`, `0x5F`, `0x64`, `0x87`, etc.) and `TCVN3` transcoding are isolated inside `JXServer.Adapter.LegacyJX1`, communicating with `JXServer.ZoneEngine` strictly through `IZoneCommand` and `IPlayerOutboundSink`. |
| **III. Zero-Hallucination Evidence & Ground-Truth Fidelity** | **PASS** | 64-direction `g_nSin`/`g_nCos` tables (`KMath.cpp`), `KNpc::CheckHitTarget`/`ReceiveDamage`/`CalcDamage` (`KNpc.cpp`), `KNpcSet::GetRelation` (`KNpcSet.cpp`), `KItemGenerator::Gen_Equipment`/`Gen_MagicAttrib` (`KItemGenerator.cpp`), and `KItemList::ms_ActivedEquip` (`KItemList.cpp`) are verified in [research.md](research.md) and [contracts/](contracts/). |
| **IV. Zero-Dupe Economy & Lease-Fenced Session Integrity** | **PASS** | Every `CharacterItem` has a globally unique `Guid Id`; ground object pickups execute sequentially on the owning `MapZoneWorker`'s single-threaded 18-Hz loop; all commands and `UpdateCharacterWorldSnapshotDto` saves enforce monotonic `LeaseEpoch` fencing. |
| **V. UTF-8 Domain Core, Dual-Tier Game Data Standard & Boundary-Only Transcoding** | **PASS** | `tools/JXServer.DataExporter` exports canonical UTF-8 `data/**/*.json` (validated against `schemas/*.schema.json`) and `MemoryPack` `artifacts/*.pack` snapshots; `JXServer.GameAssets` loads exclusively from `JXData Standard v1`. |
| **VI. Clean Modular Boundaries, ABP DDD Role Separation & C# 14 Fluent Scripting** | **PASS** | Skill scaling curves and monster stat growth curves are declared in `data/**/*.json` and evaluated in pure C# (`SkillParameterCurveTemplate`); strict `One-Type-Per-File`, sealed classes, pluralized folders, and 18-Hz naming suffixes are enforced across all layers. |

## Project Structure

### Documentation (this feature)

```text
specs/003-combat-missiles-loot-inventory/
├── plan.md                                      # This file (/speckit-plan output)
├── research.md                                  # Phase 0 ground-truth C++, binary & JXData v1 research
├── data-model.md                                # Phase 1 ABP DDD, JXData v1 & 18-Hz Zone data model
├── quickstart.md                                # Phase 1 exporter, build, test & JX-Swarm verification guide
├── checklists/
│   └── requirements.md                          # Specification quality checklist (16/16 passed)
└── contracts/
    ├── combat-and-skills-protocol.md            # Port 6666 Combat, Skills, Missiles, Progression & Revival wire contract
    ├── inventory-and-ground-loot-protocol.md    # Port 6666 Ground Objects & 6x10 Inventory wire contract
    └── zone-combat-and-inventory-ports.md       # Internal C# 14 Catalogs, Zone Ports & Persistence contract
```

### Source Code & Data (repository root `d:\Projects\jx-server`)

```text
JXServer.slnx
├── schemas/                                     # Tier 1 JSON Schema (Draft 2020-12) contracts (JXData Standard v1)
│   ├── map.schema.json
│   ├── npc-catalog.schema.json
│   ├── player-template.schema.json
│   ├── skill-catalog.schema.json
│   ├── item-catalog.schema.json
│   ├── loot-table.schema.json
│   └── shop-catalog.schema.json
│
├── data/                                        # [NEW] Canonical UTF-8 JSON Game Data (Source of Truth)
│   ├── maps/                                    # 053_ba-lang-huyen.map.json (+ connected maps 54, 70, 74, 199)
│   ├── npcs/                                    # core.npcs.json
│   ├── players/                                 # progression.players.json (incl. starterItems & levelExpTable)
│   ├── skills/                                  # base-and-factions.skills.json, missiles.skills.json
│   ├── items/                                   # equipment.items.json, consumables-and-scripts.items.json, magic-affixes.items.json
│   └── loot/                                    # field-monsters.loot.json
│
├── artifacts/                                   # [NEW] Tier 2 MemoryPack runtime binary snapshots (*.pack + manifest.json)
│
├── tools/
│   └── JXServer.DataExporter/                   # [NEW] Standalone Offline Ground-Truth Exporter CLI
│       ├── JXServer.DataExporter.csproj
│       ├── Program.cs
│       ├── Exporters/                           # MapDataExporter, NpcAndLootDataExporter, PlayerDataExporter, SkillAndMissileDataExporter, ItemAndAffixDataExporter
│       └── Compilation/                         # MemoryPackSnapshotCompiler, JsonSchemaAssetValidator
│
├── src/
│   ├── JXServer.Primitives/                     # Tier 1: Domain.Shared / Zero-dependency primitives
│   │   ├── Combat/                              # [NEW] SixtyFourDirectionTrigTable, FiveElementsSeriesMatrix, EntityRelationKind, PlayerPkMode, BaseAttributeKind, DamageChannelKind, SkillStyleKind, MissileMoveKind, MonsterAiStateKind, CombatConsts
│   │   ├── Items/                               # [NEW] ItemGenreKind, ItemQualityKind, EquipmentPartKind, ItemContainerPlace, GroundObjectKind, ItemBindState, ItemConsts
│   │   └── WorldZones/                          # [UPDATED] EntityDoingPosture (Hurt, Revive), MovementAntiCheatConsts
│   │
│   ├── JXServer.GameAssets/                     # Tier 2a: Dual-Tier JXData Standard v1 Catalogs & ItemGenerator
│   │   ├── Items/                               # [NEW] ItemTemplate, ItemBaseAttributeTemplate, ItemRequirementTemplate, MagicAffixTemplate, GroundObjectTemplate, IItemTemplateCatalog, ItemTemplateCatalog, ItemGenerator
│   │   ├── LootTables/                          # [NEW] LootTableTemplate, WeightedLootPoolEntryTemplate, ILootTableCatalog, LootTableCatalog
│   │   ├── Skills/                              # [NEW] SkillTemplate, SkillParameterCurveTemplate, SkillEffectScalingTemplate, SkillCostScalingTemplate, MissileTemplate, ISkillTemplateCatalog, SkillTemplateCatalog, IMissileTemplateCatalog, MissileTemplateCatalog
│   │   ├── NpcTemplates/                        # [UPDATED] NpcTemplate (combatScaling, skills, aiConfig, treasureDropCount, lootTableCode), NpcTemplateCatalog (loads data/npcs/*.npcs.json / artifacts)
│   │   ├── PlayerTemplates/                     # [UPDATED] PlayerTemplateCatalog (loads data/players/progression.players.json / artifacts: seriesGrowthRules, fiveElementsMatrix, levelExpTable, starterItems)
│   │   ├── WorldMaps/                           # [UPDATED] MapDescriptor & MapCatalog (loads data/maps/*.map.json / artifacts)
│   │   └── Snapshots/                           # [NEW] GameAssetsSnapshotBundle, GameAssetsSnapshotLoader
│   │
│   ├── JXServer.Contracts/                      # Tier 3a: ABP Application.Contracts
│   │   └── Characters/                          # [UPDATED] CharacterSkillDto, CharacterItemMagicAttributeDto, CharacterItemDto, UpdateCharacterWorldSnapshotDto, WorldEntryResultDto
│   │
│   ├── JXServer.Domain/                         # Tier 3b: ABP DDD Domain
│   │   └── Characters/                          # [UPDATED] Character, CharacterSkillValue, CharacterItem, ItemMagicAttributeValue, CharacterManager (seeds starterSkills & starterItems)
│   │
│   ├── JXServer.Infrastructure/                 # Tier 3d: EF Core 10 Persistence
│   │   └── Persistence/                         # [UPDATED] JXServerDbContext & CharacterItem / JSON column mappings
│   │
│   ├── JXServer.Application/                    # Tier 3c: ABP DDD Application Use-Case Orchestrators
│   │   └── Characters/                          # [UPDATED] CharacterAppService (loads/saves CharacterItem & progression in EnterWorldAsync & UpdateCharacterWorldSnapshotAsync)
│   │
│   ├── JXServer.ZoneEngine/                     # Tier 2b: Sharded 18-Hz Deterministic Simulation Engine
│   │   ├── Combat/                              # [NEW] SkillDamagePayload, ActiveMissileSlot, EntityStatusEffectState, CombatDamageCalculator, MissileSimulationTickSystem, MonsterAiTickSystem, CastSkillZoneCommand, ChangeAuraSkillZoneCommand, ChangePkModeZoneCommand, AllocateBaseAttributeZoneCommand, AllocateSkillPointZoneCommand, RevivePlayerZoneCommand
│   │   ├── Items/                               # [NEW] PlayerItemSlot, PlayerInventoryGrid, GroundObjectSlot, GroundObjectTickSystem, PickupGroundObjectZoneCommand, MoveInventoryItemZoneCommand, UseInventoryItemZoneCommand, DropInventoryItemZoneCommand, DivideInventoryItemZoneCommand, RequestGroundObjectSyncZoneCommand
│   │   ├── NpcEntities/                         # [UPDATED] NpcZoneState (AI state, aggro target, spawn coordinates, respawn timer, combat stats, status effects)
│   │   ├── PlayerSessions/                      # [UPDATED] PlayerZoneState (PlayerInventoryGrid, learned skills, cooldowns, combat stats, status effects, medicine regen), IPlayerOutboundSink, GroundObjectSyncSnapshot, PlayerItemSyncSnapshot
│   │   ├── SpatialGrid/                         # [UPDATED] SpatialRegionSlot (adds GroundObjects list)
│   │   └── WorldZones/                          # [UPDATED] MapZoneWorker (integrates MissileSimulationTickSystem, MonsterAiTickSystem, GroundObjectTickSystem & M3 command handlers)
│   │
│   ├── JXServer.Adapter.LegacyJX1/              # Tier 4a: Legacy TCP 5622 & 6666 Adapter
│   │   ├── Framing/                             # [UPDATED] ProtocolSizeTable (registers M3 C2S opcodes 0x4A, 0x4D, 0x57, 0x58, 0x59, 0x5A, 0x5B, 0x5E, 0x6F, 0x72, 0x74, 0x76, 0x83, 0x85, 0x87)
│   │   ├── GameServer/                          # [UPDATED] GameServerClientSession + 15 new *PacketHandler classes
│   │   └── Packets/                             # [UPDATED] ProtocolOpcodes + 30 new *C2SPacket / *S2CPacket structs (one type per file)
│   │
│   └── JXServer.Host/                           # Tier 4b: Executable .NET 10 Server Host
│       └── appsettings.json                     # [UPDATED] GameData paths (data/ & artifacts/)
│
└── tests/
    ├── JXServer.Protocol.Tests/                 # [UPDATED] 64-dir trig, Five-Elements matrix, and 30 M3 packet symmetry tests
    ├── JXServer.ZoneEngine.Tests/               # [UPDATED] Schema validation, MemoryPack catalog loading, 18-Hz missile/combat/status effects, monster AI/level-up, 6-affix Blue ItemGenerator, 6x10 inventory & Five-Elements activation tests
    └── JXServer.Integration.Tests/              # [UPDATED] End-to-end TCP 6666 skill combat, monster kill, level-up, ground loot pickup, 6-affix Blue equip, potion/Town Portal use, and DB persistence tests
```

**Structure Decision**: Extends `JXServer.slnx` with `tools/JXServer.DataExporter` and new pluralized feature directories (`Combat/`, `Items/`, `Skills/`, `LootTables/`, `Snapshots/`) across the existing 4 architectural tiers while maintaining strict `One-Type-Per-File` and inward dependency rules.

## Complexity Tracking

> **No Constitution Check violations exist.** All gates pass without exception.
