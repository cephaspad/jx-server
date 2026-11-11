# Implementation Plan: Core Game Data Pipeline (`JXData Standard v1`)

**Branch/Directory**: `specs/001-core-game-data-pipeline`  
**Date**: 2026-11-11  
**Spec**: [spec.md](spec.md)  

---

## 1. Summary & Technical Scope

This specification establishes the authoritative, dual-tier game data architecture (**`JXData Standard v1`**) for JXServer, governed by Constitution v1.2.0 Principle V and `docs/data-architecture-standard.md`:
1. **Tier 1 (Authoring & Source of Truth)**: UTF-8 JSON files under `data/` (`maps/`, `npcs/`, `players/`, `skills/`, `items/`, `loot/`, `shops/`) validated against JSON Schema Draft 2020-12 specifications under `schemas/`.
2. **Tier 2 (High-Speed Binary Delivery)**: Zero-copy, pre-compiled binary snapshot bundles (`artifacts/jxdata.pack`) serialized via `MemoryPack` (`v1.21.4`) with SHA-256 integrity checksums.
3. **Runtime Asset Registries (`JXServer.GameAssets`)**: Clean, immutable `I*Catalog` registries (`IMapCatalog`, `INpcTemplateCatalog`, `IPlayerTemplateCatalog`, `ISkillTemplateCatalog`, `IMissileTemplateCatalog`, `IItemTemplateCatalog`, `ILootTableCatalog`, `IShopCatalog`) backed by `FrozenDictionary` with zero legacy file parsers or archive decompressors in the runtime assembly.
4. **Standalone Offline Migration CLI (`tools/JXServer.DataExporter`)**: A decoupled extraction and compilation tool that parses legacy `jx-linux-bin` assets (`.pak`, `.dat`, `.txt`, `.ini`, Lua 4.0 scripts), generates canonical JSON documents, evaluates scaling curves into static lookup tables, compiles MemoryPack snapshots, and verifies 4-layer parity against historical ground truth.

---

## 2. Technical Context

- **Target Framework**: .NET 10 (`net10.0`), C# 14
- **Build Configuration**: Centralized XML solution (`JXServer.slnx`), `Directory.Build.props` (`Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `AllowUnsafeBlocks=true`)
- **Key Packages**:
  - `MemoryPack` (`1.21.4`) — Zero-copy high-performance binary serialization
  - `System.Text.Json` (`10.0.0`) — Canonical JSON authoring with `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`
  - `JsonSchema.Net` / Schema validators in compilation tools
  - `Microsoft.Extensions.DependencyInjection` & `Microsoft.Extensions.Logging.Abstractions` (`10.0.0`)
- **Runtime Assemblies**:
  - `src/JXServer.GameAssets/`: Runtime catalog implementations and `GameAssetsSnapshotLoader`
  - `tools/JXServer.DataExporter/`: Offline migration CLI, legacy readers, and parity verifier
- **Testing**: `tests/JXServer.ZoneEngine.Tests/` (`JxDataCatalogLoadingTests.cs`, `GameAssetsCatalogTests.cs`, `NpcAssetLoadingTests.cs`)

---

## 3. Constitution Check (v1.2.0)

| Principle / Gate | Status | Compliance Details |
| :--- | :---: | :--- |
| **Principle I: Hybrid Control Plane & 18-Hz Zone** | **PASS** | Registries are immutable `FrozenDictionary` instances shared across zones via `ZoneAssetServices`; zero disk I/O occurs inside 18-Hz simulation ticks. |
| **Principle II: Multi-Protocol Transport Abstraction** | **PASS** | Asset models represent game rules and templates independent of wire framing. |
| **Principle III: Zero-Hallucination Evidence** | **PASS** | 100% of entity structures, row IDs, skill curves, and obstacle layouts match historical C++ Kingsoft definitions documented in `docs/data-architecture-standard.md`. |
| **Principle IV: Zero-Dupe Economy** | **PASS** | Item templates preserve natural composite keys `(quality, genre, detailType, particularType, level)` and row IDs with zero synthetic ID inventions. |
| **Principle V: UTF-8 Domain Core & Dual-Tier Data Standard** | **PASS** | Canonical UTF-8 JSON + MemoryPack snapshots; legacy file readers are banned from `src/JXServer.GameAssets` and reside exclusively in `tools/JXServer.DataExporter`. |
| **Principle VI: Clean Modular Boundaries & 1 Type Per File** | **PASS** | All concrete classes are `sealed`, each class occupies its own file, and folders follow pluralized domain conventions (`NpcTemplates/`, `WorldMaps/`, etc.). |
| **Gate 1: Ground-Truth Specification** | **PASS** | Formally documented schemas in `schemas/` and contracts in `contracts/`. |
| **Gate 2: Unit Gate** | **PASS** | Verified via `JxDataCatalogLoadingTests`, `GameAssetsCatalogTests`, and `NpcAssetLoadingTests`. |
| **Gate 3: Integration Gate** | **PASS** | End-to-end server initialization verifies catalogs load into live zones. |
| **Gate 4: Stress Gate** | **PASS** | Sub-100ms cold startup and $O(1)$ constant-time lookups with zero heap allocations. |
| **Gate 5: Build Gate** | **PASS** | Builds cleanly with `TreatWarningsAsErrors=true` (0 warnings). |

---

## 4. Real Project Structure

```text
jx-server/
├── schemas/                                                    # JSON Schema 2020-12 Contracts
│   ├── item-catalog.schema.json
│   ├── loot-table.schema.json
│   ├── map.schema.json
│   ├── npc-catalog.schema.json
│   ├── player-template.schema.json
│   ├── shop-catalog.schema.json
│   └── skill-catalog.schema.json
├── data/                                                       # Canonical UTF-8 JSON Game Data
│   ├── items/
│   │   ├── consumables-and-scripts.items.json
│   │   ├── equipment.items.json
│   │   ├── golden-equipment.items.json
│   │   └── magic-affixes.items.json
│   ├── loot/
│   │   └── droprates.loot.json
│   ├── maps/
│   │   └── 053_ba-lang-huyen.map.json
│   ├── npcs/
│   │   ├── core.npcs.json
│   │   ├── event-spawns.npcs.json
│   │   └── global-spawns.npcs.json
│   ├── players/
│   │   └── progression.players.json
│   ├── shops/
│   │   └── npc-shops.shops.json
│   └── skills/
│       ├── base-and-factions.skills.json
│       └── missiles.skills.json
├── artifacts/                                                  # Compiled MemoryPack Snapshots
│   ├── jxdata.pack
│   └── migration-parity-report.json
├── src/
│   └── JXServer.GameAssets/                                    # Runtime Immutable Catalogs
│       ├── Configuration/
│       │   └── GameAssetsOptions.cs
│       ├── Items/
│       │   ├── EquipPartWeightsTemplate.cs
│       │   ├── GeneratedItemData.cs
│       │   ├── GroundObjectTemplate.cs
│       │   ├── IItemTemplateCatalog.cs
│       │   ├── ItemBaseAttributeTemplate.cs
│       │   ├── ItemGenerator.cs
│       │   ├── ItemRequirementTemplate.cs
│       │   ├── ItemTemplate.cs
│       │   ├── ItemTemplateCatalog.cs
│       │   ├── MagicAffixTemplate.cs
│       │   └── SeriesWeightsTemplate.cs
│       ├── Logging/
│       │   └── GameAssetsLogMessages.cs
│       ├── LootTables/
│       │   ├── ILootTableCatalog.cs
│       │   ├── LootTableCatalog.cs
│       │   ├── LootTableTemplate.cs
│       │   └── WeightedLootPoolEntryTemplate.cs
│       ├── NpcTemplates/
│       │   ├── INpcTemplateCatalog.cs
│       │   ├── NpcAiConfigTemplate.cs
│       │   ├── NpcCombatScalingTemplate.cs
│       │   ├── NpcSkillSlotTemplate.cs
│       │   ├── NpcTemplate.cs
│       │   ├── NpcTemplateCatalog.cs
│       │   └── StaticNpcSpawnTemplate.cs
│       ├── PlayerTemplates/
│       │   ├── CharacterBaseTemplate.cs
│       │   ├── IPlayerTemplateCatalog.cs
│       │   ├── PlayerSeriesGrowthRuleTemplate.cs
│       │   ├── PlayerTemplateCatalog.cs
│       │   ├── StarterItemTemplate.cs
│       │   ├── StarterSkillEntry.cs
│       │   └── StarterVillageTemplate.cs
│       ├── Shops/
│       │   ├── IShopCatalog.cs
│       │   ├── ShopCatalog.cs
│       │   ├── ShopGoodTemplate.cs
│       │   └── ShopTemplate.cs
│       ├── Skills/
│       │   ├── IMissileTemplateCatalog.cs
│       │   ├── ISkillTemplateCatalog.cs
│       │   ├── MissileTemplate.cs
│       │   ├── MissileTemplateCatalog.cs
│       │   ├── SkillCostScalingTemplate.cs
│       │   ├── SkillCurveControlPoint.cs
│       │   ├── SkillEffectScalingTemplate.cs
│       │   ├── SkillParameterCurveTemplate.cs
│       │   ├── SkillTemplate.cs
│       │   └── SkillTemplateCatalog.cs
│       ├── Snapshots/
│       │   ├── GameAssetsSnapshotBundle.cs
│       │   └── GameAssetsSnapshotLoader.cs
│       ├── WorldMaps/
│       │   ├── IMapCatalog.cs
│       │   ├── ITrapScriptCatalog.cs
│       │   ├── MapCatalog.cs
│       │   ├── MapDescriptor.cs
│       │   ├── ObstacleRleCodec.cs
│       │   ├── PortalTrapBindingTemplate.cs
│       │   ├── RegionObstacleGrid.cs
│       │   ├── RegionTrapGrid.cs
│       │   └── TrapScriptCatalog.cs
│       └── GameAssetsServiceCollectionExtensions.cs
├── tools/
│   └── JXServer.DataExporter/                                  # Offline Migration & Parity CLI
│       ├── Compilation/
│       │   ├── JsonSchemaAssetValidator.cs
│       │   └── MemoryPackSnapshotCompiler.cs
│       ├── Exporters/
│       │   ├── ExportCatalogRegistry.cs
│       │   ├── ItemAndAffixDataExporter.cs
│       │   ├── MapDataExporter.cs
│       │   ├── NpcAndLootDataExporter.cs
│       │   ├── PlayerDataExporter.cs
│       │   ├── ShopDataExporter.cs
│       │   └── SkillAndMissileDataExporter.cs
│       ├── Legacy/
│       │   ├── HybridLegacyTextDecoder.cs
│       │   ├── IniFileParser.cs
│       │   ├── KTabFileTable.cs
│       │   ├── LegacyPathResolver.cs
│       │   ├── Lua4Interpreter.cs
│       │   ├── Lua4Lexer.cs
│       │   ├── Lua4Token.cs
│       │   ├── Lua4TokenKind.cs
│       │   ├── Lua4Value.cs
│       │   ├── MagicAttributeRegistry.cs
│       │   ├── PakFilePathHasher.cs
│       │   ├── ParsedRegionDat.cs
│       │   ├── ParsedRegionNpc.cs
│       │   ├── ParsedRegionObj.cs
│       │   ├── ParsedTrapCellSpan.cs
│       │   ├── RecordedLuaMapTrap.cs
│       │   ├── RecordedLuaNpcSpawn.cs
│       │   ├── RegionDatBinaryReader.cs
│       │   ├── TabFileParser.cs
│       │   ├── UclNrv2bDecompressor.cs
│       │   └── XPackArchiveReader.cs
│       ├── Verification/
│       │   └── MigrationParityVerifier.cs
│       └── Program.cs
└── tests/
    └── JXServer.ZoneEngine.Tests/
        ├── GameAssetsCatalogTests.cs
        ├── JxDataCatalogLoadingTests.cs
        └── NpcAssetLoadingTests.cs
```

---

## 5. Risk Assessment & Verification Strategy

- **Risk 1: Rounding drift across fractional skill scaling curves**: Legacy Lua 4.0 used double precision before integer casting. Mitigation: Pre-evaluated `valuesByLevel` arrays are computed using exact IEEE 754 arithmetic with `Math.Floor` up to level 64, tested in `JxDataCatalogLoadingTests`.
- **Risk 2: Obstacle decompression corruption**: Misinterpreting run-length encoding leads to invalid walkability checks. Mitigation: `ObstacleRleCodec` enforces 6-byte Little-Endian pairs (`ushort runLength` + `int cellValue`) verified by round-trip tests.
- **Risk 3: Legacy parser re-introduction into runtime**: An engineer might accidentally re-introduce file readers into `JXServer.GameAssets`. Mitigation: Enforced by Constitution Principle V and verified by architecture review.
