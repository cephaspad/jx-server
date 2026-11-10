# Phase 1 Data Model: Five-Elements Combat, 64-Direction Missiles, Monster AI, Loot Generation & Inventory Management

**Feature Branch**: `003-combat-missiles-loot-inventory`  
**Date**: 2026-11-09  
**Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

All types strictly follow the **JXServer Constitution v1.2.0** and **JXData Standard v1** (`docs/data-architecture-standard.md`):
- **One-Type-Per-File** and **Pluralized Domain/Feature Folders**.
- **Sealed Concrete Classes** (`public sealed class` / `public readonly record struct`) unless designed as an abstract base.
- **5 Real-Time 18-Hz Game Server & `JXData Standard v1` Naming Conventions**:
  1. Directional wire packets (`*C2SPacket`, `*S2CPacket`) and handlers (`*PacketHandler`).
  2. In-memory 18-Hz simulation state (`*ZoneState`, `*Slot`) & channel commands (`*ZoneCommand`) processed by `<Subsystem>TickSystem`.
  3. Unit-explicit spatial/timing suffixes (`*MpsX`, `*MpsY`, `*RadiusMps`, `*CellX`, `*CellY`, `*WidthCells`, `*HeightCells`, `*RegionX`, `*RegionY`, `*Frames`, `*Seconds`, `*Minutes`).
  4. Immutable static asset templates (`*Template`, `*Descriptor`) with dual `Id` + `Code` identifiers exposed via `I*Catalog` / `*Catalog` registries (`MemoryPack` + canonical UTF-8 JSON).
  5. High-performance source-generated logging (`*LogMessages`).

---

## 1. Tier 1: `JXServer.Primitives` (Domain.Shared Constants, Math & Enums)

### `JXServer.Primitives/Combat/`
| Type Name | File Path | Kind | Fields / Values |
| :--- | :--- | :--- | :--- |
| `SixtyFourDirectionTrigTable` | `Combat/SixtyFourDirectionTrigTable.cs` | `static class` | `ReadOnlySpan<int> SinTable` (`g_nSinBuffer[64]`), `ReadOnlySpan<int> CosTable` (`g_nCosBuffer[64]`), `int GetDirectionIndex(int srcMpsX, int srcMpsY, int destMpsX, int destMpsY)` (`g_GetDirIndex`), `int DirToDirIndex(int dir, int maxDir)`, `int DirIndexToDir(int dirIndex, int maxDir)` |
| `FiveElementsSeriesMatrix` | `Combat/FiveElementsSeriesMatrix.cs` | `static class` | `bool Generates(ElementSeries src, ElementSeries target)` (`g_IsAccrue`: Metal$\rightarrow$Water$\rightarrow$Wood$\rightarrow$Fire$\rightarrow$Earth$\rightarrow$Metal), `bool Conquers(ElementSeries src, ElementSeries target)` (`g_IsConquer`: Metal$\rightarrow$Wood$\rightarrow$Earth$\rightarrow$Water$\rightarrow$Fire$\rightarrow$Metal), `ElementSeries GetGeneratedSeries(ElementSeries src)`, `ElementSeries GetConqueredSeries(ElementSeries src)` |
| `EntityRelationKind` | `Combat/EntityRelationKind.cs` | `enum : byte` | `None = 1` (`relation_none`), `Friend = 2` (`relation_friend`), `Enemy = 4` (`relation_enemy`), `Self = 8` (`relation_self`), `All = 15` (`relation_all`) |
| `PlayerPkMode` | `Combat/PlayerPkMode.cs` | `enum : byte` | `Peace = 0` (Luyện công), `Fight = 1` (Chiến đấu), `Slaughter = 2` (Đồ sát) |
| `BaseAttributeKind` | `Combat/BaseAttributeKind.cs` | `enum : byte` | `Strength = 0`, `Dexterity = 1`, `Vitality = 2`, `Energy = 3` |
| `DamageChannelKind` | `Combat/DamageChannelKind.cs` | `enum : byte` | `Physics = 0`, `Cold = 1`, `Fire = 2`, `Light = 3`, `Poison = 4`, `Magic = 5` |
| `SkillStyleKind` | `Combat/SkillStyleKind.cs` | `enum : byte` | `Melee = 0`, `Projectile = 1`, `SelfBuff = 2`, `TargetBuff = 3`, `Aura = 4`, `Passive = 5`, `SummonNpc = 6`, `Jump = 7`, `JumpAndAttack = 8`, `RunAndAttack = 9`, `MultiAttack = 10`, `CorpseExplode = 11`, `StrideObstacle = 12`, `Steal = 13` |
| `MissileMoveKind` | `Combat/MissileMoveKind.cs` | `enum : byte` | `Stand = 0`, `Stationary = 1`, `Line64 = 2`, `Random = 3`, `Circle = 4`, `Spread = 5`, `Spiral = 6`, `Helix = 7`, `Follow = 8`, `Motion = 9`, `Parabola = 10`, `SingleLine = 11`, `RollBack = 12`, `Toss = 13` |
| `MonsterAiStateKind` | `Combat/MonsterAiStateKind.cs` | `enum : byte` | `IdlePatrol = 0`, `Pursuit = 1`, `Attack = 2`, `LeashReturn = 3`, `DeadRespawnWait = 4` |
| `CombatConsts` | `Combat/CombatConsts.cs` | `static class` | `MinHitPercent = 5`, `MaxHitPercent = 95`, `DefaultMaxResistPercent = 75`, `HardCapResistPercent = 95`, `UnarmedStrengthDivisor = 5`, `DefaultPkDamageRatePercent = 20`, `SwitchToPeaceCooldownFrames = 3240`, `BroadcastNetCmdHurt = 9` |

### `JXServer.Primitives/Items/`
| Type Name | File Path | Kind | Fields / Values |
| :--- | :--- | :--- | :--- |
| `ItemGenreKind` | `Items/ItemGenreKind.cs` | `enum : byte` | `Equipment = 0`, `Medicine = 1`, `QuestKey = 2`, `TownPortal = 3`, `Material = 4`, `MagicScript = 6`, `PlatinaEquipment = 7` |
| `ItemQualityKind` | `Items/ItemQualityKind.cs` | `enum : byte` | `Normal = 0` (White/Blue), `Golden = 1`, `Purple = 2`, `Platina = 3`, `Broken = 4` |
| `EquipmentPartKind` | `Items/EquipmentPartKind.cs` | `enum : byte` | `Head = 0`, `Body = 1`, `Belt = 2`, `Weapon = 3`, `Foot = 4`, `Cuff = 5`, `Amulet = 6`, `Ring1 = 7`, `Ring2 = 8`, `Pendant = 9`, `Horse = 10`, `Mask = 11`, `Mantle = 12`, `Signet = 13`, `Shipin = 14`, `Count = 15` |
| `ItemContainerPlace` | `Items/ItemContainerPlace.cs` | `enum : byte` | `None = 0`, `Hand = 1` (`pos_hand`), `Equip = 2` (`pos_equip`), `EquipRoom = 3` (`pos_equiproom`), `RepositoryRoom = 4` (`pos_repositoryroom`), `Trade = 5` (`pos_trade`), `Immediacy = 7` (`pos_immediacy`) |
| `GroundObjectKind` | `Items/GroundObjectKind.cs` | `enum : byte` | `Item = 0`, `Money = 1`, `Prop = 2`, `Box = 3`, `Switch = 4`, `Door = 5`, `TrapObj = 6` |
| `ItemBindState` | `Items/ItemBindState.cs` | `enum : byte` | `Unbound = 0`, `BoundPermanent = 1`, `UnbindingPending = 2` |
| `ItemConsts` | `Items/ItemConsts.cs` | `static class` | `GeneratorVersion = 4`, `MaxMagicAffixSlots = 6`, `EquipRoomWidthCells = 6`, `EquipRoomHeightCells = 10`, `ImmediacyBeltSlots = 3`, `DefaultGroundObjectLifeFrames = 1080`, `DefaultLootOwnerProtectionFrames = 180`, `MaxGroundPickupDistanceMps = 192` |

---

## 2. Tier 2a: `JXServer.GameAssets` (`JXData Standard v1` Catalogs & `MemoryPack` Snapshots)

### `JXServer.GameAssets/Skills/`
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `SkillParameterCurveTemplate` | `Skills/SkillParameterCurveTemplate.cs` | `sealed record` | `[MemoryPackable]` 3-parameter curve (`CurveKind`: `Constant`, `Linear`, `Piecewise`, `Conic`, `Sqrt`, `Table`; `BaseValue`, `PerLevelValue`, `ValuesByLevel`, `Points`) with `int Evaluate(int skillLevel)`. |
| `SkillEffectScalingTemplate` | `Skills/SkillEffectScalingTemplate.cs` | `sealed record` | `[MemoryPackable]` `int AttributeId`, `string AttributeCode`, `string SlotCategory`, `SkillParameterCurveTemplate? Param0`, `SkillParameterCurveTemplate? Param1`, `SkillParameterCurveTemplate? Param2`. |
| `SkillCostScalingTemplate` | `Skills/SkillCostScalingTemplate.cs` | `sealed record` | `[MemoryPackable]` `string CostType` (`mana`, `life`, `stamina`, `money`), `int BaseMana`, `int ManaPerLevel`, `int BaseStamina`, `int BaseLife`, `IReadOnlyList<int> ValuesByLevel` with `int EvaluateCost(int skillLevel)`. |
| `SkillTemplate` | `Skills/SkillTemplate.cs` | `sealed record` | `[MemoryPackable]` `int Id`, `string Code`, `string Name`, `SkillStyleKind Style`, `ElementSeries ElementSeries`, `bool IsOffensive`, `bool IsPhysical`, `bool IsMelee`, `bool IsAura`, `bool IsWeaponSkill`, `bool IsExpSkill`, `bool UseAttackRating`, `bool DoHurtAnimation`, `bool PeaceCanUse`, `int ReqLevel`, `int MaxLevel`, `int EquipLimit`, `int HorseLimit`, `int CastRangeMps`, `int WaitFrames`, `int MissileId`, `string MissileForm`, `int ChildSkillId`, `int ChildSkillLevel`, `int ChildSkillCount`, `SkillCostScalingTemplate CostScaling`, `IReadOnlyList<SkillEffectScalingTemplate> EffectScaling`. |
| `MissileTemplate` | `Skills/MissileTemplate.cs` | `sealed record` | `[MemoryPackable]` `int Id`, `string Code`, `string Name`, `MissileMoveKind MoveKind`, `string FollowKind`, `int SpeedMpsPerFrame`, `int HeightMps`, `int ZSpeedMpsPerFrame`, `int ZAccelMpsPerFrame2`, `int LifeFrames`, `int HitRadiusMps`, `int CollideRadiusCells`, `int DamageRadiusCells`, `bool IsRangeDamage`, `int DamageIntervalFrames`, `int PierceCount`, `bool CollideVanish`, `bool CanCollideFriend`, `bool AutoExplode`, `bool MustHit`, `int SubMissileId`, `int Param1`, `int Param2`, `int Param3`. |
| `ISkillTemplateCatalog` | `Skills/ISkillTemplateCatalog.cs` | `interface` | `bool TryGetSkill(int skillId, out SkillTemplate? skill)`, `bool TryGetSkillByCode(string code, out SkillTemplate? skill)`, `int GetBasicWeaponSkillId(int weaponDetailOrParticular)` |
| `IMissileTemplateCatalog` | `Skills/IMissileTemplateCatalog.cs` | `interface` | `bool TryGetMissile(int missileId, out MissileTemplate? missile)`, `bool TryGetMissileByCode(string code, out MissileTemplate? missile)` |
| `SkillTemplateCatalog` | `Skills/SkillTemplateCatalog.cs` | `sealed class` | Loads `data/skills/*.skills.json` (or `artifacts/skills.pack`) into `FrozenDictionary<int, SkillTemplate>` and `FrozenDictionary<string, SkillTemplate>`. |
| `MissileTemplateCatalog` | `Skills/MissileTemplateCatalog.cs` | `sealed class` | Loads `data/skills/missiles.skills.json` (or `artifacts/skills.pack`) into `FrozenDictionary<int, MissileTemplate>`. |

### `JXServer.GameAssets/Items/`
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `ItemBaseAttributeTemplate` | `Items/ItemBaseAttributeTemplate.cs` | `sealed record` | `[MemoryPackable]` `int AttributeId`, `string AttributeCode`, `int MinValue`, `int MaxValue`, `int DurationFrames`. |
| `ItemRequirementTemplate` | `Items/ItemRequirementTemplate.cs` | `sealed record` | `[MemoryPackable]` `int RequirementType`, `string RequirementCode` (`strength`, `dexterity`, `vitality`, `energy`, `level`, `series`, `gender`, `faction`), `int MinValue`. |
| `ItemTemplate` | `Items/ItemTemplate.cs` | `sealed record` | `[MemoryPackable]` `int Id`, `string Code`, `string Name`, `ItemGenreKind Genre`, `ItemQualityKind Quality`, `int DetailType`, `int ParticularType`, `byte Level`, `ElementSeries ElementSeries`, `byte GridWidthCells`, `byte GridHeightCells`, `int ObjDataId`, `int AppearanceResId`, `int PriceGold`, `int MaxStackCount`, `IReadOnlyList<ItemBaseAttributeTemplate> BaseAttributes`, `IReadOnlyList<ItemRequirementTemplate> Requirements`. |
| `MagicAffixTemplate` | `Items/MagicAffixTemplate.cs` | `sealed record` | `[MemoryPackable]` `int Id`, `string Code`, `int AttributeId`, `string AttributeCode`, `string NameTemplate`, `bool IsSuffix` (`true` = hidden Five-Elements slot `1, 3, 5`; `false` = explicit slot `0, 2, 4`), `bool IsGoldenOnly`, `ElementSeries ElementSeries`, `int Level`, `int DropWeight`, `EquipPartWeightsTemplate WeightsByEquipPart`, `SeriesWeightsTemplate WeightsBySeries`, `int Param0Min`, `int Param0Max`, `int Param1Min`, `int Param1Max`, `int Param2Min`, `int Param2Max`. |
| `GroundObjectTemplate` | `Items/GroundObjectTemplate.cs` | `sealed record` | `[MemoryPackable]` `int Id`, `string Code`, `string Name`, `GroundObjectKind Kind`, `int LifeFrames`, `int ObstacleKind`, `string? ScriptPath`. |
| `IItemTemplateCatalog` | `Items/IItemTemplateCatalog.cs` | `interface` | `bool TryGetItemByCode(string code, out ItemTemplate? item)`, `bool TryGetItem(ItemGenreKind genre, int detailType, int particularType, byte level, out ItemTemplate? item)`, `IReadOnlyList<MagicAffixTemplate> GetCandidateMagicAffixes(bool isSuffix, int equipDetailType, int weaponParticularType, ElementSeries series, int maxLevel)`, `bool TryGetGroundObject(int objDataId, out GroundObjectTemplate? objTemplate)` |
| `ItemTemplateCatalog` | `Items/ItemTemplateCatalog.cs` | `sealed class` | Loads `data/items/*.items.json` (or `artifacts/items.pack`) and indexes items by `Code` and `(Genre, DetailType, ParticularType, Level)` plus pre-bucketed `MagicAffixTemplate` lookup tables. |
| `ItemGenerator` | `Items/ItemGenerator.cs` | `sealed class` | Deterministic C# port of `KItemGenerator::Gen_Equipment`, `Gen_Medicine`, `Gen_TownPortal`, and `Gen_MagicAttrib` (`m_wVersion = 4`), rolling base durability, base defense/damage, and up to 6 `ItemMagicAttributeSlot` values from `RandomSeed`, `Series`, `Level`, and `Luck`. |

### `JXServer.GameAssets/LootTables/`
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `WeightedLootPoolEntryTemplate` | `LootTables/WeightedLootPoolEntryTemplate.cs` | `sealed record` | `[MemoryPackable]` `int Weight`, `string? ItemCode`, `ItemGenreKind? Genre`, `ItemQualityKind Quality`, `int? DetailType`, `int? ParticularType`, `ElementSeries ElementSeries`, `byte MinLevel`, `byte MaxLevel`, `int? GoldEquipId`. |
| `LootTableTemplate` | `LootTables/LootTableTemplate.cs` | `sealed record` | `[MemoryPackable]` `int Id`, `string Code`, `int RandRange` (`1000000`), `int MagicRate`, `double MoneyDropChance`, `int MinMoneyGold`, `int MaxMoneyGold`, `byte MinItemLevel`, `byte MaxItemLevel`, `int MaxDropsPerKill`, `double BlueAffixLuckFactor`, `IReadOnlyList<WeightedLootPoolEntryTemplate> WeightedPools`. |
| `ILootTableCatalog` | `LootTables/ILootTableCatalog.cs` | `interface` | `bool TryGetLootTable(string code, out LootTableTemplate? lootTable)` |
| `LootTableCatalog` | `LootTables/LootTableCatalog.cs` | `sealed class` | Loads `data/loot/*.loot.json` (or `artifacts/loot.pack`) into `FrozenDictionary<string, LootTableTemplate>`. |

*(Note: `MapDescriptor`/`MapCatalog`, `NpcTemplate`/`NpcTemplateCatalog`, and `PlayerTemplateCatalog` in `JXServer.GameAssets` are updated to load directly from `data/maps/*.map.json`, `data/npcs/*.npcs.json`, and `data/players/*.players.json` — or `artifacts/*.pack` — including monster `combatScaling`, `skills`, `treasureDropCount`, `lootTableCode`, `seriesGrowthRules`, `levelExpTable`, and `starterItems`.)*

---

## 3. Tier 3: `JXServer.Contracts`, `JXServer.Domain`, `JXServer.Application`, `JXServer.Infrastructure`

### `JXServer.Domain/Characters/`
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `CharacterSkillValue` | `Characters/CharacterSkillValue.cs` | `sealed record` | Value object persisted in `Character.Skills`: `int SkillId`, `byte BaseLevel`, `byte BonusLevel`, `short ExpPercent`. |
| `ItemMagicAttributeValue` | `Characters/ItemMagicAttributeValue.cs` | `sealed record` | Value object persisted in `CharacterItem.MagicAttributes` (6 slots): `short AttributeId`, `int Value0`, `int Value1`, `int Value2`, `byte EnchaseLevel`. |
| `CharacterItem` | `Characters/CharacterItem.cs` | `sealed class` | Entity owned by `Character` aggregate (matching Section 6.2 of `docs/data-architecture-standard.md`): `Guid Id`, `Guid CharacterId`, `string ItemCode`, `ItemGenreKind Genre`, `short DetailType`, `ushort ParticularType`, `ItemQualityKind Quality`, `ElementSeries Series`, `byte Level`, `byte Luck`, `uint RandomSeed`, `ItemContainerPlace ContainerPlace`, `byte GridX`, `byte GridY`, `ushort StackCount`, `short CurrentDurability`, `short MaxDurability`, `bool IsBroken`, `ItemBindState BindState`, `DateTimeOffset? UnbindReadyAtUtc`, `DateTimeOffset? ExpireAtUtc`, `int RemainingOnlineSeconds`, `IReadOnlyList<ItemMagicAttributeValue> MagicAttributes` (6 slots). |
| `Character` *(Updated)* | `Characters/Character.cs` | `sealed class` | Aggregate root extended with `PlayerPkMode PkState`, `byte PkValue`, `int Repute`, `int FuYuan`, `int Honor`, `byte TransLifeCount`, `int ZhenYuan`, `int UnallocatedAttributePoints`, `int UnallocatedSkillPoints`, `int ActiveAuraSkillId`, `IReadOnlyDictionary<ushort, int> TaskValues`, `IReadOnlyList<CharacterSkillValue> Skills`, and `IReadOnlyList<CharacterItem> Items`, plus domain methods `ApplyProgressionAndInventorySnapshot(...)`. |

### `JXServer.Contracts/Characters/`
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `CharacterSkillDto` | `Characters/CharacterSkillDto.cs` | `sealed record` | `int SkillId`, `byte BaseLevel`, `byte BonusLevel`, `short ExpPercent`. |
| `CharacterItemMagicAttributeDto` | `Characters/CharacterItemMagicAttributeDto.cs` | `sealed record` | `short AttributeId`, `int Value0`, `int Value1`, `int Value2`, `byte EnchaseLevel`. |
| `CharacterItemDto` | `Characters/CharacterItemDto.cs` | `sealed record` | Read/Sync DTO for `CharacterItem` (carries `Guid Id`, `string ItemCode`, classification, container coordinates, durability, stack count, `RandomSeed`, and 6 `CharacterItemMagicAttributeDto` slots). |
| `UpdateCharacterWorldSnapshotDto` *(Updated)* | `Characters/UpdateCharacterWorldSnapshotDto.cs` | `sealed record` | Extended with `ushort Level`, `long Experience`, `long LeadExperience`, `ushort BaseStrength`, `ushort BaseDexterity`, `ushort BaseVitality`, `ushort BaseEnergy`, `int UnallocatedAttributePoints`, `int UnallocatedSkillPoints`, `int MaxLife`, `int MaxMana`, `ushort MaxStamina`, `uint InventoryMoney`, `uint RepositoryMoney`, `PlayerPkMode PkState`, `byte PkValue`, `int ActiveAuraSkillId`, `IReadOnlyList<CharacterSkillDto> Skills`, and `IReadOnlyList<CharacterItemDto> Items`. |

---

## 4. Tier 2b: `JXServer.ZoneEngine` (Sharded 18-Hz Combat, Missiles, Monster AI, Ground Loot & Inventory)

### `JXServer.ZoneEngine/Combat/`
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `SkillDamagePayload` | `Combat/SkillDamagePayload.cs` | `readonly record struct` | Pre-computed 6-channel damage & status effect payload attached to a melee hit or `ActiveMissileSlot`: `int AttackRating`, `int IgnoreDefensePercent`, `int PhysicsMin`, `int PhysicsMax`, `int ColdMin`, `int ColdMax`, `int ColdFreezeFrames`, `int FireMin`, `int FireMax`, `int LightMin`, `int LightMax`, `int LightStunFrames`, `int PoisonDamagePerTick`, `int PoisonDurationFrames`, `int PoisonIntervalFrames`, `int MagicMin`, `int MagicMax`, `bool UseAttackRating`, `bool DoHurtAnimation`, `bool IsMelee`. |
| `ActiveMissileSlot` | `Combat/ActiveMissileSlot.cs` | `sealed class` | Pooled 18-Hz projectile slot: `int SlotIndex`, `bool IsActive`, `uint CasterNpcId`, `Guid? CasterCharacterId`, `EntityKind CasterKind`, `EntityCamp CasterCamp`, `int SkillId`, `byte SkillLevel`, `int MissileTemplateId`, `MissileMoveKind MoveKind`, `int CurrentMpsX`, `int CurrentMpsY`, `int FixedX` (`MpsX << 10`), `int FixedY` (`MpsY << 10`), `int StepFixedX`, `int StepFixedY`, `byte DirIndex` (`0..63`), `int RemainingLifeFrames`, `int HitRadiusMps`, `int DamageIntervalFrames`, `int RemainingPierceCount`, `bool CollideVanish`, `uint LockedTargetNpcId`, `SkillDamagePayload DamagePayload`, `uint LastHitNpcId`, `long LastHitTick`. |
| `EntityStatusEffectState` | `Combat/EntityStatusEffectState.cs` | `struct` | Tracks active Cold slow (`FreezeRemainingFrames`), Poison DoT (`PoisonRemainingFrames`, `PoisonDamagePerTick`, `PoisonIntervalFrames`, `NextPoisonTickFrame`, `PoisonAttackerNpcId`), Lightning Stun (`StunRemainingFrames`), and Hurt recovery (`HurtRecoveryRemainingFrames`). |
| `CombatDamageCalculator` | `Combat/CombatDamageCalculator.cs` | `static class` | Evaluates `KNpcSet::GetRelation`, `KNpc::AppendSkillEffect`, `KNpc::CheckHitTarget`, `KNpc::ReceiveDamage`, and `KNpc::CalcDamage` deterministically. |
| `MissileSimulationTickSystem` | `Combat/MissileSimulationTickSystem.cs` | `sealed class` | Advances all active `ActiveMissileSlot` instances each 18-Hz tick along their 64-direction trajectory, checks map barrier collisions (`Obstacle_Full`/`Obstacle_Fly`), detects hostile entity hits in surrounding regions, applies `CombatDamageCalculator`, and recycles expired slots. |
| `MonsterAiTickSystem` | `Combat/MonsterAiTickSystem.cs` | `sealed class` | Steps all `NpcZoneState` monsters each 18-Hz tick through `IdlePatrol`, `Pursuit`, `Attack`, `LeashReturn`, and `DeadRespawnWait`, plus ticks active Poison/Freeze/Stun status effects on both monsters and players. |
| `CastSkillZoneCommand` | `Combat/CastSkillZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `int SkillId`, `int TargetMpsX` (`-1` if entity-locked), `int TargetMpsYOrNpcId`. |
| `ChangeAuraSkillZoneCommand` | `Combat/ChangeAuraSkillZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `int AuraSkillId`. |
| `ChangePkModeZoneCommand` | `Combat/ChangePkModeZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `PlayerPkMode RequestedPkMode`. |
| `AllocateBaseAttributeZoneCommand` | `Combat/AllocateBaseAttributeZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `BaseAttributeKind Attribute`, `int PointsToAdd`. |
| `AllocateSkillPointZoneCommand` | `Combat/AllocateSkillPointZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `int SkillId`, `int PointsToAdd`. |
| `RevivePlayerZoneCommand` | `Combat/RevivePlayerZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `int ReviveType`. |

### `JXServer.ZoneEngine/Items/`
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `PlayerItemSlot` | `Items/PlayerItemSlot.cs` | `sealed class` | In-memory runtime representation of an item owned by a player in a zone: `uint RuntimeItemId` (`m_ID` sent in `0x5F`/`0x60`), `Guid DomainItemId`, `ItemTemplate Template`, `ItemQualityKind Quality`, `ElementSeries Series`, `byte Level`, `byte Luck`, `uint RandomSeed`, `ItemContainerPlace ContainerPlace`, `byte GridX`, `byte GridY`, `ushort StackCount`, `short CurrentDurability`, `short MaxDurability`, `bool IsBroken`, `ItemMagicAttributeValue[] MagicAttributes` (6 slots), `bool[] ActiveHiddenSlots` (3 flags for slots `1, 3, 5`). |
| `PlayerInventoryGrid` | `Items/PlayerInventoryGrid.cs` | `sealed class` | Manages a player's `pos_hand`, `pos_equip[15]`, `pos_equiproom[6, 10]` occupancy grid, and `pos_immediacy[3]` belt. Provides `TryFindFreeRect(byte width, byte height, out byte gridX, out byte gridY)`, `TryPlaceItem`, `TryMoveOrSwapItem`, `CanEquipItem`, and `RecalculateEquipmentAndFiveElementsActivation(PlayerZoneState player)` (implementing `KItemList::ms_ActivedEquip`). |
| `GroundObjectSlot` | `Items/GroundObjectSlot.cs` | `sealed class` | Live ground object in `SpatialRegionSlot.GroundObjects`: `int ObjId`, `int ObjDataId`, `GroundObjectKind Kind`, `string DisplayNameUtf8`, `ushort MapId`, `int MpsX`, `int MpsY`, `int RegionX`, `int RegionY`, `byte DirIndex`, `int MoneyAmount`, `PlayerItemSlot? ItemPayload`, `byte ColorId` (`0`=White, `1`=Blue, `2`=Purple, `3`=Gold), `Guid? OwnerCharacterId`, `long OwnerProtectionExpiryTick`, `long ExpireTick`. |
| `GroundObjectTickSystem` | `Items/GroundObjectTickSystem.cs` | `sealed class` | Expires ground objects whose `ExpireTick <= currentTick` and broadcasts `0x67` (`s2c_objremove`, `Reason = 0`) to the `3x3` region AOI; also ticks active gradual medicine healing/mana restoration (`MedicineRegenState`) on players. |
| `PickupGroundObjectZoneCommand` | `Items/PickupGroundObjectZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `int ObjId`, `ItemContainerPlace TargetPlace`, `byte TargetGridX`, `byte TargetGridY`. |
| `MoveInventoryItemZoneCommand` | `Items/MoveInventoryItemZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `ItemContainerPlace SrcPlace`, `byte SrcGridX`, `byte SrcGridY`, `ItemContainerPlace DestPlace`, `byte DestGridX`, `byte DestGridY`. |
| `UseInventoryItemZoneCommand` | `Items/UseInventoryItemZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `uint RuntimeItemId`, `ItemContainerPlace Place`, `byte GridX`, `byte GridY`, `bool IsEmergencyTownPortal`. |
| `DropInventoryItemZoneCommand` | `Items/DropInventoryItemZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `bool ThrowAllMedicine`. |
| `DivideInventoryItemZoneCommand` | `Items/DivideInventoryItemZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `uint RuntimeItemId`, `byte SrcPlace`, `byte SrcGridX`, `byte SrcGridY`, `ushort DivideCount`. |
| `RequestGroundObjectSyncZoneCommand` | `Items/RequestGroundObjectSyncZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `int ObjId`. |

---

## 5. Tier 4a: `JXServer.Adapter.LegacyJX1` (Wire Packets & Handlers)

### `JXServer.Adapter.LegacyJX1/Packets/` (One-Type-Per-File `readonly record struct`)
| Packet Struct Name | Opcode | Direction | Wire Size | File Path |
| :--- | :--- | :--- | :--- | :--- |
| `NpcSkillC2SPacket` | `0x4D` | `C2S` | `17B` (`<BiiiI`) | `Packets/NpcSkillC2SPacket.cs` |
| `AutoAttackNpcC2SPacket` | `0x87` | `C2S` | `26B` | `Packets/AutoAttackNpcC2SPacket.cs` |
| `ChangeAuraSkillC2SPacket` | `0x6F` | `C2S` | `5B` (`<Bi`) | `Packets/ChangeAuraSkillC2SPacket.cs` |
| `PkApplyChangeNormalFlagC2SPacket` | `0x76` | `C2S` | `2B` (`<BB`) | `Packets/PkApplyChangeNormalFlagC2SPacket.cs` |
| `PlayerAddBaseAttributeC2SPacket` | `0x57` | `C2S` | `14B` (`<BBiII`) | `Packets/PlayerAddBaseAttributeC2SPacket.cs` |
| `PlayerApplyAddSkillPointC2SPacket` | `0x58` | `C2S` | `17B` (`<BiiII`) | `Packets/PlayerApplyAddSkillPointC2SPacket.cs` |
| `PlayerReviveC2SPacket` | `0x74` | `C2S` | `13B` (`<BiII`) | `Packets/PlayerReviveC2SPacket.cs` |
| `RequestObjC2SPacket` | `0x4A` | `C2S` | `5B` (`<Bi`) | `Packets/RequestObjC2SPacket.cs` |
| `ObjMouseClickC2SPacket` | `0x72` | `C2S` | `9B` (`<BII`) | `Packets/ObjMouseClickC2SPacket.cs` |
| `PlayerPickupItemC2SPacket` | `0x5A` | `C2S` | `12B` (`<BBBBiI`) | `Packets/PlayerPickupItemC2SPacket.cs` |
| `PlayerMoveItemC2SPacket` | `0x5B` | `C2S` | `7B` (`<BBBBBBB`) | `Packets/PlayerMoveItemC2SPacket.cs` |
| `PlayerEatItemC2SPacket` | `0x59` | `C2S` | `16B` (`<BbIbBII`) | `Packets/PlayerEatItemC2SPacket.cs` |
| `PlayerThrowAwayItemC2SPacket` | `0x5E` | `C2S` | `1B` (`<B`) | `Packets/PlayerThrowAwayItemC2SPacket.cs` |
| `ThrowAwayAllMedicineC2SPacket` | `0x83` | `C2S` | `4B` (`<BBBB`) | `Packets/ThrowAwayAllMedicineC2SPacket.cs` |
| `PlayerDivideItemC2SPacket` | `0x85` | `C2S` | `10B` (`<BIHBBBB`) | `Packets/PlayerDivideItemC2SPacket.cs` |
| `SkillCastS2CPacket` | `0x5A` | `S2C` | `25B` (`<BiIiIII`) | `Packets/SkillCastS2CPacket.cs` |
| `CastSkillDirectlyS2CPacket` | `0x85` | `S2C` | `25B` (`<BiIiIII`) | `Packets/CastSkillDirectlyS2CPacket.cs` |
| `BroadcastNetCommandS2CPacket` | `0x56` | `S2C` | `22B` (`<BIBiiii`) | `Packets/BroadcastNetCommandS2CPacket.cs` |
| `NpcDeathS2CPacket` | `0x57` | `S2C` | `5B` (`<BI`) | `Packets/NpcDeathS2CPacket.cs` |
| `NpcReviveS2CPacket` | `0x89` | `S2C` | `6B` (`<BIB`) | `Packets/NpcReviveS2CPacket.cs` |
| `PlayerAttributeS2CPacket` | `0x5D` | `S2C` | `14B` (`<BBiii`) | `Packets/PlayerAttributeS2CPacket.cs` |
| `PlayerSkillLevelS2CPacket` | `0x5E` | `S2C` | `15B` (`<Biiih`) | `Packets/PlayerSkillLevelS2CPacket.cs` |
| `PlayerExpS2CPacket` | `0x5C` | `S2C` | `5B` (`<Bi`) | `Packets/PlayerExpS2CPacket.cs` |
| `PlayerLevelUpS2CPacket` | `0x5B` | `S2C` | `17B` (`<Biiii`) | `Packets/PlayerLevelUpS2CPacket.cs` |
| `ObjAddS2CPacket` | `0x64` | `S2C` | `35 + N` | `Packets/ObjAddS2CPacket.cs` |
| `ObjRemoveS2CPacket` | `0x67` | `S2C` | `6B` (`<BiB`) | `Packets/ObjRemoveS2CPacket.cs` |
| `SyncItemS2CPacket` | `0x5F` | `S2C` | `80B` | `Packets/SyncItemS2CPacket.cs` |
| `RemoveItemS2CPacket` | `0x60` | `S2C` | `5B` (`<BI`) | `Packets/RemoveItemS2CPacket.cs` |
| `SyncMoneyS2CPacket` | `0x68` | `S2C` | `13B` (`<Biii`) | `Packets/SyncMoneyS2CPacket.cs` |
| `PlayerMoveItemS2CPacket` | `0x61` | `S2C` | `7B` (`<BBBBBBB`) | `Packets/PlayerMoveItemS2CPacket.cs` |
| `ItemAutoMoveS2CPacket` | `0x8E` | `S2C` | `1B` (`<B`) | `Packets/ItemAutoMoveS2CPacket.cs` |

### `JXServer.Adapter.LegacyJX1/GameServer/` (15 New Sealed Packet Handlers)
- `NpcSkillPacketHandler` (`0x4D`), `AutoAttackNpcPacketHandler` (`0x87`), `ChangeAuraSkillPacketHandler` (`0x6F`), `PkApplyChangeNormalFlagPacketHandler` (`0x76`), `PlayerAddBaseAttributePacketHandler` (`0x57`), `PlayerApplyAddSkillPointPacketHandler` (`0x58`), `PlayerRevivePacketHandler` (`0x74`), `RequestObjPacketHandler` (`0x4A`), `ObjMouseClickPacketHandler` (`0x72`), `PlayerPickupItemPacketHandler` (`0x5A`), `PlayerMoveItemPacketHandler` (`0x5B`), `PlayerEatItemPacketHandler` (`0x59`), `PlayerThrowAwayItemPacketHandler` (`0x5E`), `ThrowAwayAllMedicinePacketHandler` (`0x83`), `PlayerDivideItemPacketHandler` (`0x85`).

---

## 6. Offline Migration Tool: `tools/JXServer.DataExporter`

| Type Name | File Path | Kind | Responsibilities |
| :--- | :--- | :--- | :--- |
| `Program` | `tools/JXServer.DataExporter/Program.cs` | `static class` | CLI entry point: `--source d:\Projects\jx-linux-bin\server1 --data-dir data --schemas-dir schemas --artifacts-dir artifacts`. |
| `MapDataExporter` | `tools/JXServer.DataExporter/Exporters/MapDataExporter.cs` | `sealed class` | Exports unified `data/maps/<id>_<code-slug>.map.json` (combining `maplist.ini`, `Region_S.dat` Sections `0..3` with RLE+Base64 obstacles, `trap/*.lua` portals, and `autoexec.lua` spawns). |
| `NpcAndLootDataExporter` | `tools/JXServer.DataExporter/Exporters/NpcAndLootDataExporter.cs` | `sealed class` | Exports `data/npcs/core.npcs.json` (evaluating `npclevelscript/*.lua` curves) and `data/loot/field-monsters.loot.json` (from `settings/droprate/**/*.ini`). |
| `PlayerDataExporter` | `tools/JXServer.DataExporter/Exporters/PlayerDataExporter.cs` | `sealed class` | Exports `data/players/progression.players.json` (combining `newplayerini00..09.ini`, `level_add.txt`, `level_exp.txt`, `series.ini`, `revivepos.ini`, and `starterItems`). |
| `SkillAndMissileDataExporter` | `tools/JXServer.DataExporter/Exporters/SkillAndMissileDataExporter.cs` | `sealed class` | Exports `data/skills/base-and-factions.skills.json` and `data/skills/missiles.skills.json` (combining `skills.txt`, `missles.txt`, `clientweaponskill.txt`, and evaluated `script/skill/**/*.lua` curves). |
| `ItemAndAffixDataExporter` | `tools/JXServer.DataExporter/Exporters/ItemAndAffixDataExporter.cs` | `sealed class` | Exports `data/items/*.items.json` from `settings/item/004/*.txt` (`m_wVersion = 4`) and `settings/obj/objdata.txt`. |
| `MemoryPackSnapshotCompiler` | `tools/JXServer.DataExporter/Compilation/MemoryPackSnapshotCompiler.cs` | `sealed class` | Validates all `data/**/*.json` files against `schemas/*.schema.json` and compiles `artifacts/*.pack` + `SHA-256` manifest. |
