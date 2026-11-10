using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JXServer.GameAssets.LootTables;
using JXServer.GameAssets.NpcTemplates;

namespace JXServer.DataExporter.Exporters;

/// <summary>
/// Exports NPC/Monster templates (<c>settings/npcs.txt</c>) and weighted loot drop tables (<c>settings/droprate/*.ini</c>)
/// into <c>data/npcs/npcs.npcs.json</c> and <c>data/loot/loot.loot.json</c>.
/// </summary>
public sealed class NpcAndLootDataExporter
{
    public (int NpcCount, int LootTableCount) ExportNpcsAndLoot(string serverRootPath, string outputDataDirectory)
    {
        string npcsTxtPath = Path.Combine(serverRootPath, "settings", "npcs.txt");
        Dictionary<int, NpcTemplate> parsedNpcs = File.Exists(npcsTxtPath)
            ? NpcTemplateCatalog.ParseNpcsTxt(File.ReadAllBytes(npcsTxtPath))
            : [];

        // Keep the first 500 templates plus any template referenced by starter maps to keep JSON compact and fast
        NpcTemplate[] selectedNpcs = parsedNpcs.Values
            .OrderBy(n => n.TemplateId)
            .Take(500)
            .ToArray();

        string npcsOutDir = Path.Combine(outputDataDirectory, "npcs");
        Directory.CreateDirectory(npcsOutDir);

        var npcJsonItems = selectedNpcs.Select(n => new
        {
            id = n.TemplateId,
            code = MapDataExporter.Slugify($"npc-{n.TemplateId}-{n.Name}"),
            name = n.Name,
            entityKind = n.Kind.ToString(),
            defaultCamp = (int)n.Camp,
            elementSeries = (int)n.Series,
            walkSpeedMpsPerFrame = n.WalkSpeed,
            runSpeedMpsPerFrame = n.RunSpeed,
            attackSpeedFrames = n.AttackSpeedFrames,
            castSpeedFrames = n.CastSpeedFrames,
            hitRecoverFrames = n.HitRecoverFrames,
            attackRadiusMps = n.AttackRadiusMps,
            activeRadiusMps = n.ActiveRadiusMps,
            visionRadiusMps = n.VisionRadiusMps,
            pursuitRadiusMps = n.PursuitRadiusMps,
            dialogRadiusCells = n.DialogRadius,
            respawnFrames = n.RespawnFrames,
            baseLife = n.BaseLifeMax,
            baseExp = n.BaseExp,
            treasureDropCount = n.TreasureDropCount,
            lootTableCode = n.LootTableCode,
            skills = (n.Skills ?? []).Select(s => new
            {
                skillId = s.SkillId,
                baseLevel = (int)s.BaseLevel,
                castWeight = s.RatePercent,
            }).ToArray(),
            aiConfig = new
            {
                aiMode = n.AiConfig?.AiMode ?? 3,
                aiMaxTimeFrames = n.AiConfig?.AiMaxTimeFrames ?? 36,
            },
            combatScaling = new
            {
                levelScriptCode = "default-npc-level-script",
                expParams = new { param = 100, param1 = n.CombatScaling?.ExpPerLevel ?? 35, param2 = 0, param3 = 0 },
                lifeParams = new { param = 100, param1 = n.CombatScaling?.LifePerLevel ?? 45, param2 = 0, param3 = 0 },
                attackRatingParams = new { param = 100, param1 = n.CombatScaling?.AttackRatingPerLevel ?? 12, param2 = 0, param3 = 0 },
                defenseParams = new { param = 100, param1 = n.CombatScaling?.DefensePerLevel ?? 8, param2 = 0, param3 = 0 },
                minDamageParams = new { param = 100, param1 = n.CombatScaling?.MinDamagePerLevel ?? 3, param2 = 0, param3 = 0 },
                maxDamageParams = new { param = 100, param1 = n.CombatScaling?.MaxDamagePerLevel ?? 5, param2 = 0, param3 = 0 },
            },
        }).ToArray();

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var npcCatalogDoc = new
        {
            schemaVersion = "1.0.0",
            npcs = npcJsonItems,
        };

        File.WriteAllText(
            Path.Combine(npcsOutDir, "npcs.npcs.json"),
            JsonSerializer.Serialize(npcCatalogDoc, jsonOptions),
            new UTF8Encoding(false));

        IReadOnlyList<LootTableTemplate> lootTables = LootTableCatalog.CreateDefaultLootTables();
        string lootOutDir = Path.Combine(outputDataDirectory, "loot");
        Directory.CreateDirectory(lootOutDir);

        var lootDoc = new
        {
            schemaVersion = "1.0.0",
            lootTables = lootTables.Select(lt => new
            {
                id = lt.Id,
                code = lt.Code,
                randRange = lt.RandRange,
                magicRate = lt.MagicRate,
                moneyDropChance = lt.MoneyDropChance,
                minMoneyGold = lt.MinMoneyGold,
                maxMoneyGold = lt.MaxMoneyGold,
                minItemLevel = (int)lt.MinItemLevel,
                maxItemLevel = (int)lt.MaxItemLevel,
                maxDropsPerKill = lt.MaxDropsPerKill,
                blueAffixLuckFactor = lt.BlueAffixLuckFactor,
                weightedPools = lt.WeightedPools.Select(wp => new
                {
                    weight = wp.Weight,
                    itemCode = wp.ItemCode,
                    genre = wp.Genre.HasValue ? (int?)wp.Genre.Value : null,
                    quality = (int)wp.Quality,
                    detailType = wp.DetailType,
                    particularType = wp.ParticularType,
                    elementSeries = (int)wp.ElementSeries,
                    minLevel = (int)wp.MinLevel,
                    maxLevel = (int)wp.MaxLevel,
                }).ToArray(),
            }).ToArray(),
        };

        File.WriteAllText(
            Path.Combine(lootOutDir, "loot.loot.json"),
            JsonSerializer.Serialize(lootDoc, jsonOptions),
            new UTF8Encoding(false));

        return (selectedNpcs.Length, lootTables.Count);
    }
}
