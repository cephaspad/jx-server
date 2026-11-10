using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;

namespace JXServer.DataExporter.Exporters;

/// <summary>
/// Exports starter player templates (<c>newplayerini00..09.ini</c>), series growth rules (<c>level_add.txt</c>),
/// level experience thresholds (<c>level_exp.txt</c>), starter villages (<c>nativeplacelist.ini</c>),
/// and starter items (`FR-010`) into <c>data/players/players.players.json</c>.
/// </summary>
public sealed class PlayerDataExporter
{
    public int ExportPlayers(string serverRootPath, string outputDataDirectory)
    {
        string playerDir = Path.Combine(serverRootPath, "settings", "npc", "player");
        IReadOnlyList<StarterItemTemplate> starterItems = PlayerTemplateCatalog.CreateDefaultStarterItems();

        var charTemplates = new List<CharacterBaseTemplate>();
        for (int seriesIdx = 0; seriesIdx < 5; seriesIdx++)
        {
            for (int genderIdx = 0; genderIdx < 2; genderIdx++)
            {
                int fileIdx = (seriesIdx * 2) + genderIdx;
                string iniPath = Path.Combine(playerDir, $"newplayerini{fileIdx:D2}.ini");
                ElementSeries series = (ElementSeries)seriesIdx;
                CharacterGender gender = (CharacterGender)genderIdx;
                if (File.Exists(iniPath))
                {
                    charTemplates.Add(PlayerTemplateCatalog.ParseNewPlayerIni(File.ReadAllBytes(iniPath), series, gender, starterItems));
                }
                else
                {
                    charTemplates.Add(new CharacterBaseTemplate(
                        series,
                        gender,
                        25,
                        25,
                        25,
                        25,
                        0,
                        100,
                        100,
                        100,
                        [new StarterSkillEntry(53, 1)],
                        starterItems));
                }
            }
        }

        string levelAddPath = Path.Combine(playerDir, "level_add.txt");
        IReadOnlyList<PlayerSeriesGrowthRuleTemplate> growthRules = File.Exists(levelAddPath)
            ? PlayerTemplateCatalog.ParseLevelAddTxt(File.ReadAllBytes(levelAddPath))
            : [
                new PlayerSeriesGrowthRuleTemplate(ElementSeries.Metal, 10, 5, 4, 5, 2, 3),
                new PlayerSeriesGrowthRuleTemplate(ElementSeries.Wood, 10, 5, 4, 5, 2, 3),
                new PlayerSeriesGrowthRuleTemplate(ElementSeries.Water, 10, 5, 4, 5, 2, 3),
                new PlayerSeriesGrowthRuleTemplate(ElementSeries.Fire, 10, 5, 4, 5, 2, 3),
                new PlayerSeriesGrowthRuleTemplate(ElementSeries.Earth, 10, 5, 4, 5, 2, 3),
            ];

        string levelExpPath = Path.Combine(playerDir, "level_exp.txt");
        long[] levelExp = File.Exists(levelExpPath)
            ? PlayerTemplateCatalog.ParseLevelExpTxt(File.ReadAllBytes(levelExpPath))
            : Enumerable.Range(1, 100).Select(lvl => 100L * lvl * lvl).ToArray();

        string nativePlacePath = Path.Combine(serverRootPath, "settings", "nativeplacelist.ini");
        string revivalIdPath = Path.Combine(playerDir, "revivalid.ini");
        string revivePosPath = Path.Combine(serverRootPath, "settings", "revivepos.ini");

        Dictionary<ushort, StarterVillageTemplate> villages =
            File.Exists(nativePlacePath) && File.Exists(revivalIdPath) && File.Exists(revivePosPath)
                ? PlayerTemplateCatalog.LoadStarterVillages(
                    File.ReadAllBytes(nativePlacePath),
                    File.ReadAllBytes(revivalIdPath),
                    File.ReadAllBytes(revivePosPath))
                : new Dictionary<ushort, StarterVillageTemplate>
                {
                    [53] = new StarterVillageTemplate(53, "Ba Lăng huyện", 19, 52032, 101696),
                };

        string playersOutDir = Path.Combine(outputDataDirectory, "players");
        Directory.CreateDirectory(playersOutDir);

        var doc = new
        {
            schemaVersion = "1.0.0",
            characterTemplates = charTemplates.Select((ct, idx) => new
            {
                id = idx,
                code = $"starter-{ct.Series.ToString().ToLowerInvariant()}-{ct.Gender.ToString().ToLowerInvariant()}",
                elementSeries = (int)ct.Series,
                gender = (int)ct.Gender,
                baseStrength = (int)ct.BaseStrength,
                baseDexterity = (int)ct.BaseDexterity,
                baseVitality = (int)ct.BaseVitality,
                baseEnergy = (int)ct.BaseEnergy,
                baseLucky = (int)ct.BaseLucky,
                initialLifeMax = (int)ct.InitialLifeMax,
                initialManaMax = (int)ct.InitialManaMax,
                initialStaminaMax = (int)ct.InitialStaminaMax,
                starterSkills = ct.StarterSkills.Select(s => new
                {
                    skillId = (int)s.SkillId,
                    level = (int)s.Level,
                }).ToArray(),
                starterItems = (ct.StarterItems ?? starterItems).Select(si => new
                {
                    itemCode = si.ItemCode,
                    container = si.ContainerPlace switch
                    {
                        ItemContainerPlace.Equip => "equip",
                        ItemContainerPlace.Immediacy => "immediacy",
                        _ => "equipRoom",
                    },
                    gridX = (int)si.GridX,
                    gridY = (int)si.GridY,
                    stackCount = (int)si.StackCount,
                }).ToArray(),
            }).ToArray(),
            seriesGrowthRules = growthRules.Select(gr => new
            {
                elementSeries = (int)gr.Series,
                code = $"growth-{gr.Series.ToString().ToLowerInvariant()}",
                lifePerLevel = gr.LifePerLevel,
                manaPerLevel = gr.ManaPerLevel,
                staminaMalePerLevel = gr.StaminaPerLevel,
                staminaFemalePerLevel = gr.StaminaPerLevel,
                lifePerVitality = gr.LifePerVitality,
                manaPerEnergy = gr.ManaPerEnergy,
                staminaPerVitality = gr.StaminaPerVitality,
                attributePointsPerLevel = gr.AttributePointsPerLevel,
                skillPointsPerLevel = gr.SkillPointsPerLevel,
            }).ToArray(),
            fiveElementsMatrix = new
            {
                generates = new[] { 2, 3, 1, 4, 0 },
                conquers = new[] { 1, 4, 3, 0, 2 },
                conqueredDamageBonusPercent = 20,
                conqueredDamagePenaltyPercent = -20,
            },
            levelExpTable = levelExp.Select((exp, idx) => new
            {
                level = idx + 1,
                expRequired = exp,
            }).ToArray(),
            starterVillages = villages.Values.OrderBy(v => v.MapId).Select((v, idx) => new
            {
                id = idx + 1,
                code = MapDataExporter.Slugify($"village-{v.MapId}-{v.VillageNameUnicode}"),
                name = v.VillageNameUnicode,
                mapId = (int)v.MapId,
                defaultRevivalPointId = v.RevivalId,
                revivalPoints = new[]
                {
                    new
                    {
                        revivalPointId = v.RevivalId,
                        mpsX = v.SpawnMpsX,
                        mpsY = v.SpawnMpsY,
                    },
                },
            }).ToArray(),
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(
            Path.Combine(playersOutDir, "players.players.json"),
            JsonSerializer.Serialize(doc, jsonOptions),
            new UTF8Encoding(false));

        return charTemplates.Count;
    }
}
