using System.Collections.Generic;
using JXServer.GameAssets.Items;
using JXServer.GameAssets.LootTables;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.Shops;
using JXServer.GameAssets.Skills;
using JXServer.GameAssets.WorldMaps;
using MemoryPack;

namespace JXServer.GameAssets.Snapshots;

/// <summary>
/// Pre-compiled binary snapshot bundle (<c>artifacts/jxdata.pack</c>) containing all <c>JXData Standard v1</c>
/// static game asset templates and SHA-256 source digest for zero-overhead runtime startup (`FR-001`).
/// </summary>
[MemoryPackable]
public sealed partial record GameAssetsSnapshotBundle(
    string SchemaVersion,
    string SourceJsonSha256Hex,
    IReadOnlyList<SkillTemplate> Skills,
    IReadOnlyList<MissileTemplate> Missiles,
    IReadOnlyList<ItemTemplate> Items,
    IReadOnlyList<MagicAffixTemplate> MagicAffixes,
    IReadOnlyList<GroundObjectTemplate> GroundObjects,
    IReadOnlyList<LootTableTemplate> LootTables,
    IReadOnlyList<NpcTemplate> NpcTemplates,
    IReadOnlyList<StaticNpcSpawnTemplate> AutoExecSpawns,
    IReadOnlyList<CharacterBaseTemplate> CharacterTemplates,
    IReadOnlyList<StarterVillageTemplate> StarterVillages,
    IReadOnlyList<PlayerSeriesGrowthRuleTemplate> SeriesGrowthRules,
    IReadOnlyList<long> LevelExpTable,
    IReadOnlyList<StarterItemTemplate> DefaultStarterItems,
    IReadOnlyList<MapDescriptor> MapDescriptors,
    IReadOnlyList<PortalTrapBindingTemplate> PortalTrapBindings,
    IReadOnlyList<ShopTemplate>? Shops = null);
