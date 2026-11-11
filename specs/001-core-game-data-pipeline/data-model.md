# Data Model: Core Game Data Pipeline (`JXData Standard v1`)

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  
**Standard**: [docs/data-architecture-standard.md](../../docs/data-architecture-standard.md)  

---

## 1. Domain Entities & In-Memory Catalog Models

### 1.1 Map Domain (`data/maps/*.map.json` / `IMapCatalog`)
- **`MapDescriptor`**:
  - `MapId` (`ushort`): Numeric subworld map ID (`1..999`).
  - `Code` (`string`): Semantic ASCII slug (e.g. `ba-lang-huyen`).
  - `Name` (`string`): UTF-8 Vietnamese display name (e.g. `Ba Lăng huyện`).
  - `MapType` (`string`): `"City"`, `"Capital"`, `"Field"`, `"Cave"`, `"Battle"`, `"Mission"`, `"Prison"`.
  - `LeftRegionX`, `TopRegionY`, `RightRegionX`, `BottomRegionY` (`int`): Bounding region coordinates.
- **`RegionObstacleGrid`**:
  - `RegionX`, `RegionY` (`int`): Spatial region coordinates (`512x1024` Mps).
  - `CellObstacles` (`ReadOnlyMemory<int>`): `16 x 32 = 512` obstacle integer cells, encoded via `ObstacleRleCodec` (6-byte Little-Endian run-length pairs: `ushort runLength` + `int cellValue`).
- **`PortalTrapBindingTemplate`**:
  - `TrapId` (`uint`): 32-bit trap identifier from `Region_S.dat`.
  - `Code` (`string`): Semantic portal slug.
  - `Action` (`string`): `"teleport"`, `"toggleFightMode"`, `"randomTeleport"`, `"customScript"`, `"unmapped"`.
  - `TargetMapId` (`ushort`), `TargetMpsX` (`int`), `TargetMpsY` (`int`), `TargetFightMode` (`int?`).
  - `AlternateMpsX` (`int?`), `AlternateMpsY` (`int?`), `AlternateFightMode` (`int?`): Bidirectional town-gate return destination.
  - `TriggerCells` (`IReadOnlyList<long>`): Encoded coordinate positions `(cellX << 16) | cellY`.

### 1.2 NPC & Monster Domain (`data/npcs/*.npcs.json` / `INpcTemplateCatalog`)
- **`NpcTemplate`**:
  - `Id` (`int`): Zero-based template index matching `npcs.txt` (`0..2539`).
  - `Code` (`string`): Deterministic slug (e.g. `dong-bac-ho`).
  - `Name` (`string`): UTF-8 display name.
  - `EntityKind` (`EntityKind`): `Monster = 0`, `Player = 1`, `Partner = 2`, `DialogNpc = 3`.
  - `DefaultCamp` (`byte`), `ElementSeries` (`sbyte` `-1..4`).
  - `WalkSpeedMpsPerFrame` (`int`), `RunSpeedMpsPerFrame` (`int`).
  - `AttackSpeedFrames` (`int`), `CastSpeedFrames` (`int`), `HitRecoverFrames` (`int`).
  - `AttackRadiusMps` (`int`), `ActiveRadiusMps` (`int`), `VisionRadiusMps` (`int`), `PursuitRadiusMps` (`int`).
  - `RespawnFrames` (`int`), `BaseLife` (`int`), `BaseExp` (`int`).
  - `TreasureDropCount` (`int`): Number of drop roll iterations per kill.
  - `LootTableCode` (`string?`): Cross-reference to loot table catalog.
  - `Skills` (`IReadOnlyList<NpcSkillSlotTemplate>`): Up to 4 active skill slots with cast weights.
  - `CombatScaling` (`NpcCombatScalingTemplate?`): Scaling curves for life, damage, and elemental resistances.

### 1.3 Player Progression Domain (`data/players/*.players.json` / `IPlayerTemplateCatalog`)
- **`CharacterBaseTemplate`**:
  - `Series` (`ElementSeries`), `Gender` (`CharacterGender`).
  - `BaseStrength`, `BaseDexterity`, `BaseVitality`, `BaseEnergy` (`int`).
  - `BaseLife`, `BaseMana`, `BaseStamina` (`int`).
  - `StarterSkills` (`IReadOnlyList<StarterSkillEntry>`).
  - `StarterItems` (`IReadOnlyList<StarterItemTemplate>`).
- **`StarterVillageTemplate`**:
  - `NativePlaceId` (`ushort`): Starter village map ID (`53`, `20`, `99`, `100`, `101`, `121`, `153`, `174`).
  - `VillageName` (`string`), `SpawnMapId` (`ushort`), `SpawnMpsX` (`int`), `SpawnMpsY` (`int`).
- **`PlayerSeriesGrowthRuleTemplate`**:
  - Vital statistic growth per level and per allocated stat point.

### 1.4 Skill & Projectile Domain (`data/skills/*.skills.json` / `ISkillTemplateCatalog`, `IMissileTemplateCatalog`)
- **`SkillTemplate`**:
  - `Id` (`ushort`), `Code` (`string`), `Name` (`string`).
  - `Style` (`string`): `"melee"`, `"projectile"`, `"selfBuff"`, `"targetBuff"`, `"aura"`, `"passive"`, `"jump"`.
  - `ElementSeries` (`sbyte` `-1..4`), `IsOffensive` (`bool`), `IsPhysical` (`bool`), `IsAura` (`bool`).
  - `CastRangeMps` (`int`), `WaitFrames` (`int` cooldown), `MinFramesPerCast` (`int`).
  - `MissileId` (`ushort?`), `MissileForm` (`string?`).
  - `CostScaling` (`SkillCostScalingTemplate?`): Mana/stamina cost curves.
  - `EffectScaling` (`IReadOnlyList<SkillEffectScalingTemplate>`): Pre-evaluated 3-parameter curves.
- **`SkillParameterCurveTemplate`**:
  - `ParamIndex` (`int` `0..2`), `CurveType` (`string`), `ControlPoints` (`IReadOnlyList<SkillCurveControlPoint>`).
  - `ValuesByLevel` (`IReadOnlyList<int>`): Pre-evaluated array of length up to 64 for instant $O(1)$ lookups.
- **`MissileTemplate`**:
  - `Id` (`ushort`), `Code` (`string`), `MoveKind` (`string`), `SpeedMpsPerFrame` (`int`).
  - `LifeFrames` (`int`), `HitRadiusMps` (`int`), `CollideRadiusCells` (`int`), `DamageRadiusCells` (`int`).
  - `IsRangeDamage` (`bool`), `PierceCount` (`int`), `CollideVanish` (`bool`).

### 1.5 Item & Affix Domain (`data/items/*.items.json` / `IItemTemplateCatalog`)
- **`ItemTemplate`**:
  - `RowId` (`int`): Authentic 1-based row index in source table.
  - `Code` (`string`), `Name` (`string`).
  - `Quality` (`int` `0`=White/Blue, `1`=Golden, `2`=Purple, `3`=Platina, `4`=Broken).
  - `Genre` (`int`), `DetailType` (`int`), `ParticularType` (`int`), `Level` (`int`).
  - `ElementSeries` (`sbyte`), `GridWidthCells` (`byte`), `GridHeightCells` (`byte`).
  - `PriceGold` (`uint`), `MaxStackCount` (`int`), `MaxEnchaseSockets` (`int`).
  - `BaseAttributes` (`IReadOnlyList<ItemBaseAttributeTemplate>`).
  - `Requirements` (`IReadOnlyList<ItemRequirementTemplate>`).
  - `FixedMagicAffixes` (`IReadOnlyList<MagicAffixTemplate>`): For Golden and Platina equipment.
- **`MagicAffixTemplate`**:
  - `RowId` (`int`), `AffixPool` (`"blueRandom"`, `"purpleEnchase"`, `"goldenFixed"`).
  - `AttributeId` (`ushort`), `AttributeCode` (`string`), `IsSuffix` (`bool`).
  - `WeightsByEquipPart` (`EquipPartWeightsTemplate`), `WeightsBySeries` (`SeriesWeightsTemplate`).
  - `Param0Min`, `Param0Max`, `Param1Min`, `Param1Max`, `Param2Min`, `Param2Max` (`int`).
- **`GroundObjectTemplate`**:
  - `DataId` (`ushort`), `Code` (`string`), `Name` (`string`), `Kind` (`string`).

### 1.6 Loot Table Domain (`data/loot/*.loot.json` / `ILootTableCatalog`)
- **`LootTableTemplate`**:
  - `Code` (`string`): Unique identifier (e.g. `npcdroprate10`).
  - `RandRange` (`int` `1000000`), `MagicRate` (`int`), `MoneyDropChance` (`int`), `MoneyScale` (`int`).
  - `MinMoneyGold` (`int`), `MaxMoneyGold` (`int`), `MinItemLevel` (`int`), `MaxItemLevel` (`int`).
  - `WeightedPools` (`IReadOnlyList<WeightedLootPoolEntryTemplate>`).

### 1.7 Shop Catalog Domain (`data/shops/*.shops.json` / `IShopCatalog`)
- **`ShopTemplate`**:
  - `ShopId` (`int`): 1-based index in `buysell.txt`.
  - `Code` (`string`), `Name` (`string`), `Currency` (`string`).
  - `Goods` (`IReadOnlyList<ShopGoodTemplate>`).

---

## 2. MemoryPack Binary Snapshot Bundle (`GameAssetsSnapshotBundle`)

```csharp
[MemoryPackable]
public sealed partial class GameAssetsSnapshotBundle
{
    public required string SchemaVersion { get; init; }
    public required DateTimeOffset CompiledAtUtc { get; init; }
    public required IReadOnlyList<MapDescriptor> Maps { get; init; }
    public required IReadOnlyList<NpcTemplate> Npcs { get; init; }
    public required IReadOnlyList<CharacterBaseTemplate> PlayerTemplates { get; init; }
    public required IReadOnlyList<SkillTemplate> Skills { get; init; }
    public required IReadOnlyList<MissileTemplate> Missiles { get; init; }
    public required IReadOnlyList<ItemTemplate> Items { get; init; }
    public required IReadOnlyList<MagicAffixTemplate> MagicAffixes { get; init; }
    public required IReadOnlyList<LootTableTemplate> LootTables { get; init; }
    public required IReadOnlyList<ShopTemplate> Shops { get; init; }
}
```

- Pre-compiled into `artifacts/jxdata.pack` with companion parity report `artifacts/migration-parity-report.json`.
- Verified and deserialized via `GameAssetsSnapshotLoader.LoadFromSnapshotAsync()` in milliseconds.
