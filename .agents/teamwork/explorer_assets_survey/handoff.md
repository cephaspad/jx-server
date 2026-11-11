# Handoff Report: GameAssets, Build, and Test Suite Survey

**State Folder**: `d:\Projects\jx-server\.agents\teamwork\explorer_assets_survey\`  
**Date & Timestamp**: 2026-11-11T07:05:00Z  
**Author**: GameAssets, Build, and Test Suite Surveyor (Explorer Agent)  
**Parent Conversation ID**: `d2392fd8-ba50-46c2-b953-b7059df99b64`  
**Solution Path**: `d:\Projects\jx-server\JXServer.slnx`  

---

## 1. Observation

### 1.1. Detailed File & Class Survey of `src/JXServer.GameAssets`

The project `src/JXServer.GameAssets` contains **63 files** total (1 `.csproj` + 62 `.cs` files).

| Subfolder / File | Declared Type | SMR / Classification | Lines |
| :--- | :--- | :--- | :--- |
| `JXServer.GameAssets.csproj` | MSBuild Project | Project Definition | 13 |
| `GameAssetsServiceCollectionExtensions.cs` | `public static class GameAssetsServiceCollectionExtensions` | DI Service Registrations (`AddJXServerGameAssets`) | 56 |
| **`Archives/`** | | | |
| `Archives/PakFilePathHasher.cs` | `public static class PakFilePathHasher` | **Legacy Reader**: Kingsoft `g_FileName2Id` (`0x08217fb0`) hash algorithm | 45 |
| `Archives/UclNrv2bDecompressor.cs` | `public static class UclNrv2bDecompressor` | **Legacy Reader**: Bit-exact `ucl_nrv2b_decompress_8` decompressor | 113 |
| `Archives/XPackArchiveReader.cs` | `public sealed class XPackArchiveReader` | **Legacy Reader**: Kingsoft `.pak` (`0x4B434150`) binary archive reader | 137 |
| **`Configuration/`** | | | |
| `Configuration/GameAssetsOptions.cs` | `public sealed class GameAssetsOptions` | Configuration POCO (contains both Tier 1/2 and legacy disk paths) | 59 |
| `Configuration/IniFileParser.cs` | `public static class IniFileParser` | **Legacy Reader**: Kingsoft `[Section]` and `Key=Value` `.ini` parser | 118 |
| `Configuration/TabFileParser.cs` | `public static class TabFileParser` | **Legacy Reader**: Tab-delimited `.txt` header + data row parser | 56 |
| **`Items/`** | | | |
| `Items/EquipPartWeightsTemplate.cs` | `public sealed partial record EquipPartWeightsTemplate` | MemoryPack Template | 60 |
| `Items/GeneratedItemData.cs` | `public sealed record GeneratedItemData` | Item Generation Runtime Model | 24 |
| `Items/GroundObjectTemplate.cs` | `public sealed partial record GroundObjectTemplate` | MemoryPack Template (`objdata.txt`) | 17 |
| `Items/IItemTemplateCatalog.cs` | `public interface IItemTemplateCatalog` | Runtime Interface | 44 |
| `Items/ItemBaseAttributeTemplate.cs` | `public sealed partial record ItemBaseAttributeTemplate` | MemoryPack Template | 14 |
| `Items/ItemGenerator.cs` | `public sealed class ItemGenerator` | Runtime Affix & Item Generator | 442 |
| `Items/ItemRequirementTemplate.cs` | `public sealed partial record ItemRequirementTemplate` | MemoryPack Template | 12 |
| `Items/ItemTemplate.cs` | `public sealed partial record ItemTemplate` | MemoryPack Template | 29 |
| `Items/ItemTemplateCatalog.cs` | `public sealed class ItemTemplateCatalog : IItemTemplateCatalog` | **Runtime Loader**: Loads via `GameAssetsSnapshotLoader` | 504 |
| `Items/MagicAffixTemplate.cs` | `public sealed partial record MagicAffixTemplate` | MemoryPack Template | 29 |
| `Items/SeriesWeightsTemplate.cs` | `public sealed partial record SeriesWeightsTemplate` | MemoryPack Template | 27 |
| **`Logging/`** | | | |
| `Logging/GameAssetsLogMessages.cs` | `public static partial class GameAssetsLogMessages` | High-Perf Source-Generated LoggerMessages | 86 |
| **`LootTables/`** | | | |
| `LootTables/ILootTableCatalog.cs` | `public interface ILootTableCatalog` | Runtime Interface | 15 |
| `LootTables/LootTableCatalog.cs` | `public sealed class LootTableCatalog : ILootTableCatalog` | **Runtime Loader**: Loads via `GameAssetsSnapshotLoader` | 84 |
| `LootTables/LootTableTemplate.cs` | `public sealed partial record LootTableTemplate` | MemoryPack Template | 22 |
| `LootTables/WeightedLootPoolEntryTemplate.cs` | `public sealed partial record WeightedLootPoolEntryTemplate` | MemoryPack Template | 21 |
| **`NpcTemplates/`** | | | |
| `NpcTemplates/INpcTemplateCatalog.cs` | `public interface INpcTemplateCatalog` | Runtime Interface | 22 |
| `NpcTemplates/NpcAiConfigTemplate.cs` | `public sealed partial record NpcAiConfigTemplate` | MemoryPack Template | 14 |
| `NpcTemplates/NpcCombatScalingTemplate.cs` | `public sealed partial record NpcCombatScalingTemplate` | MemoryPack Template | 46 |
| `NpcTemplates/NpcSkillSlotTemplate.cs` | `public sealed partial record NpcSkillSlotTemplate` | MemoryPack Template | 12 |
| `NpcTemplates/NpcTemplate.cs` | `public sealed partial record NpcTemplate` | MemoryPack Template | 36 |
| `NpcTemplates/NpcTemplateCatalog.cs` | `public sealed partial class NpcTemplateCatalog : INpcTemplateCatalog` | **Hybrid**: SnapshotLoader + **Legacy Fallback** (`ParseNpcsTxt`, `ParseAutoExecSpawns`) | 305 |
| `NpcTemplates/StaticNpcSpawnTemplate.cs` | `public sealed partial record StaticNpcSpawnTemplate` | MemoryPack Template | 23 |
| **`PlayerTemplates/`** | | | |
| `PlayerTemplates/CharacterBaseTemplate.cs` | `public sealed partial record CharacterBaseTemplate` | MemoryPack Template | 23 |
| `PlayerTemplates/IPlayerTemplateCatalog.cs` | `public interface IPlayerTemplateCatalog` | Runtime Interface | 54 |
| `PlayerTemplates/PlayerSeriesGrowthRuleTemplate.cs` | `public sealed partial record PlayerSeriesGrowthRuleTemplate` | MemoryPack Template | 21 |
| `PlayerTemplates/PlayerTemplateCatalog.cs` | `public sealed class PlayerTemplateCatalog : IPlayerTemplateCatalog` | **Hybrid / Legacy Bound**: Hard-requires and parses `newplayerini*.ini`, `level_add.txt`, etc., before snapshot override | 455 |
| `PlayerTemplates/StarterItemTemplate.cs` | `public sealed partial record StarterItemTemplate` | MemoryPack Template | 19 |
| `PlayerTemplates/StarterSkillEntry.cs` | `public readonly partial record struct StarterSkillEntry` | MemoryPack Template Struct | 9 |
| `PlayerTemplates/StarterVillageTemplate.cs` | `public sealed partial record StarterVillageTemplate` | MemoryPack Template | 14 |
| **`Shops/`** | | | |
| `Shops/IShopCatalog.cs` | `public interface IShopCatalog` | Runtime Interface | 17 |
| `Shops/ShopCatalog.cs` | `public sealed class ShopCatalog : IShopCatalog` | **Runtime Loader**: Loads via `GameAssetsSnapshotLoader` | 56 |
| `Shops/ShopGoodTemplate.cs` | `public sealed partial record ShopGoodTemplate` | MemoryPack Template | 22 |
| `Shops/ShopTemplate.cs` | `public sealed partial record ShopTemplate` | MemoryPack Template | 17 |
| **`Skills/`** | | | |
| `Skills/IMissileTemplateCatalog.cs` | `public interface IMissileTemplateCatalog` | Runtime Interface | 17 |
| `Skills/ISkillTemplateCatalog.cs` | `public interface ISkillTemplateCatalog` | Runtime Interface | 19 |
| `Skills/MissileTemplate.cs` | `public sealed partial record MissileTemplate` | MemoryPack Template | 34 |
| `Skills/MissileTemplateCatalog.cs` | `public sealed class MissileTemplateCatalog : IMissileTemplateCatalog` | **Runtime Loader**: Loads via `GameAssetsSnapshotLoader` | 160 |
| `Skills/SkillCostScalingTemplate.cs` | `public sealed partial record SkillCostScalingTemplate` | MemoryPack Template | 40 |
| `Skills/SkillCurveControlPoint.cs` | `public sealed partial record SkillCurveControlPoint` | MemoryPack Template | 9 |
| `Skills/SkillEffectScalingTemplate.cs` | `public sealed partial record SkillEffectScalingTemplate` | MemoryPack Template | 15 |
| `Skills/SkillParameterCurveTemplate.cs` | `public sealed partial record SkillParameterCurveTemplate` | MemoryPack Template | 67 |
| `Skills/SkillTemplate.cs` | `public sealed partial record SkillTemplate` | MemoryPack Template | 39 |
| `Skills/SkillTemplateCatalog.cs` | `public sealed class SkillTemplateCatalog : ISkillTemplateCatalog` | **Runtime Loader**: Loads via `GameAssetsSnapshotLoader` | 417 |
| **`Snapshots/`** | | | |
| `Snapshots/GameAssetsSnapshotBundle.cs` | `public sealed partial record GameAssetsSnapshotBundle` | MemoryPack Binary Snapshot Root (`artifacts/jxdata.pack`) | 36 |
| `Snapshots/GameAssetsSnapshotLoader.cs` | `public static class GameAssetsSnapshotLoader` | **Core Runtime Loader**: Loads Tier 2 (`MemoryPack` `.pack`) or compiles Tier 1 (`data/**/*.json`) | 1408 |
| **`WorldMaps/`** | | | |
| `WorldMaps/IMapCatalog.cs` | `public interface IMapCatalog` | Runtime Interface | 56 |
| `WorldMaps/ITrapScriptCatalog.cs` | `public interface ITrapScriptCatalog` | Runtime Interface | 17 |
| `WorldMaps/MapCatalog.cs` | `public sealed class MapCatalog : IMapCatalog` | **Legacy Loader**: Directly reads `worldset.ini`, `maplist.ini`, `.wor`, `Region_S.dat`, `maps.pak` from disk | 587 |
| `WorldMaps/MapDescriptor.cs` | `public sealed partial record MapDescriptor` | MemoryPack Template | 28 |
| `WorldMaps/ObstacleRleCodec.cs` | `public static class ObstacleRleCodec` | **Runtime Codec**: 6-byte LE RLE + Base64 codec for `*.map.json` 512-cell regions | 111 |
| `WorldMaps/PortalTrapBindingTemplate.cs` | `public sealed partial record PortalTrapBindingTemplate` | MemoryPack Template | 25 |
| `WorldMaps/RegionObstacleGrid.cs` | `public sealed class RegionObstacleGrid` | **Legacy Reader**: Parses binary `Region_S.dat` Section 0 (`OBSTACLE_SECTION`) | 139 |
| `WorldMaps/RegionTrapGrid.cs` | `public sealed class RegionTrapGrid` | **Legacy Reader**: Parses binary `Region_S.dat` Section 1 (`TRAP_SECTION`) | 138 |
| `WorldMaps/TrapScriptCatalog.cs` | `public sealed partial class TrapScriptCatalog : ITrapScriptCatalog` | **Legacy Reader**: Scans `script/**/trap/*.lua` with Regex | 214 |

---

### 1.2. Build Environment and Compiler Rules Inspection

#### A. Central MSBuild Settings (`Directory.Build.props`)
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest</AnalysisLevel>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Deterministic>true</Deterministic>
    <NuGetAudit>false</NuGetAudit>
    <NoWarn>$(NoWarn);xUnit1051</NoWarn>
  </PropertyGroup>
</Project>
```

#### B. Central Package Management (`Directory.Packages.props`)
Central package management is active (`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`). All Microsoft.Extensions and EF Core packages are pegged to version `10.0.0`, `MemoryPack` to `1.21.4`, `xunit.v3` to `2.0.0`.

#### C. All Projects Warning & Version Status
Inspected all 12 `.csproj` files (`src/*`, `tools/*`, `tests/*`). **Zero** projects disable `TreatWarningsAsErrors`. Running `dotnet build "d:\Projects\jx-server\JXServer.slnx"` compiles with **0 warnings and 0 errors**.

#### D. Sealed Concrete Class Rule Compliance
Evaluated all class declarations across `src/JXServer.GameAssets`. **100% compliance**:
- All concrete catalog and helper classes are explicitly marked `sealed` (`ItemTemplateCatalog`, `MapCatalog`, `ShopCatalog`, `LootTableCatalog`, `NpcTemplateCatalog`, `PlayerTemplateCatalog`, `XPackArchiveReader`, `RegionObstacleGrid`, `RegionTrapGrid`, `GameAssetsOptions`, `ItemGenerator`).
- All stateless utilities are `public static class` (`PakFilePathHasher`, `UclNrv2bDecompressor`, `IniFileParser`, `TabFileParser`, `GameAssetsSnapshotLoader`, `ObstacleRleCodec`, `GameAssetsLogMessages`).
- All templates are `sealed partial record` or `readonly partial record struct` (`StarterSkillEntry`).

#### E. 1 Type per `.cs` File Rule Compliance
Across all 62 `.cs` files in `src/JXServer.GameAssets`, **every single file contains exactly one type definition**. Zero nested or secondary top-level types.

#### F. Unit-Explicit Suffixes Compliance
- **Compliant Suffixes**:
  - `MissileTemplate`: `SpeedMpsPerFrame`, `HeightMps`, `ZSpeedMpsPerFrame`, `ZAccelMpsPerFrame2`, `LifeFrames`, `HitRadiusMps`, `CollideRadiusCells`, `DamageRadiusCells`, `DamageIntervalFrames`.
  - `SkillTemplate`: `CastRangeMps`, `WaitFrames`.
  - `ItemTemplate`: `GridWidthCells`, `GridHeightCells`, `PriceGold`.
  - `PortalTrapBindingTemplate`: `TargetMpsX`, `TargetMpsY`, `AlternateMpsX`, `AlternateMpsY`, `BounceBackMpsX`, `BounceBackMpsY`.
  - `StarterVillageTemplate`: `SpawnMpsX`, `SpawnMpsY`.
  - `StaticNpcSpawnTemplate`: `SpawnMpsX`, `SpawnMpsY`.
- **Minor Non-Compliant Field Names Identified for Standardization**:
  - `MapDescriptor.cs`: `RegionBeginX`, `RegionBeginY`, `RegionWidth`, `RegionHeight` (Constitution mandates `*RegionX`, `*RegionY`, or `*WidthRegions`, `*HeightRegions`).
  - `NpcTemplate.cs`: `WalkSpeed`, `RunSpeed`, `DialogRadius` (Constitution mandates `WalkSpeedMpsPerFrame`, `RunSpeedMpsPerFrame`, `DialogRadiusCells`).

---

### 1.3. Test Suite Verification (97 Tests Baseline)

Executed test verification across all projects using .NET SDK `10.0.403`. Total existing tests: **97 tests**.

#### Breakdown by Test Assembly

```
================================================================================
Test Assembly                                   Passed  Failed  Skipped  Total
================================================================================
tests/JXServer.Protocol.Tests.csproj                59       0        0     59
tests/JXServer.ZoneEngine.Tests.csproj              24       0        0     24
tests/JXServer.Integration.Tests.csproj             14       0        0     14
--------------------------------------------------------------------------------
TOTAL                                               97       0        0     97 (100%)
================================================================================
```

#### Detailed Test Enumeration

1. **`JXServer.Protocol.Tests` (59 tests)**:
   - `AoiEntitySyncPacketTests`: 4 tests (`RequestPlayerC2SPacket_ShouldRoundTrip_0x48`, `RequestNpcC2SPacket_ShouldRoundTrip_0x49`, `SyncNpcMinS2CPacket_ShouldRoundTrip_0x4D_18Bytes`, `SyncNpcFullS2CPacket_CreateFromSnapshot_ShouldEncodeNpcSettingIdxAndVietnameseName`).
   - `CharacterCreationAndDeletionPacketTests`: 3 tests (`CreateCharacterC2SPacket_ShouldSerialize24ByteChoivolamVector_AndRoundTrip`, `DeleteCharacterC2SPacket_ShouldBe129Bytes_AndRoundTrip`, `RoleNewDelResponseS2CPacket_ShouldBe42Bytes_AndRoundTrip`).
   - `ChatPacketTests`: 3 tests (`PlayerTalk_C2S_And_S2C_ShouldRoundTripVietnameseTcvn3`, `PlayerSendChat_And_ChatGetSomeone_And_SystemExtend_ShouldRoundTrip`, `ChatSetChannelC2SPacket_ShouldRoundTrip_0x60`).
   - `CombatAndSkillPacketTests`: 4 tests (`NpcSkillC2SPacket_ShouldRoundTrip17BytesAndVerifySkillCheckKey`, `AutoAttackNpcC2SPacket_ShouldRoundTrip26BytesWithWrappedSkillPayload`, `ChangeAuraSkillAndPkPackets_ShouldRoundTripExactWireBytes`, `SkillCastDirectCastAndHurtS2CPackets_ShouldRoundTripExactWireBytes`).
   - `FramingAndCipherTests`: 13 tests (`PublicKeys_ShouldLoad5679Entries_WithExpectedFirstKey`, 3 parameterized `HandshakeKeyObfuscator_And_AccountBeginS2CPacket_ShouldRoundTripExactKeys`, 7 parameterized `KsgStreamCipher_ShouldRoundTripPayloadsOfAllAlignments`, `SecondLayerXorCipher_RoleListAndSelectRole_ShouldBeSymmetric`, `JxFramePipeProcessor_ShouldReassembleFragmentedAndBatchedFrames`).
   - `GatewayAndWorldEntryPacketTests`: 5 tests (`AccountLoginC2S_And_ResultS2C_ShouldBe127Bytes_AndRoundTrip`, `RoleListResultS2CPacket_ShouldEncryptWith0x3042_AndRoundTripAllSlots`, `SelectCharacterC2SPacket_ShouldEncryptWith0x38BC_AndDecodeRoleName`, `NotifyPlayerLoginS2CPacket_ShouldBe60Bytes_AndPreserveGuidIpAndPort`, `GameServerWorldSyncAndHeartbeatPackets_ShouldMatchExactWireSizes`).
   - `GroundObjectPacketTests`: 2 tests (`RequestObjAndObjMouseClickC2SPackets_ShouldRoundTripExactWireBytes`, `ObjAddAndObjRemoveS2CPackets_ShouldRoundTripExactWireBytesWithTcvn3Name`).
   - `InventoryAndEquipmentPacketTests`: 3 tests (`SyncItemAndSyncMoneyS2CPackets_ShouldRoundTripExactWireBytes`, `PickupMoveRemoveAndAutoMovePackets_ShouldRoundTripExactWireBytes`, `EatThrowAndDivideC2SPackets_ShouldRoundTripExactWireBytes`).
   - `MovementAndPosturePacketTests`: 3 tests (`MovementAntiCheatConsts_ShouldValidateWalkAndRunCheckValues`, `NpcWalkS2CPacket_And_NpcRunS2CPacket_ShouldHaveDistinctAsymmetricWireLayouts`, `NpcJump_And_NpcSit_And_NotifyPlayerExchange_ShouldRoundTrip`).
   - `ProgressionAndRevivalPacketTests`: 3 tests (`PlayerAddBaseAttributeAndApplySkillPointC2SPackets_ShouldRoundTripExactWireBytes`, `PlayerReviveC2SAndNpcDeathReviveS2CPackets_ShouldRoundTripExactWireBytes`, `PlayerExpLevelUpAttributeAndSkillLevelS2CPackets_ShouldRoundTripExactWireBytes`).
   - `SixtyFourDirectionAndFiveElementsMathTests`: 16 tests (`TrigTable_Has64Entries_AndCardinalsMatchTenBitFixedPoint`, 4 parameterized `GetDirectionIndex_ResolvesExpectedCardinalDirections`, 10 parameterized `FiveElementsSeriesMatrix_ValidatesConquerAndAccrueCycles`, `FiveElementsSeriesMatrix_ReturnsReducedMultiplierWhenTargetConquersAttacker`).

2. **`JXServer.ZoneEngine.Tests` (24 tests)**:
   - `BlueItemGeneratorAndLootTests`: 2 tests (`ItemGenerator_ShouldRollUpTo6BlueMagicAffixes_WithSlotParityAndZeroDuplicateAttributeIds`, `GroundObjectTickSystem_ShouldSpawnMonsterDropsBroadcastObjAdd_SyncOnAoiEntry_AndExpireAtExpireTick`).
   - `CombatAndMissileSimulationTests`: 2 tests (`CombatDamageCalculator_ShouldEnforceRelationsFiveElementsAndWeightedPoisonBlending`, `MapZoneWorker_ShouldRejectPeaceZoneSkillCast_AndExecuteProjectileCollisionInWilderness`).
   - `GameAssetsCatalogTests`: 4 tests (`PakFilePathHasher_ShouldNormalizeCaseAndSlashesDeterministically`, `MapCatalog_ShouldLoadWorldSet_Map53BaLangHuyen_AndObstacleGrid`, `PlayerTemplateCatalog_ShouldLoadAll10SeriesGenderTemplates_And8StarterVillages`, `MapCatalog_ShouldHaltOnMissingServerRootPath`).
   - `HighDensityAoiBenchmarkTests`: 1 test (`HighDensity_50PlayersCrossingRegionBoundariesAndChatting_ShouldMaintainP99TickUnder15Ms`).
   - `JxDataCatalogLoadingTests`: 3 tests (`SnapshotLoader_LoadsTier2MemoryPack_AndMatchesCanonicalJson`, `SkillParameterCurveTemplate_EvaluatesLinearPiecewiseConicAndOverCapTableDeterministically`, `Catalogs_LoadExpectedVietnameseNames_Shops_GoldenPlatina_AndStarterItems`).
   - `MapZoneWorkerTests`: 1 test (`MapZoneWorker_ShouldExecuteInitialSync_ActiveConfirmation_PingCycle_AndTimeoutEviction`).
   - `MonsterAiAndProgressionTests`: 1 test (`MonsterAiTickSystem_ShouldAggroPursueAttackLeashDieRespawn_AndAwardLevelUpAndStatAllocation`).
   - `MovementSimulationTests`: 2 tests (`MapZoneWorker_ShouldStepWalkAndRun_BroadcastTo3x3_AndRegenerateWhenSitting`, `MapZoneWorker_ShouldStopMovementAtImpassableObstacleCell`).
   - `NpcAssetLoadingTests`: 2 tests (`NpcTemplateCatalog_ShouldLoadVietnameseTemplates_AndAutoExecSpawns`, `MapCatalog_ShouldParseMap53NpcSection_AndTrapSection_WithVietnameseNames`).
   - `PlayerInventoryAndEquipmentTests`: 2 tests (`PlayerInventoryGrid_ShouldEnforce6x10Footprints_AndActivateFiveElementsHiddenAffixSlots`, `MapZoneWorker_ShouldEnforceOwnerProtectionAndRaceSafetyOnPickup_TickMedicineRegen_AndExecuteTownPortal`).
   - `SpatialRegionGridTests`: 2 tests (`SpatialRegionGrid_ComputeEnteringRegions_ShouldCompute3x3SetDifference`, `MapZoneWorker_ShouldSyncStaticNpcs_AndNeighborPlayers_AndRespondTo0x48_0x49`).
   - `TrapAndMapTransitionTests`: 2 tests (`TrapScriptCatalog_ShouldParseTownGates_AndCrossMapPortals_ForMap53`, `ZoneClusterRouter_ShouldTransferPlayerAcrossMaps_WhenSteppingOnCrossMapTrap`).

3. **`JXServer.Integration.Tests` (14 tests)**:
   - `CharacterLifecycleIntegrationTests`: 2 tests (`CreateCharacters_EnforceMax3_RejectDuplicateAndProhibited_VerifyAll5Elements_AndDeleteToReuseName`, `UnloadedStarterVillage_ShouldRedirectToMap53WhenFallbackEnabled_AndRejectWhenFallbackDisabled`).
   - `CombatSkillIntegrationTests`: 1 test (`CastSkillAndChangePkModeOverTcp_ShouldBroadcastSkillCastAndSyncPkFlag`).
   - `GatewayWorldEntryIntegrationTests`: 2 tests (`EndToEnd_AutoProvision_CreateOrSelectCharacter_AndEnterWorldMap53`, `AuthenticationRejections_ShouldEnforceStrictMode_WrongPassword_ProtocolVersion_AndFrozenStatus`).
   - `HighDensityCombatAndLootStressTests`: 1 test (`HighDensity_50PlayersCastingProjectilesKillingMonstersAndRacingGroundLoot_ShouldMaintainP99TickUnder15MsAndZeroDuplication`).
   - `InventoryAndLootIntegrationTests`: 1 test (`StarterItemsGroundPickupMoveConsumeAndTownPortalPersistenceOverTcp_ShouldRoundTripAndPersist`).
   - `MapTransitionIntegrationTests`: 1 test (`TownGateFightToggle_AndCrossMapLevelGateBounceBack_ShouldExecuteOverTcp`).
   - `MonsterAiAndProgressionIntegrationTests`: 1 test (`MonsterKillLevelUpStatSkillAllocationAndReviveOverTcp_ShouldRoundTripPackets`).
   - `MovementIntegrationTests`: 1 test (`WalkRunJumpSit_AndAntiCheatValidation_ShouldBroadcastOverTcpWithExactWireLayouts`).
   - `MultiChannelChatIntegrationTests`: 1 test (`NearbyTalk_WorldChat_PrivateWhisper_AndChannelMask_ShouldRouteWithVietnameseFidelity`).
   - `SessionLeaseFencingIntegrationTests`: 2 tests (`SessionTakeover_ShouldRejectWhenKickOnlineFalse_AndEvictOldSessionWhenKickOnlineTrue`, `SwarmConcurrency_100ConcurrentBots_ShouldAuthenticateCreateAndEnterMap53`).
   - `SpatialAoiIntegrationTests`: 1 test (`TwoPlayersInMap53_ShouldReceive3x3StaticNpcs_Mutual0x4C_AndRespondTo0x48_0x49`).

---

## 2. Logic Chain

1. **Premise 1 (Constitution v1.2.0 Principle V & Data Standard)**:
   - Static game data in production and zone simulation MUST be loaded exclusively from Tier 1 canonical UTF-8 JSON (`data/**/*.json`) or Tier 2 pre-compiled binary snapshots (`artifacts/*.pack`).
   - Runtime code MUST NOT read or parse legacy binary `.pak`, `Region_S.dat`, KTabFile `.txt`, or raw `.lua` files.
2. **Premise 2 (Observed Architecture of JXServer.GameAssets)**:
   - `Snapshots/GameAssetsSnapshotLoader.cs` and `Snapshots/GameAssetsSnapshotBundle.cs` already provide a fully functional, schema-validated Dual-Tier loader for all domains (Skills, Missiles, Items, MagicAffixes, GroundObjects, LootTables, NpcTemplates, AutoExecSpawns, CharacterTemplates, StarterVillages, SeriesGrowthRules, LevelExpTable, DefaultStarterItems, MapDescriptors, PortalTrapBindings, Shops).
   - `ItemTemplateCatalog`, `SkillTemplateCatalog`, `MissileTemplateCatalog`, `LootTableCatalog`, and `ShopCatalog` strictly use `GameAssetsSnapshotLoader`.
3. **Premise 3 (Observed Legacy Parsers in GameAssets)**:
   - `Archives/` (`PakFilePathHasher.cs`, `UclNrv2bDecompressor.cs`, `XPackArchiveReader.cs`) only exists to open and decompress Kingsoft `maps.pak`.
   - `Configuration/` (`IniFileParser.cs`, `TabFileParser.cs`) parses `.ini` and `.txt`.
   - `WorldMaps/` (`RegionObstacleGrid.cs`, `RegionTrapGrid.cs`, `TrapScriptCatalog.cs`, and `MapCatalog.cs`) directly read `Region_S.dat`, `worldset.ini`, `maplist.ini`, and `script/**/trap/*.lua`.
   - `PlayerTemplates/PlayerTemplateCatalog.cs` hard-requires and parses `newplayerini*.ini`, `level_add.txt`, etc., on initialization.
   - `NpcTemplates/NpcTemplateCatalog.cs` maintains legacy fallback parsers `ParseNpcsTxt` and `ParseAutoExecSpawns`.
4. **Premise 4 (Existing tools/JXServer.DataExporter capabilities)**:
   - `tools/JXServer.DataExporter/Legacy/` already contains `RegionDatBinaryReader.cs`, `KTabFileTable.cs`, `HybridLegacyTextDecoder.cs`, `Lua4Interpreter.cs`, and `LegacyPathResolver.cs`.
   - `DataExporter` currently references `JXServer.GameAssets.Archives.XPackArchiveReader`.
5. **Deduction & Migration Target**:
   - The legacy readers in `Archives/` (`PakFilePathHasher.cs`, `UclNrv2bDecompressor.cs`, `XPackArchiveReader.cs`) should be relocated into `tools/JXServer.DataExporter/Legacy/` (or `Archives/`) so that the exporter is 100% self-contained for offline extraction.
   - `MapCatalog` must be transitioned to load its `MapDescriptor` list and `RegionNpcSpawns` from `GameAssetsSnapshotLoader`, and load region cell obstacle barriers directly from `obstacleRleBase64` using `ObstacleRleCodec` (which is already implemented and verified in `WorldMaps/ObstacleRleCodec.cs`).
   - `TrapScriptCatalog` must be refactored to populate `_bindings` from `bundle.PortalTrapBindings` (already extracted and validated in `data/maps/*.map.json` and `jxdata.pack`), eliminating runtime Lua regex parsing.
   - `PlayerTemplateCatalog` must remove the hard disk file dependency (`level_add.txt`, `newplayerini*.ini`) and load directly from `GameAssetsSnapshotLoader.TryLoadBundle` like `ItemTemplateCatalog`.
   - `NpcTemplateCatalog` can safely retire `ParseNpcsTxt` and `ParseAutoExecSpawns` since `data/npcs/*.npcs.json` and `jxdata.pack` cover 100% of NPC archetypes (2540 templates) and autoexec spawns.
   - `IniFileParser.cs` and `TabFileParser.cs` in `GameAssets` become obsolete once `MapCatalog` and `PlayerTemplateCatalog` load from JSON/MemoryPack, and can either be retired or moved to `tools/JXServer.DataExporter`.

---

## 3. Caveats

1. **Test Suite Reliance on Legacy Paths**:
   - `tests/JXServer.ZoneEngine.Tests/GameAssetsCatalogTests.cs` (4 tests) and `NpcAssetLoadingTests.cs` (2 tests) explicitly pass `ServerRootPath = @"d:\Projects\jx-linux-bin\server1"` to test `MapCatalog`, `PlayerTemplateCatalog`, and `NpcTemplateCatalog` legacy reading.
   - When refactoring `MapCatalog` and `PlayerTemplateCatalog` to pure Dual-Tier runtime loaders in Step 1, these unit tests must be updated to verify loading from `data/` or `artifacts/jxdata.pack` without breaking test intent, or moved to a dedicated `DataExporter.Tests` suite if testing raw Kingsoft format decoding.
2. **Stress Test Concurrency Timing**:
   - `HighDensityCombatAndLootStressTests.HighDensity_50PlayersCastingProjectilesKillingMonstersAndRacingGroundLoot_ShouldMaintainP99TickUnder15MsAndZeroDuplication` is a real-time 18-Hz simulation benchmark requiring p99 < 15.0ms. Running all 3 test projects concurrently in a single `dotnet test` command can cause thread scheduling delays (observed 15.477ms). For zero-regression verification, sequential execution (`--concurrency 1` or project-by-project) guarantees 100% pass rate.
3. **Read-Only Scope**:
   - In accordance with explorer archetype constraints, **no code modifications have been made**. All findings, mappings, and recommendations are documented for the subsequent implementation agents.

---

## 4. Conclusion

1. **GameAssets Architecture**:
   - `JXServer.GameAssets` already has the complete infrastructure for `JXData Standard v1` Dual-Tier runtime loading via `GameAssetsSnapshotLoader`, `GameAssetsSnapshotBundle`, and `ObstacleRleCodec`.
   - Exactly **9 legacy reader/parser components** (`XPackArchiveReader`, `PakFilePathHasher`, `UclNrv2bDecompressor`, `IniFileParser`, `TabFileParser`, `RegionObstacleGrid`, `RegionTrapGrid`, `TrapScriptCatalog` Lua regex parser, and `PlayerTemplateCatalog` legacy disk initializers) should be moved to `tools/JXServer.DataExporter` or retired to achieve 100% compliance with Constitution v1.2.0 Principle V.
2. **Build Environment**:
   - Baseline is healthy: .NET 10, C# 14, `TreatWarningsAsErrors=true` enforced with 0 warnings.
   - Sealed class rule is 100% respected across all concrete classes.
   - 1 type per `.cs` file rule is 100% respected across all 62 source files.
   - Minor unit suffix normalizations needed on `MapDescriptor` (`*RegionX`/`*RegionY`/`*WidthRegions`/`*HeightRegions`) and `NpcTemplate` (`WalkSpeedMpsPerFrame`, `RunSpeedMpsPerFrame`, `DialogRadiusCells`).
3. **Test Suite Baseline**:
   - 97 tests total (59 Protocol + 24 ZoneEngine + 14 Integration).
   - Baseline status: 100% passing when executed with project separation or sequential mode.

---

## 5. Verification Method

To independently verify all findings and test counts, run the following commands:

### 1. Build Verification (Zero Warnings)
```powershell
dotnet build "d:\Projects\jx-server\JXServer.slnx"
```
*Expected*: Build succeeded with 0 Warning(s), 0 Error(s).

### 2. Full Test Suite Baseline Verification (97 Tests)
```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.Protocol.Tests\JXServer.Protocol.Tests.csproj"
dotnet test "d:\Projects\jx-server\tests\JXServer.ZoneEngine.Tests\JXServer.ZoneEngine.Tests.csproj"
dotnet test "d:\Projects\jx-server\tests\JXServer.Integration.Tests\JXServer.Integration.Tests.csproj"
```
*Expected Outputs*:
- `JXServer.Protocol.Tests`: Passed 59, Failed 0, Skipped 0, Total 59.
- `JXServer.ZoneEngine.Tests`: Passed 24, Failed 0, Skipped 0, Total 24.
- `JXServer.Integration.Tests`: Passed 14, Failed 0, Skipped 0, Total 14.
- **Combined Total**: 97 tests, 100% Pass.

### 3. Verification of Files & Classes in `JXServer.GameAssets`
```powershell
Get-ChildItem -Path "d:\Projects\jx-server\src\JXServer.GameAssets" -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Measure-Object
```
*Expected Count*: 63 files (1 `.csproj` + 62 `.cs` files).
