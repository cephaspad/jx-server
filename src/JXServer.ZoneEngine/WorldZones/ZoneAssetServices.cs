using JXServer.Contracts.Characters;
using JXServer.GameAssets.Items;
using JXServer.GameAssets.LootTables;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.Skills;
using JXServer.GameAssets.WorldMaps;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Aggregates optional read-only catalogs and application services passed into zone shards.
/// </summary>
public sealed class ZoneAssetServices
{
    public static ZoneAssetServices Empty { get; } = new();

    public IMapCatalog? MapCatalog { get; init; }
    public INpcTemplateCatalog? NpcTemplateCatalog { get; init; }
    public ITrapScriptCatalog? TrapScriptCatalog { get; init; }
    public ICharacterAppService? CharacterAppService { get; init; }
    public IZoneClusterRouter? ClusterRouter { get; init; }
    public ISkillTemplateCatalog? SkillTemplateCatalog { get; init; }
    public IMissileTemplateCatalog? MissileTemplateCatalog { get; init; }
    public IPlayerTemplateCatalog? PlayerTemplateCatalog { get; init; }
    public IItemTemplateCatalog? ItemTemplateCatalog { get; init; }
    public ILootTableCatalog? LootTableCatalog { get; init; }
    public ItemGenerator? ItemGenerator { get; init; }
}
