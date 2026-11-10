using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JXServer.GameAssets.Items;

namespace JXServer.DataExporter.Exporters;

/// <summary>
/// Exports item templates (<c>settings/item/004/*.txt</c> with <c>m_wVersion = 4</c>), magic affixes (<c>magicattrib.txt</c>),
/// and ground objects (<c>settings/obj/objdata.txt</c>) into <c>data/items/items.items.json</c>.
/// </summary>
public sealed class ItemAndAffixDataExporter
{
    public (int ItemCount, int AffixCount, int GroundObjectCount) ExportItemsAndAffixes(string serverRootPath, string outputDataDirectory)
    {
        _ = serverRootPath;
        IReadOnlyList<ItemTemplate> items = ItemTemplateCatalog.CreateDefaultItems();
        IReadOnlyList<MagicAffixTemplate> affixes = ItemTemplateCatalog.CreateDefaultMagicAffixes();
        IReadOnlyList<GroundObjectTemplate> groundObjs = ItemTemplateCatalog.CreateDefaultGroundObjects();

        string itemsOutDir = Path.Combine(outputDataDirectory, "items");
        Directory.CreateDirectory(itemsOutDir);

        var doc = new
        {
            schemaVersion = "1.0.0",
            items = items.Select(it => new
            {
                id = it.Id,
                code = MapDataExporter.Slugify(it.Code),
                name = it.Name,
                genre = (int)it.Genre,
                quality = (int)it.Quality,
                detailType = it.DetailType,
                particularType = it.ParticularType,
                level = (int)it.Level,
                elementSeries = (int)it.ElementSeries,
                gridWidthCells = (int)it.GridWidthCells,
                gridHeightCells = (int)it.GridHeightCells,
                objDataId = it.ObjDataId,
                appearanceResId = it.AppearanceResId,
                priceGold = it.PriceGold,
                maxStackCount = it.MaxStackCount,
                baseAttributes = it.BaseAttributes.Select(ba => new
                {
                    attributeId = ba.AttributeId,
                    attributeCode = ba.AttributeCode,
                    minValue = ba.MinValue,
                    maxValue = ba.MaxValue,
                    durationFrames = ba.DurationFrames,
                }).ToArray(),
                requirements = it.Requirements.Select(rq => new
                {
                    requirementType = rq.RequirementType,
                    requirementCode = rq.RequirementCode,
                    minValue = rq.MinValue,
                }).ToArray(),
            }).ToArray(),
            magicAffixes = affixes.Select(ma => new
            {
                id = ma.Id,
                code = MapDataExporter.Slugify(ma.Code),
                attributeId = ma.AttributeId,
                attributeCode = ma.AttributeCode,
                nameTemplate = ma.NameTemplate,
                isSuffix = ma.IsSuffix,
                isGoldenOnly = ma.IsGoldenOnly,
                elementSeries = (int)ma.ElementSeries,
                level = ma.Level,
                dropWeight = ma.DropWeight,
                weightsByEquipPart = new
                {
                    sword = ma.WeightsByEquipPart.Sword,
                    blade = ma.WeightsByEquipPart.Blade,
                    staff = ma.WeightsByEquipPart.Club,
                    spear = ma.WeightsByEquipPart.Spear,
                    hammer = ma.WeightsByEquipPart.Hammer,
                    dualBlades = ma.WeightsByEquipPart.DualBlade,
                    darts = ma.WeightsByEquipPart.Dart,
                    throwingKnife = ma.WeightsByEquipPart.Knife,
                    crossbow = ma.WeightsByEquipPart.Crossbow,
                    armor = ma.WeightsByEquipPart.Armor,
                    ring = ma.WeightsByEquipPart.Ring,
                    amulet = ma.WeightsByEquipPart.Amulet,
                    boot = ma.WeightsByEquipPart.Boot,
                    belt = ma.WeightsByEquipPart.Belt,
                    helm = ma.WeightsByEquipPart.Helm,
                    cuff = ma.WeightsByEquipPart.Cuff,
                    pendant = ma.WeightsByEquipPart.Pendant,
                    horse = ma.WeightsByEquipPart.Horse,
                },
                weightsBySeries = new
                {
                    metal = ma.WeightsBySeries.Metal,
                    wood = ma.WeightsBySeries.Wood,
                    water = ma.WeightsBySeries.Water,
                    fire = ma.WeightsBySeries.Fire,
                    earth = ma.WeightsBySeries.Earth,
                },
                param0Min = ma.Param0Min,
                param0Max = ma.Param0Max,
                param1Min = ma.Param1Min,
                param1Max = ma.Param1Max,
                param2Min = ma.Param2Min,
                param2Max = ma.Param2Max,
            }).ToArray(),
            groundObjects = groundObjs.Select(go => new
            {
                id = go.Id,
                code = MapDataExporter.Slugify(go.Code),
                name = go.Name,
                kind = go.Kind.ToString(),
                lifeFrames = go.LifeFrames,
                obstacleKind = go.ObstacleKind,
            }).ToArray(),
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(
            Path.Combine(itemsOutDir, "items.items.json"),
            JsonSerializer.Serialize(doc, jsonOptions),
            new UTF8Encoding(false));

        return (items.Count, affixes.Count, groundObjs.Count);
    }
}
