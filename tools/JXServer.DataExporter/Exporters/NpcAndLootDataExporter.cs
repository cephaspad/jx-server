using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JXServer.DataExporter.Legacy;

namespace JXServer.DataExporter.Exporters;

/// <summary>
/// Exports all monster/boss drop tables (<c>settings/droprate/**/*.ini</c> + <c>settings/item/*droprate*.ini</c>),
/// <c>settings/npcs.txt</c> (<c>2,540</c> physical templates <c>0..2539</c> with Row-2 inheritance),
/// <c>settings/npc/npcgoldtemplate.txt</c>, <c>settings/partner/*</c>, and <c>script/global/autoexec.lua</c>
/// global vs feature/event spawns (`JXData Standard v1`).
/// </summary>
public sealed class NpcAndLootDataExporter
{
    private static readonly string[] FeatureCfgFlags =
    [
        "CFG_ThienTu",
        "CFG_ThatThanhDaiChien",
        "CFG_NhiemVuLongNgu",
        "CFG_ViemDe",
        "CFG_BossSatThu",
        "CFG_LienDau",
        "CFG_mid_autumn05",
        "CFG_zhongqiu2007",
        "CFG_newyear_2008",
        "CFG_jiefang_jieri2008",
        "CFG_jiefang_jieri2009",
    ];

    public (int NpcCount, int LootTableCount, int GlobalSpawnCount, int EventSpawnCount) ExportNpcsAndLoot(
        string serverRootPath,
        string outputDataDirectory,
        ExportCatalogRegistry? registry = null)
    {
        registry ??= new ExportCatalogRegistry();

        string lootOutDir = Path.Combine(outputDataDirectory, "loot");
        string npcsOutDir = Path.Combine(outputDataDirectory, "npcs");
        Directory.CreateDirectory(lootOutDir);
        Directory.CreateDirectory(npcsOutDir);

        DeleteIfExists(Path.Combine(lootOutDir, "loot.loot.json"));
        DeleteIfExists(Path.Combine(npcsOutDir, "npcs.npcs.json"));

        int lootCount = ExportLootTables(serverRootPath, lootOutDir, registry);
        int npcCount = ExportCoreNpcsGoldenAndPartner(serverRootPath, npcsOutDir, registry);
        (int globalSpawns, int eventSpawns) = ExportAutoExecWorldSpawns(serverRootPath, npcsOutDir, registry);

        return (npcCount, lootCount, globalSpawns, eventSpawns);
    }

    private static int ExportLootTables(string serverRootPath, string lootOutDir, ExportCatalogRegistry registry)
    {
        string settingsDir = Path.Combine(serverRootPath, "settings");
        var iniPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string dropRateDir = Path.Combine(settingsDir, "droprate");
        if (Directory.Exists(dropRateDir))
        {
            foreach (string f in Directory.EnumerateFiles(dropRateDir, "*.ini", SearchOption.AllDirectories))
            {
                iniPaths.Add(Path.GetFullPath(f));
            }
        }

        string itemDir = Path.Combine(settingsDir, "item");
        if (Directory.Exists(itemDir))
        {
            foreach (string f in Directory.EnumerateFiles(itemDir, "*droprate*.ini", SearchOption.TopDirectoryOnly))
            {
                iniPaths.Add(Path.GetFullPath(f));
            }

            string doubleDir = Path.Combine(itemDir, "double");
            if (Directory.Exists(doubleDir))
            {
                foreach (string f in Directory.EnumerateFiles(doubleDir, "*.ini", SearchOption.AllDirectories))
                {
                    iniPaths.Add(Path.GetFullPath(f));
                }
            }
        }

        string[] sortedFiles = iniPaths
            .OrderBy(p => Path.GetRelativePath(serverRootPath, p).Replace('/', '\\'), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rawEntries = new List<(int Index, string BaseSlug, string LegacyRelPath, Dictionary<string, object?> Props)>(sortedFiles.Length);

        for (int i = 0; i < sortedFiles.Length; i++)
        {
            string fullPath = sortedFiles[i];
            string relFromRoot = "\\" + Path.GetRelativePath(serverRootPath, fullPath).Replace('/', '\\');
            string normRel = ExportCatalogRegistry.NormalizeLegacyRelPath(relFromRoot);

            string baseSlug;
            if (string.Equals(normRel, @"\settings\item\npcdroprate.ini", StringComparison.OrdinalIgnoreCase))
            {
                baseSlug = "default-monster-loot";
            }
            else if (normRel.StartsWith(@"\settings\droprate\", StringComparison.OrdinalIgnoreCase))
            {
                string sub = normRel[@"\settings\droprate\".Length..];
                if (sub.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
                {
                    sub = sub[..^4];
                }

                baseSlug = HybridLegacyTextDecoder.Slugify(sub.Replace('\\', '-'), "loot-table");
            }
            else
            {
                string sub = normRel.StartsWith(@"\settings\", StringComparison.OrdinalIgnoreCase)
                    ? normRel[@"\settings\".Length..]
                    : normRel.TrimStart('\\');
                if (sub.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
                {
                    sub = sub[..^4];
                }

                baseSlug = HybridLegacyTextDecoder.Slugify(sub.Replace('\\', '-'), "loot-table");
            }

            Dictionary<string, object?> props = ParseDropRateIniFile(fullPath, relFromRoot, registry);
            rawEntries.Add((i + 1, baseSlug, normRel, props));
        }

        string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(rawEntries, e => e.BaseSlug, e => e.Index);
        var lootDocs = new List<Dictionary<string, object?>>(rawEntries.Count + 1);
        for (int i = 0; i < rawEntries.Count; i++)
        {
            string code = codes[i];
            rawEntries[i].Props["code"] = code;
            registry.LootTableCodes.Add(code);
            registry.LootTableCodeByLegacyPath[rawEntries[i].LegacyRelPath] = code;
            lootDocs.Add(rawEntries[i].Props);
        }

        int legacyIniCount = rawEntries.Count;
        if (!registry.LootTableCodes.Contains("default-monster-loot"))
        {
            var fallback = new Dictionary<string, object?>
            {
                ["code"] = "default-monster-loot",
                ["randRange"] = 1000000,
                ["magicRate"] = 1,
                ["moneyDropChance"] = 0.01,
                ["minMoneyGold"] = 50,
                ["maxMoneyGold"] = 500,
                ["minItemLevel"] = 1,
                ["maxItemLevel"] = 10,
                ["weightedPools"] = new List<Dictionary<string, object?>>(),
            };
            registry.LootTableCodes.Add("default-monster-loot");
            lootDocs.Add(fallback);
            legacyIniCount++;
        }

        LegacyPathResolver.WriteCanonicalJson(
            Path.Combine(lootOutDir, "droprates.loot.json"),
            new Dictionary<string, object?>
            {
                ["$schema"] = "../../schemas/loot-table.schema.json",
                ["schemaVersion"] = "1.0.0",
                ["lootTables"] = lootDocs,
            });

        registry.RecordCount("loot.droprateTables", legacyIniCount, lootDocs.Count);
        return lootDocs.Count;
    }

    private static Dictionary<string, object?> ParseDropRateIniFile(
        string filePath,
        string legacyRelPath,
        ExportCatalogRegistry registry)
    {
        var sections = ParseIniSectionsLatin1(File.ReadAllBytes(filePath));
        sections.TryGetValue("Main", out Dictionary<string, string>? mainSec);
        mainSec ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int count = ParseIniInt(mainSec, "Count", 0);
        int randRange = Math.Max(1, ParseIniInt(mainSec, "RandRange", 1000000));
        int magicRate = Math.Max(0, ParseIniInt(mainSec, "MagicRate", 1));
        int moneyRate = Math.Max(0, ParseIniInt(mainSec, "MoneyRate", 0));
        int moneyScale = Math.Max(0, ParseIniInt(mainSec, "MoneyScale", 50));
        int minItemLvl = Math.Clamp(ParseIniInt(mainSec, "MinItemLevel", 1), 0, 30);
        int minItemLvlScale = Math.Max(0, ParseIniInt(mainSec, "MinItemLevelScale", 20));
        int maxItemLvl = Math.Clamp(ParseIniInt(mainSec, "MaxItemLevel", 10), 0, 30);
        int maxItemLvlScale = Math.Max(0, ParseIniInt(mainSec, "MaxItemLevelScale", 10));
        int maxDrops = Math.Max(0, ParseIniInt(mainSec, "MaxItemDrop", 0));
        bool teamShare = ParseIniInt(mainSec, "TeamShare", 0) != 0;
        int teamShareRate = Math.Max(0, ParseIniInt(mainSec, "TeamShareRate", 0));

        double moneyChance = Math.Round(Math.Clamp(moneyRate / 100.0, 0.0, 1.0), 6);
        int minMoneyGold = Math.Max(0, Math.Max(1, minItemLvl) * moneyScale);
        int maxMoneyGold = Math.Max(minMoneyGold, Math.Max(1, maxItemLvl) * moneyScale);

        var pools = new List<Dictionary<string, object?>>();
        int maxSecIdx = Math.Max(count, sections.Count);
        for (int sIdx = 1; sIdx <= maxSecIdx; sIdx++)
        {
            if (!sections.TryGetValue(sIdx.ToString(CultureInfo.InvariantCulture), out Dictionary<string, string>? sec))
            {
                continue;
            }

            int weight = Math.Max(0, ParseIniInt(sec, "RandRate", 0));
            int genre = Math.Max(0, ParseIniInt(sec, "Genre", 0));
            int quality = ParseIniInt(sec, "Quality", 0);
            if (quality is < 0 or > 4)
            {
                quality = 0;
            }

            int detail = Math.Max(0, ParseIniInt(sec, "Detail", 0));
            int particular = Math.Max(0, ParseIniInt(sec, "Particular", 0));
            int series = Math.Clamp(ParseIniInt(sec, "Series", -1), -1, 6);
            int pMinLvl = Math.Clamp(ParseIniInt(sec, "MinItemLevel", minItemLvl), 0, 30);
            int pMaxLvl = Math.Clamp(ParseIniInt(sec, "MaxItemLevel", maxItemLvl), 0, 30);
            int goldRow = ParseIniInt(sec, "GoldRow", 0);

            var poolEntry = new Dictionary<string, object?>
            {
                ["weight"] = weight,
                ["genre"] = genre,
                ["quality"] = quality,
                ["detailType"] = detail,
                ["particularType"] = particular,
                ["elementSeries"] = series,
                ["minLevel"] = pMinLvl,
                ["maxLevel"] = pMaxLvl,
            };

            if (quality == 1)
            {
                int goldId = goldRow >= 1 ? goldRow : (detail >= 1 ? detail : 0);
                if (goldId >= 1)
                {
                    poolEntry["goldEquipId"] = goldId;
                    if (registry.GoldenCodeByRowId.TryGetValue(goldId, out string? gCode))
                    {
                        poolEntry["itemCode"] = gCode;
                    }
                }
            }
            else if (quality == 3)
            {
                int platId = detail >= 1 ? detail : 0;
                if (platId >= 1 && registry.PlatinaCodeByRowId.TryGetValue(platId, out string? pCode))
                {
                    poolEntry["itemCode"] = pCode;
                }
            }
            else if (quality == 0 && genre != 0)
            {
                if (registry.ItemCodeByTupleAndLevel.TryGetValue((genre, detail, particular, pMinLvl), out string? itemCode) ||
                    registry.PrimaryItemCodeByTuple.TryGetValue((genre, detail, particular), out itemCode))
                {
                    poolEntry["itemCode"] = itemCode;
                }
            }

            if (sec.ContainsKey("MagicLevel1"))
            {
                poolEntry["magicLevel1"] = Math.Max(0, ParseIniInt(sec, "MagicLevel1", 0));
            }

            if (sec.ContainsKey("MinSocket"))
            {
                poolEntry["minSocket"] = Math.Max(0, ParseIniInt(sec, "MinSocket", 0));
            }

            if (sec.ContainsKey("MaxSocket"))
            {
                poolEntry["maxSocket"] = Math.Max(0, ParseIniInt(sec, "MaxSocket", 0));
            }

            pools.Add(poolEntry);
        }

        var doc = new Dictionary<string, object?>
        {
            ["code"] = string.Empty,
            ["legacyIniPath"] = legacyRelPath,
            ["randRange"] = randRange,
            ["magicRate"] = magicRate,
            ["moneyDropChance"] = moneyChance,
            ["moneyRate"] = moneyRate,
            ["moneyScale"] = moneyScale,
            ["minMoneyGold"] = minMoneyGold,
            ["maxMoneyGold"] = maxMoneyGold,
            ["minItemLevel"] = minItemLvl,
            ["minItemLevelScale"] = minItemLvlScale,
            ["maxItemLevel"] = maxItemLvl,
            ["maxItemLevelScale"] = maxItemLvlScale,
            ["maxDropsPerKill"] = maxDrops,
            ["isTeamShare"] = teamShare,
            ["teamShareRate"] = teamShareRate,
            ["weightedPools"] = pools,
        };

        return doc;
    }

    private static int ExportCoreNpcsGoldenAndPartner(
        string serverRootPath,
        string npcsOutDir,
        ExportCatalogRegistry registry)
    {
        var npcEntries = new List<(int Id, string Name, Dictionary<string, object?> Props)>(2550);
        int legacyNpcRows = 0;

        string? npcsPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\npcs.txt");
        if (npcsPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(npcsPath);
            legacyNpcRows = table.DataRowCount;
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                int id = r - 1;
                string rawName = table.GetString(r, 0, preferVietnamese: true, inheritRow2Default: false);
                string name = string.IsNullOrWhiteSpace(rawName) ? $"Ô trống {id}" : rawName;

                int kindVal = table.GetInt(r, 1, 0, inheritRow2Default: true);
                string entityKind = kindVal switch
                {
                    1 => "Player",
                    2 => "Partner",
                    3 => "DialogNpc",
                    _ => "Monster",
                };

                int defaultCamp = Math.Max(0, table.GetInt(r, 2, 0, inheritRow2Default: true));
                int series = Math.Clamp(table.GetInt(r, 3, 0, inheritRow2Default: true), -1, 5);
                int treasure = Math.Max(0, table.GetInt(r, 4, 1, inheritRow2Default: true));

                int walkSpeed = Math.Max(0, table.GetInt(r, 58, 7, inheritRow2Default: true));
                int runSpeed = Math.Max(0, table.GetInt(r, 59, 7, inheritRow2Default: true));
                int atkSpeed = Math.Max(0, table.GetInt(r, 60, 18, inheritRow2Default: true));
                int castSpeed = Math.Max(0, table.GetInt(r, 61, 18, inheritRow2Default: true));
                int visionRadius = Math.Max(0, table.GetInt(r, 62, 400, inheritRow2Default: true));
                int hitRecover = Math.Max(0, table.GetInt(r, 63, 12, inheritRow2Default: true));
                int activeRadius = Math.Max(0, table.GetInt(r, 64, 400, inheritRow2Default: true));
                int pursuitRadius = Math.Max(activeRadius, 512);
                int respawnFrames = Math.Max(0, table.GetInt(r, 85, 85, inheritRow2Default: true));

                int baseExp = Math.Max(0, table.GetInt(r, 33, 100, inheritRow2Default: true));
                int baseLife = Math.Max(1, table.GetInt(r, 37, 100, inheritRow2Default: true));

                var skillList = new List<Dictionary<string, object?>>(4);
                int primarySkillId = 0;
                for (int k = 0; k < 4; k++)
                {
                    int colSkill = 23 + (k * 2);
                    int colLvl = 24 + (k * 2);
                    int sid = ResolveSkillIdFromCell(table, r, colSkill, inheritRow2Default: true, registry);
                    if (sid <= 0)
                    {
                        continue;
                    }

                    if (primarySkillId == 0)
                    {
                        primarySkillId = sid;
                    }

                    ParseBaseAndStepInt(table.GetString(r, colLvl, preferVietnamese: false, inheritRow2Default: true), out int baseLvl, out int lvlStep);
                    var skObj = new Dictionary<string, object?>
                    {
                        ["skillId"] = sid,
                        ["baseLevel"] = Math.Max(0, baseLvl),
                        ["levelStep"] = lvlStep,
                        ["castWeight"] = 25,
                    };
                    if (registry.SkillCodeById.TryGetValue(sid, out string? sCode))
                    {
                        skObj["skillCode"] = sCode;
                    }

                    skillList.Add(skObj);
                }

                int auraSkillId = Math.Max(0, ResolveSkillIdFromCell(table, r, 99, inheritRow2Default: false, registry));
                ParseBaseAndStepInt(table.GetString(r, 100, preferVietnamese: false, inheritRow2Default: true), out int auraBaseLvl, out _);
                int passiveSkillId = Math.Max(0, ResolveSkillIdFromCell(table, r, 101, inheritRow2Default: false, registry));
                ParseBaseAndStepInt(table.GetString(r, 102, preferVietnamese: false, inheritRow2Default: true), out int passiveBaseLvl, out _);

                int aiMode = Math.Max(0, table.GetInt(r, 65, 3, inheritRow2Default: true));
                int aiMaxTime = Math.Max(0, table.GetInt(r, 88, 36, inheritRow2Default: true));
                var aiParams = new int[9];
                for (int a = 0; a < 9; a++)
                {
                    aiParams[a] = table.GetInt(r, 66 + a, 0, inheritRow2Default: true);
                }

                string actionScript = table.GetEnginePath(r, 31, inheritRow2Default: false);
                string levelScript = table.GetEnginePath(r, 32, inheritRow2Default: true);
                string dropRateRaw = table.GetEnginePath(r, 87, inheritRow2Default: true);
                string? lootCode = registry.TryResolveLootCode(dropRateRaw);
                if (lootCode is null && entityKind == "Monster" && registry.LootTableCodes.Contains("default-monster-loot"))
                {
                    lootCode = "default-monster-loot";
                }

                var combatScaling = new Dictionary<string, object?>
                {
                    ["expParams"] = ReadQuartet(table, r, 33),
                    ["lifeParams"] = ReadQuartet(table, r, 37),
                    ["lifeReplenish"] = ReadBaseAndPerLevel(table.GetString(r, 41, preferVietnamese: false, inheritRow2Default: true)),
                    ["attackRatingParams"] = ReadQuartet(table, r, 42),
                    ["defenseParams"] = ReadQuartet(table, r, 46),
                    ["minDamageParams"] = ReadQuartet(table, r, 50),
                    ["maxDamageParams"] = ReadQuartet(table, r, 54),
                    ["fireResist"] = ReadResistScaling(table, r, 75, 80),
                    ["coldResist"] = ReadResistScaling(table, r, 76, 81),
                    ["lightResist"] = ReadResistScaling(table, r, 77, 82),
                    ["poisonResist"] = ReadResistScaling(table, r, 78, 83),
                    ["physicsResist"] = ReadResistScaling(table, r, 79, 84),
                    ["physicalDamageBase"] = ReadElementalDamage(table, r, 89, 90),
                    ["poisonDamageBase"] = ReadElementalDamage(table, r, 91, 92),
                    ["coldDamageBase"] = ReadElementalDamage(table, r, 93, 94),
                    ["fireDamageBase"] = ReadElementalDamage(table, r, 95, 96),
                    ["lightingDamageBase"] = ReadElementalDamage(table, r, 97, 98),
                };
                if (!string.IsNullOrWhiteSpace(levelScript))
                {
                    combatScaling["levelScriptCode"] = Path.GetFileNameWithoutExtension(levelScript).ToLowerInvariant();
                }

                var props = new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["code"] = string.Empty,
                    ["name"] = name,
                    ["entityKind"] = entityKind,
                    ["defaultCamp"] = defaultCamp,
                    ["elementSeries"] = series,
                    ["walkSpeedMpsPerFrame"] = walkSpeed,
                    ["runSpeedMpsPerFrame"] = runSpeed,
                    ["attackSpeedFrames"] = atkSpeed,
                    ["castSpeedFrames"] = castSpeed,
                    ["hitRecoverFrames"] = hitRecover,
                    ["attackRadiusMps"] = 96,
                    ["activeRadiusMps"] = activeRadius,
                    ["visionRadiusMps"] = visionRadius,
                    ["pursuitRadiusMps"] = pursuitRadius,
                    ["dialogRadiusCells"] = 4,
                    ["respawnFrames"] = respawnFrames,
                    ["baseLife"] = baseLife,
                    ["baseExp"] = baseExp,
                    ["treasureDropCount"] = treasure,
                    ["primarySkillId"] = primarySkillId,
                    ["auraSkillId"] = auraSkillId,
                    ["auraSkillLevel"] = Math.Max(0, auraBaseLvl),
                    ["passiveSkillId"] = passiveSkillId,
                    ["passiveSkillLevel"] = Math.Max(0, passiveBaseLvl),
                    ["skills"] = skillList,
                    ["aiConfig"] = new Dictionary<string, object?>
                    {
                        ["aiMode"] = aiMode,
                        ["aiMaxTimeFrames"] = aiMaxTime,
                        ["aiParams"] = aiParams,
                    },
                    ["combatScaling"] = combatScaling,
                };

                if (!string.IsNullOrWhiteSpace(actionScript))
                {
                    props["actionScriptPath"] = actionScript;
                }

                if (lootCode is not null)
                {
                    props["lootTableCode"] = lootCode;
                }

                npcEntries.Add((id, name, props));
                registry.NpcMetaById[id] = (name, entityKind, defaultCamp, series, respawnFrames, lootCode);
            }
        }

        string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(npcEntries, e => e.Name, e => e.Id);
        var npcDocs = new List<Dictionary<string, object?>>(npcEntries.Count);
        for (int i = 0; i < npcEntries.Count; i++)
        {
            npcEntries[i].Props["code"] = codes[i];
            registry.NpcCodeById[npcEntries[i].Id] = codes[i];
            npcDocs.Add(npcEntries[i].Props);
        }

        registry.RecordCount("npcs.templates", legacyNpcRows, npcDocs.Count);

        var goldenTemplates = LoadGoldenNpcTemplates(serverRootPath, registry);
        Dictionary<string, object?> partnerConfig = LoadPartnerConfig(serverRootPath, registry);

        LegacyPathResolver.WriteCanonicalJson(
            Path.Combine(npcsOutDir, "core.npcs.json"),
            new Dictionary<string, object?>
            {
                ["$schema"] = "../../schemas/npc-catalog.schema.json",
                ["schemaVersion"] = "1.0.0",
                ["npcs"] = npcDocs,
                ["goldenNpcTemplates"] = goldenTemplates,
                ["partnerConfig"] = partnerConfig,
            });

        return npcDocs.Count;
    }

    private static List<Dictionary<string, object?>> LoadGoldenNpcTemplates(string serverRootPath, ExportCatalogRegistry registry)
    {
        var entries = new List<(int Id, string Name, Dictionary<string, object?> Props)>();
        int legacyRows = 0;
        string? path = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\npc\npcgoldtemplate.txt");
        if (path is not null)
        {
            KTabFileTable table = KTabFileTable.Load(path);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                legacyRows++;
                int id = legacyRows;
                string rawName = table.GetString(r, 0);
                string name = string.IsNullOrWhiteSpace(rawName) ? $"Boss Hoàng Kim {id}" : rawName;

                var props = new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["code"] = string.Empty,
                    ["name"] = name,
                    ["expMultiplierPercent"] = Math.Max(0, table.GetInt(r, 1, 100)),
                    ["lifeMultiplierPercent"] = Math.Max(0, table.GetInt(r, 2, 100)),
                    ["lifeReplenishBonus"] = table.GetInt(r, 3, 0),
                    ["attackRatingMultiplierPercent"] = Math.Max(0, table.GetInt(r, 4, 100)),
                    ["defenseMultiplierPercent"] = Math.Max(0, table.GetInt(r, 5, 100)),
                    ["minDamageMultiplierPercent"] = Math.Max(0, table.GetInt(r, 6, 100)),
                    ["maxDamageMultiplierPercent"] = Math.Max(0, table.GetInt(r, 7, 100)),
                    ["treasureDropMultiplier"] = Math.Max(0, table.GetInt(r, 8, 1)),
                    ["walkSpeedBonus"] = table.GetInt(r, 9, 0),
                    ["runSpeedBonus"] = table.GetInt(r, 10, 0),
                    ["attackSpeedBonus"] = table.GetInt(r, 11, 0),
                    ["castSpeedBonus"] = table.GetInt(r, 12, 0),
                    ["fireResistBonus"] = table.GetInt(r, 15, 0),
                    ["coldResistBonus"] = table.GetInt(r, 17, 0),
                    ["lightResistBonus"] = table.GetInt(r, 19, 0),
                    ["poisonResistBonus"] = table.GetInt(r, 21, 0),
                    ["physicsResistBonus"] = table.GetInt(r, 23, 0),
                };
                entries.Add((id, name, props));
            }
        }

        string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(entries, e => e.Name, e => e.Id);
        var docs = new List<Dictionary<string, object?>>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].Props["code"] = codes[i];
            docs.Add(entries[i].Props);
        }

        registry.RecordCount("npcs.goldenTemplates", legacyRows, docs.Count);
        return docs;
    }

    private static Dictionary<string, object?> LoadPartnerConfig(string serverRootPath, ExportCatalogRegistry registry)
    {
        var maxSlotsByLevel = new List<Dictionary<string, object?>>();
        int legacyExpRows = 0;
        string? expPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\partner\level_exp.txt");
        if (expPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(expPath);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                int lvl = table.GetInt(r, 0, 0);
                if (lvl < 1)
                {
                    continue;
                }

                legacyExpRows++;
                int exp = Math.Max(0, table.GetInt(r, 1, 0));
                int maxSlots = Math.Clamp(1 + (lvl / 10), 1, 10);
                maxSlotsByLevel.Add(new Dictionary<string, object?>
                {
                    ["level"] = lvl,
                    ["maxSkillSlots"] = maxSlots,
                    ["expRequired"] = exp,
                });
            }
        }

        var aptEntries = new List<(int Id, string Name, Dictionary<string, object?> Props)>();
        int legacyAptRows = 0;
        string? aptPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\partner\aptitude_mode.txt");
        if (aptPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(aptPath);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                legacyAptRows++;
                int id = Math.Max(0, table.GetInt(r, 0, r));
                string rawName = table.GetString(r, 1);
                string name = string.IsNullOrWhiteSpace(rawName) ? $"Tư chất {id}" : rawName;
                var weights = new List<int>();
                for (int c = 2; c < table.GetColumnCount(r); c++)
                {
                    weights.Add(Math.Max(0, table.GetInt(r, c, 0)));
                }

                aptEntries.Add((id, name, new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["code"] = string.Empty,
                    ["name"] = name,
                    ["weights"] = weights,
                }));
            }
        }

        string[] aptCodes = HybridLegacyTextDecoder.AssignSymmetricCodes(aptEntries, e => e.Name, e => e.Id);
        var aptDocs = new List<Dictionary<string, object?>>(aptEntries.Count);
        for (int i = 0; i < aptEntries.Count; i++)
        {
            aptEntries[i].Props["code"] = aptCodes[i];
            aptDocs.Add(aptEntries[i].Props);
        }

        var skillReqs = new List<Dictionary<string, object?>>();
        int legacyReqRows = 0;
        string? reqPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\partner\skill_requirement.txt");
        if (reqPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(reqPath);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                int skillId = table.GetInt(r, 4, 0);
                if (skillId < 1)
                {
                    continue;
                }

                legacyReqRows++;
                skillReqs.Add(new Dictionary<string, object?>
                {
                    ["skillBookId"] = Math.Max(0, table.GetInt(r, 0, 0)),
                    ["baseLevel"] = Math.Max(1, table.GetInt(r, 1, 1)),
                    ["levelIncrement"] = Math.Max(0, table.GetInt(r, 2, 0)),
                    ["seriesLimit"] = Math.Clamp(table.GetInt(r, 3, -1), -1, 5),
                    ["skillId"] = skillId,
                });
            }
        }

        registry.RecordCount("npcs.partnerLevelExp", legacyExpRows, maxSlotsByLevel.Count);
        registry.RecordCount("npcs.partnerAptitudeModes", legacyAptRows, aptDocs.Count);
        registry.RecordCount("npcs.partnerSkillRequirements", legacyReqRows, skillReqs.Count);

        return new Dictionary<string, object?>
        {
            ["maxSkillSlotsByLevel"] = maxSlotsByLevel,
            ["aptitudeModes"] = aptDocs,
            ["skillRequirements"] = skillReqs,
        };
    }

    private static (int GlobalSpawnCount, int EventSpawnCount) ExportAutoExecWorldSpawns(
        string serverRootPath,
        string npcsOutDir,
        ExportCatalogRegistry registry)
    {
        var sharedTokenCache = new Dictionary<string, List<Lua4Token>>(StringComparer.OrdinalIgnoreCase);
        string? autoExecPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"script\global\autoexec.lua");

        var globalRawSpawns = new List<(int TplId, int Level, int Series, int MapId, int MpsX, int MpsY, string Name, string ScriptPath)>();
        var eventRawSpawns = new List<(string FeatureFlag, int TplId, int Level, int Series, int MapId, int MpsX, int MpsY, string Name, string ScriptPath)>();
        var globalRawTraps = new List<(int MapId, int MpsX, int MpsY, string ScriptPath)>();
        var eventRawTraps = new List<(string FeatureFlag, int MapId, int MpsX, int MpsY, string ScriptPath)>();

        var seenSpawnKeys = new HashSet<(int MapId, int TplId, int MpsX, int MpsY)>();
        var seenTrapKeys = new HashSet<(int MapId, int MpsX, int MpsY, string ScriptPath)>();

        if (autoExecPath is not null && File.Exists(autoExecPath))
        {
            // Pass 1: Default startup (vn)
            var baseInterp = new Lua4Interpreter(serverRootPath, sharedTokenCache, lenientMissingGlobals: true);
            baseInterp.ResetStepBudget(25_000_000);
            baseInterp.ExecuteFile(autoExecPath);
            baseInterp.ResetStepBudget(25_000_000);
            baseInterp.CallGlobal("main");

            foreach (RecordedLuaNpcSpawn s in baseInterp.RecordedNpcs)
            {
                if (!TryNormalizeRecordedNpc(s, registry, out var norm))
                {
                    continue;
                }

                if (!seenSpawnKeys.Add((norm.MapId, norm.TplId, norm.MpsX, norm.MpsY)))
                {
                    continue;
                }

                if (IsSeasonalScriptPath(norm.ScriptPath))
                {
                    eventRawSpawns.Add(("CFG_DefaultSeasonalScript", norm.TplId, norm.Level, norm.Series, norm.MapId, norm.MpsX, norm.MpsY, norm.Name, norm.ScriptPath));
                }
                else
                {
                    globalRawSpawns.Add(norm);
                }
            }

            foreach (RecordedLuaMapTrap t in baseInterp.RecordedTraps)
            {
                string scriptPath = DecodeLatin1EnginePath(t.RawScriptLatin1);
                if (t.MapId < 1 || t.MpsX < 0 || t.MpsY < 0 || string.IsNullOrWhiteSpace(scriptPath))
                {
                    continue;
                }

                if (seenTrapKeys.Add((t.MapId, t.MpsX, t.MpsY, scriptPath)))
                {
                    globalRawTraps.Add((t.MapId, t.MpsX, t.MpsY, scriptPath));
                }
            }

            // Pass 2: Feature & Seasonal Event flags (pinned to 1)
            foreach (string flag in FeatureCfgFlags)
            {
                var flagInterp = new Lua4Interpreter(serverRootPath, sharedTokenCache, lenientMissingGlobals: true);
                flagInterp.PinGlobal(flag, Lua4Value.FromNumber(1));
                flagInterp.ResetStepBudget(25_000_000);
                flagInterp.ExecuteFile(autoExecPath);
                flagInterp.ResetStepBudget(25_000_000);
                flagInterp.CallGlobal("main");

                foreach (RecordedLuaNpcSpawn s in flagInterp.RecordedNpcs)
                {
                    if (!TryNormalizeRecordedNpc(s, registry, out var norm))
                    {
                        continue;
                    }

                    if (seenSpawnKeys.Add((norm.MapId, norm.TplId, norm.MpsX, norm.MpsY)))
                    {
                        eventRawSpawns.Add((flag, norm.TplId, norm.Level, norm.Series, norm.MapId, norm.MpsX, norm.MpsY, norm.Name, norm.ScriptPath));
                    }
                }

                foreach (RecordedLuaMapTrap t in flagInterp.RecordedTraps)
                {
                    string scriptPath = DecodeLatin1EnginePath(t.RawScriptLatin1);
                    if (t.MapId < 1 || t.MpsX < 0 || t.MpsY < 0 || string.IsNullOrWhiteSpace(scriptPath))
                    {
                        continue;
                    }

                    if (seenTrapKeys.Add((t.MapId, t.MpsX, t.MpsY, scriptPath)))
                    {
                        eventRawTraps.Add((flag, t.MapId, t.MpsX, t.MpsY, scriptPath));
                    }
                }
            }
        }

        // Build global-spawns.npcs.json
        List<Dictionary<string, object?>> globalGroups = BuildWorldSpawnGroups(
            globalRawSpawns.Select(s => ((string?)null, s.TplId, s.Level, s.Series, s.MapId, s.MpsX, s.MpsY, s.Name, s.ScriptPath)).ToList(),
            spawnCategory: "globalPermanent",
            enabledByDefault: true,
            registry);

        List<Dictionary<string, object?>> globalPortals = BuildWorldPortals(
            globalRawTraps.Select(t => ((string?)null, t.MapId, t.MpsX, t.MpsY, t.ScriptPath)).ToList(),
            spawnCategory: "globalPermanent",
            enabledByDefault: true,
            registry);

        var globalDoc = new Dictionary<string, object?>
        {
            ["$schema"] = "../../schemas/npc-catalog.schema.json",
            ["schemaVersion"] = "1.0.0",
            ["worldSpawnGroups"] = globalGroups,
        };
        if (globalPortals.Count > 0)
        {
            globalDoc["worldPortals"] = globalPortals;
        }

        LegacyPathResolver.WriteCanonicalJson(Path.Combine(npcsOutDir, "global-spawns.npcs.json"), globalDoc);

        // Build event-spawns.npcs.json
        List<Dictionary<string, object?>> eventGroups = BuildWorldSpawnGroups(
            eventRawSpawns.Select(s => ((string?)s.FeatureFlag, s.TplId, s.Level, s.Series, s.MapId, s.MpsX, s.MpsY, s.Name, s.ScriptPath)).ToList(),
            spawnCategory: "eventOrFeature",
            enabledByDefault: false,
            registry);

        List<Dictionary<string, object?>> eventPortals = BuildWorldPortals(
            eventRawTraps.Select(t => ((string?)t.FeatureFlag, t.MapId, t.MpsX, t.MpsY, t.ScriptPath)).ToList(),
            spawnCategory: "eventOrFeature",
            enabledByDefault: false,
            registry);

        var eventDoc = new Dictionary<string, object?>
        {
            ["$schema"] = "../../schemas/npc-catalog.schema.json",
            ["schemaVersion"] = "1.0.0",
            ["worldSpawnGroups"] = eventGroups,
        };
        if (eventPortals.Count > 0)
        {
            eventDoc["worldPortals"] = eventPortals;
        }

        LegacyPathResolver.WriteCanonicalJson(Path.Combine(npcsOutDir, "event-spawns.npcs.json"), eventDoc);

        registry.RecordCount("npcs.globalAutoExecPlacements", globalRawSpawns.Count, globalRawSpawns.Count);
        registry.RecordCount("npcs.eventAutoExecPlacements", eventRawSpawns.Count, eventRawSpawns.Count);

        return (globalRawSpawns.Count, eventRawSpawns.Count);
    }

    private static List<Dictionary<string, object?>> BuildWorldSpawnGroups(
        List<(string? FeatureFlag, int TplId, int Level, int Series, int MapId, int MpsX, int MpsY, string Name, string ScriptPath)> spawns,
        string spawnCategory,
        bool enabledByDefault,
        ExportCatalogRegistry registry)
    {
        var grouped = spawns
            .GroupBy(s => (s.FeatureFlag ?? string.Empty, s.TplId, s.Name, s.Level, s.Series, s.ScriptPath))
            .ToList();

        var rawGroups = new List<(int Index, string BaseSlug, Dictionary<string, object?> Props)>(grouped.Count);
        for (int i = 0; i < grouped.Count; i++)
        {
            var g = grouped[i];
            int tplId = g.Key.TplId;
            registry.NpcMetaById.TryGetValue(tplId, out var meta);
            string entityKind = meta.EntityKind is "Monster" or "DialogNpc" or "Partner" ? meta.EntityKind : "DialogNpc";
            int camp = Math.Max(0, meta.DefaultCamp);
            int series = g.Key.Series is >= -1 and <= 5 ? g.Key.Series : Math.Clamp(meta.DefaultSeries, -1, 5);
            int fightMode = entityKind == "Monster" ? 1 : 0;

            var placements = new List<Dictionary<string, object?>>();
            foreach (var p in g.OrderBy(x => x.MapId).ThenBy(x => x.MpsX).ThenBy(x => x.MpsY))
            {
                var plObj = new Dictionary<string, object?>
                {
                    ["mapId"] = p.MapId,
                    ["mpsX"] = p.MpsX,
                    ["mpsY"] = p.MpsY,
                };
                if (registry.ActiveMapIds.Contains(p.MapId) && registry.MapCodeById.TryGetValue(p.MapId, out string? mapCode))
                {
                    plObj["mapCode"] = mapCode;
                }

                placements.Add(plObj);
            }

            var props = new Dictionary<string, object?>
            {
                ["code"] = string.Empty,
                ["name"] = g.Key.Name,
                ["spawnCategory"] = spawnCategory,
                ["enabledByDefault"] = enabledByDefault,
                ["npcTemplateId"] = tplId,
                ["entityKind"] = entityKind,
                ["level"] = Math.Max(1, g.Key.Level),
                ["series"] = series,
                ["camp"] = camp,
                ["fightMode"] = fightMode,
                ["placements"] = placements,
            };

            if (!string.IsNullOrEmpty(g.Key.Item1))
            {
                props["featureFlag"] = g.Key.Item1;
            }

            if (registry.NpcCodeById.TryGetValue(tplId, out string? npcCode))
            {
                props["npcCode"] = npcCode;
            }

            if (!string.IsNullOrWhiteSpace(g.Key.ScriptPath))
            {
                props["scriptPath"] = g.Key.ScriptPath;
            }

            string slugSeed = !string.IsNullOrEmpty(g.Key.Item1)
                ? $"{HybridLegacyTextDecoder.Slugify(g.Key.Item1)}-{HybridLegacyTextDecoder.Slugify(g.Key.Name)}"
                : HybridLegacyTextDecoder.Slugify(g.Key.Name);
            rawGroups.Add((i + 1, slugSeed, props));
        }

        string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(rawGroups, x => x.BaseSlug, x => x.Index);
        var result = new List<Dictionary<string, object?>>(rawGroups.Count);
        for (int i = 0; i < rawGroups.Count; i++)
        {
            rawGroups[i].Props["code"] = codes[i];
            result.Add(rawGroups[i].Props);
        }

        return result;
    }

    private static List<Dictionary<string, object?>> BuildWorldPortals(
        List<(string? FeatureFlag, int MapId, int MpsX, int MpsY, string ScriptPath)> traps,
        string spawnCategory,
        bool enabledByDefault,
        ExportCatalogRegistry registry)
    {
        var grouped = traps
            .GroupBy(t => (t.FeatureFlag ?? string.Empty, t.MapId, t.ScriptPath))
            .ToList();

        var rawPortals = new List<(int Index, string BaseSlug, Dictionary<string, object?> Props)>(grouped.Count);
        for (int i = 0; i < grouped.Count; i++)
        {
            var g = grouped[i];
            var points = g
                .OrderBy(x => x.MpsX)
                .ThenBy(x => x.MpsY)
                .Select(x => new Dictionary<string, object?>
                {
                    ["mpsX"] = x.MpsX,
                    ["mpsY"] = x.MpsY,
                })
                .ToList();

            var props = new Dictionary<string, object?>
            {
                ["code"] = string.Empty,
                ["spawnCategory"] = spawnCategory,
                ["enabledByDefault"] = enabledByDefault,
                ["mapId"] = g.Key.MapId,
                ["scriptPath"] = g.Key.ScriptPath,
                ["action"] = "customScript",
                ["points"] = points,
            };

            if (!string.IsNullOrEmpty(g.Key.Item1))
            {
                props["featureFlag"] = g.Key.Item1;
            }

            if (registry.ActiveMapIds.Contains(g.Key.MapId) && registry.MapCodeById.TryGetValue(g.Key.MapId, out string? mapCode))
            {
                props["mapCode"] = mapCode;
            }

            string scriptName = Path.GetFileNameWithoutExtension(g.Key.ScriptPath);
            string slugSeed = $"trap-m{g.Key.MapId}-{HybridLegacyTextDecoder.Slugify(scriptName, "portal")}";
            rawPortals.Add((i + 1, slugSeed, props));
        }

        string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(rawPortals, x => x.BaseSlug, x => x.Index);
        var result = new List<Dictionary<string, object?>>(rawPortals.Count);
        for (int i = 0; i < rawPortals.Count; i++)
        {
            rawPortals[i].Props["code"] = codes[i];
            result.Add(rawPortals[i].Props);
        }

        return result;
    }

    private static bool TryNormalizeRecordedNpc(
        RecordedLuaNpcSpawn s,
        ExportCatalogRegistry registry,
        out (int TplId, int Level, int Series, int MapId, int MpsX, int MpsY, string Name, string ScriptPath) norm)
    {
        norm = default;
        if (s.NpcTemplateId < 0 || !registry.NpcCodeById.ContainsKey(s.NpcTemplateId) || s.MapId < 1 || s.MpsX < 0 || s.MpsY < 0)
        {
            return false;
        }

        string decodedName = string.Empty;
        if (!string.IsNullOrEmpty(s.RawNameLatin1))
        {
            byte[] rawNameBytes = Encoding.Latin1.GetBytes(s.RawNameLatin1);
            decodedName = HybridLegacyTextDecoder.DecodeSmart(rawNameBytes, preferVietnamese: true).Trim();
        }

        if (string.IsNullOrWhiteSpace(decodedName))
        {
            decodedName = registry.NpcMetaById.TryGetValue(s.NpcTemplateId, out var meta) && !string.IsNullOrWhiteSpace(meta.Name)
                ? meta.Name
                : $"NPC {s.NpcTemplateId}";
        }

        string scriptPath = DecodeLatin1EnginePath(s.RawScriptLatin1);
        int level = Math.Max(1, s.Level);
        int series = Math.Clamp(s.Series, -1, 5);

        norm = (s.NpcTemplateId, level, series, s.MapId, s.MpsX, s.MpsY, decodedName, scriptPath);
        return true;
    }

    private static bool IsSeasonalScriptPath(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return false;
        }

        return scriptPath.Contains("benguyet", StringComparison.OrdinalIgnoreCase) ||
               scriptPath.Contains(@"\missions\", StringComparison.OrdinalIgnoreCase);
    }

    private static string DecodeLatin1EnginePath(string rawLatin1)
    {
        if (string.IsNullOrWhiteSpace(rawLatin1))
        {
            return string.Empty;
        }

        byte[] bytes = Encoding.Latin1.GetBytes(rawLatin1);
        return LegacyPathResolver.NormalizeEnginePath(bytes);
    }

    private static int ResolveSkillIdFromCell(
        KTabFileTable table,
        int r,
        int col,
        bool inheritRow2Default,
        ExportCatalogRegistry registry)
    {
        ReadOnlySpan<byte> raw = table.GetRawCell(r, col, inheritRow2Default);
        if (raw.IsEmpty)
        {
            return 0;
        }

        string ascii = Encoding.Latin1.GetString(raw).Trim();
        if (int.TryParse(ascii, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric))
        {
            return numeric;
        }

        string vnName = HybridLegacyTextDecoder.DecodeSmart(raw, preferVietnamese: true).Trim();
        if (vnName.Length > 0 && registry.SkillIdByName.TryGetValue(vnName, out int sid))
        {
            return sid;
        }

        return 0;
    }

    private static Dictionary<string, object?> ReadQuartet(KTabFileTable table, int r, int startCol) =>
        new()
        {
            ["param"] = BoxNumber(table.GetDouble(r, startCol, 100, inheritRow2Default: true)),
            ["param1"] = BoxNumber(table.GetDouble(r, startCol + 1, 0, inheritRow2Default: true)),
            ["param2"] = BoxNumber(table.GetDouble(r, startCol + 2, 0, inheritRow2Default: true)),
            ["param3"] = BoxNumber(table.GetDouble(r, startCol + 3, 0, inheritRow2Default: true)),
        };

    private static Dictionary<string, object?> ReadBaseAndPerLevel(string rawPipePair)
    {
        ParseBaseAndStepDouble(rawPipePair, out double b, out double p);
        return new Dictionary<string, object?>
        {
            ["base"] = BoxNumber(b),
            ["perLevel"] = BoxNumber(p),
        };
    }

    private static Dictionary<string, object?> ReadResistScaling(KTabFileTable table, int r, int colPair, int colMax)
    {
        ParseBaseAndStepDouble(table.GetString(r, colPair, preferVietnamese: false, inheritRow2Default: true), out double b, out double p);
        return new Dictionary<string, object?>
        {
            ["base"] = BoxNumber(b),
            ["perLevel"] = BoxNumber(p),
            ["max"] = table.GetInt(r, colMax, 75, inheritRow2Default: true),
        };
    }

    private static Dictionary<string, object?> ReadElementalDamage(KTabFileTable table, int r, int colDmg, int colMagic)
    {
        ParseBaseAndStepDouble(table.GetString(r, colDmg, preferVietnamese: false, inheritRow2Default: true), out double b, out double p);
        int rawMode = table.GetInt(r, colMagic, 0, inheritRow2Default: true);
        int mode = rawMode is 0 or 1 or 2 ? rawMode : 0;
        return new Dictionary<string, object?>
        {
            ["base"] = BoxNumber(b),
            ["perLevel"] = BoxNumber(p),
            ["magicMode"] = mode,
        };
    }

    private static object BoxNumber(double val)
    {
        if (double.IsNaN(val) || double.IsInfinity(val))
        {
            return 0;
        }

        if (val == Math.Floor(val) && val is >= int.MinValue and <= int.MaxValue)
        {
            return (int)val;
        }

        return Math.Round(val, 4);
    }

    private static void ParseBaseAndStepInt(string text, out int baseVal, out int stepVal)
    {
        ParseBaseAndStepDouble(text, out double b, out double s);
        baseVal = (int)Math.Round(b);
        stepVal = (int)Math.Round(s);
    }

    private static void ParseBaseAndStepDouble(string text, out double baseVal, out double stepVal)
    {
        baseVal = 0;
        stepVal = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        int pipe = text.IndexOf('|');
        if (pipe < 0)
        {
            double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out baseVal);
            return;
        }

        double.TryParse(text[..pipe].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out baseVal);
        double.TryParse(text[(pipe + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out stepVal);
    }

    private static Dictionary<string, Dictionary<string, string>> ParseIniSectionsLatin1(byte[] rawBytes)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string current = string.Empty;
        string content = Encoding.Latin1.GetString(rawBytes);

        foreach (string rawLine in content.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = line[1..^1].Trim();
                if (!sections.ContainsKey(current))
                {
                    sections[current] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                continue;
            }

            int eq = line.IndexOf('=');
            if (eq > 0 && current.Length > 0)
            {
                sections[current][line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
        }

        return sections;
    }

    private static int ParseIniInt(Dictionary<string, string> sec, string key, int fallback) =>
        sec.TryGetValue(key, out string? s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v
            : fallback;

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
