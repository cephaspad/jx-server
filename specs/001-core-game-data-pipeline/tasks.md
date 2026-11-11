# Tasks: Core Game Data Pipeline (`JXData Standard v1`)

**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (`[US1]`, `[US2]`, `[US3]`)
- Every task specifies the exact target file path(s) in `d:\Projects\jx-server`

---

## Phase 1: Setup & Schema Contracts (Shared Scaffolding)

- [X] T001 [P] [US1] Create unified map schema enforcing bounds, obstacles, portals, and spawns in `schemas/map.schema.json`
- [X] T002 [P] [US1] Create NPC and monster catalog schema in `schemas/npc-catalog.schema.json`
- [X] T003 [P] [US1] Create player templates and series progression schema in `schemas/player-template.schema.json`
- [X] T004 [P] [US1] Create skill curves, 64-dir missiles, and proc triggers schema in `schemas/skill-catalog.schema.json`
- [X] T005 [P] [US1] Create items, affixes, golden equipment sets, and recipes schema in `schemas/item-catalog.schema.json`
- [X] T006 [P] [US1] Create weighted monster drop tables schema in `schemas/loot-table.schema.json`
- [X] T007 [P] [US1] Create multi-currency NPC shop merchandise schema in `schemas/shop-catalog.schema.json`

---

## Phase 2: Canonical Structured Data Documents (`data/`)

- [X] T008 [P] [US1] Author canonical map definition for Ba Lăng Huyện (Map 53) in `data/maps/053_ba-lang-huyen.map.json`
- [X] T009 [P] [US1] Author core NPC archetypes, global spawns, and event spawns in `data/npcs/core.npcs.json`, `data/npcs/global-spawns.npcs.json`, and `data/npcs/event-spawns.npcs.json`
- [X] T010 [P] [US1] Author player progression rules, starter villages, and elemental matrices in `data/players/progression.players.json`
- [X] T011 [P] [US1] Author martial skills, passive auras, and projectile profiles in `data/skills/base-and-factions.skills.json` and `data/skills/missiles.skills.json`
- [X] T012 [P] [US1] Author equipment, golden sets, consumables, and magic affix pools in `data/items/equipment.items.json`, `data/items/golden-equipment.items.json`, `data/items/consumables-and-scripts.items.json`, and `data/items/magic-affixes.items.json`
- [X] T013 [P] [US1] Author weighted monster loot tables in `data/loot/droprates.loot.json`
- [X] T014 [P] [US1] Author multi-currency NPC shop inventories in `data/shops/npc-shops.shops.json`

---

## Phase 3: Runtime Immutable Catalogs (`JXServer.GameAssets`)

- [X] T015 [P] [US2] Implement `MapDescriptor`, `RegionObstacleGrid`, `RegionTrapGrid`, `PortalTrapBindingTemplate`, `ObstacleRleCodec`, and `MapCatalog` in `src/JXServer.GameAssets/WorldMaps/`
- [X] T016 [P] [US2] Implement `NpcTemplate`, `NpcCombatScalingTemplate`, `NpcSkillSlotTemplate`, `NpcAiConfigTemplate`, `StaticNpcSpawnTemplate`, and `NpcTemplateCatalog` in `src/JXServer.GameAssets/NpcTemplates/`
- [X] T017 [P] [US2] Implement `CharacterBaseTemplate`, `PlayerSeriesGrowthRuleTemplate`, `StarterVillageTemplate`, `StarterSkillEntry`, and `PlayerTemplateCatalog` in `src/JXServer.GameAssets/PlayerTemplates/`
- [X] T018 [P] [US2] Implement `SkillTemplate`, `MissileTemplate`, `SkillCostScalingTemplate`, `SkillEffectScalingTemplate`, `SkillParameterCurveTemplate`, `SkillCurveControlPoint`, and `SkillTemplateCatalog` in `src/JXServer.GameAssets/Skills/`
- [X] T019 [P] [US2] Implement `ItemTemplate`, `MagicAffixTemplate`, `ItemBaseAttributeTemplate`, `ItemRequirementTemplate`, `EquipPartWeightsTemplate`, `SeriesWeightsTemplate`, `GroundObjectTemplate`, and `ItemTemplateCatalog` in `src/JXServer.GameAssets/Items/`
- [X] T020 [P] [US2] Implement `LootTableTemplate`, `WeightedLootPoolEntryTemplate`, and `LootTableCatalog` in `src/JXServer.GameAssets/LootTables/`
- [X] T021 [P] [US2] Implement `ShopTemplate`, `ShopGoodTemplate`, and `ShopCatalog` in `src/JXServer.GameAssets/Shops/`
- [X] T022 [US2] Implement `GameAssetsSnapshotBundle` and `GameAssetsSnapshotLoader` in `src/JXServer.GameAssets/Snapshots/` for zero-copy MemoryPack loading and SHA-256 verification
- [X] T023 [US2] Implement `GameAssetsOptions`, `GameAssetsLogMessages`, and DI registration extensions in `src/JXServer.GameAssets/GameAssetsServiceCollectionExtensions.cs`

---

## Phase 4: Standalone Offline Exporter & Parity Verifier (`tools/JXServer.DataExporter`)

- [X] T024 [P] [US3] Implement legacy archive decompressor and table parsers (`PakFilePathHasher`, `UclNrv2bDecompressor`, `XPackArchiveReader`, `KTabFileTable`, `TabFileParser`, `IniFileParser`, `HybridLegacyTextDecoder`) in `tools/JXServer.DataExporter/Legacy/`
- [X] T025 [P] [US3] Implement Lua 4.0 sandbox evaluator (`Lua4Lexer`, `Lua4Token`, `Lua4Interpreter`, `Lua4Value`, `RecordedLuaMapTrap`, `RecordedLuaNpcSpawn`) in `tools/JXServer.DataExporter/Legacy/`
- [X] T026 [P] [US3] Implement domain data exporters (`MapDataExporter`, `NpcAndLootDataExporter`, `PlayerDataExporter`, `SkillAndMissileDataExporter`, `ItemAndAffixDataExporter`, `ShopDataExporter`, `ExportCatalogRegistry`) in `tools/JXServer.DataExporter/Exporters/`
- [X] T027 [US3] Implement JSON schema validator `JsonSchemaAssetValidator` and `MemoryPackSnapshotCompiler` in `tools/JXServer.DataExporter/Compilation/`
- [X] T028 [US3] Implement 4-layer parity verifier `MigrationParityVerifier` and CLI entry point in `tools/JXServer.DataExporter/Program.cs`

---

## Phase 5: Verification & Parity Tests

- [X] T029 [P] [US1] Write automated JSON Schema validation and foreign-key referential integrity tests in `tests/JXServer.ZoneEngine.Tests/JxDataCatalogLoadingTests.cs`
- [X] T030 [P] [US2] Write unit tests verifying Map 53 obstacle grid decompression and player starter village templates in `tests/JXServer.ZoneEngine.Tests/GameAssetsCatalogTests.cs`
- [X] T031 [P] [US2] Write unit tests for NPC templates, combat scaling curves, and skill curves in `tests/JXServer.ZoneEngine.Tests/NpcAssetLoadingTests.cs`
- [X] T032 [US2] Verify cold start loading time under 100ms and zero allocation lookups across all 7 catalogs via automated test execution
