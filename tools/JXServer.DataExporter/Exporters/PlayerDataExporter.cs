using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JXServer.DataExporter.Legacy;

namespace JXServer.DataExporter.Exporters;

/// <summary>
/// Exports starter player templates (<c>newplayerini00..09.ini</c>), series growth rules (<c>level_add.txt</c>),
/// five-elements matrix, level &amp; leadership experience tables (<c>level_exp.txt</c>, <c>level_lead_exp.txt</c>),
/// factions &amp; rank titles (<c>门派设定.ini</c>, <c>factiontitle.txt</c>), starter villages (<c>nativeplacelist.ini</c>,
/// <c>revivalid.ini</c>, <c>revivepos.ini</c>), PK rules (<c>pkpunish.txt</c>), meridians (<c>meridian.txt</c>,
/// <c>meridian_level.txt</c>), stamina rules (<c>stamina.ini</c>), team resist auras (<c>team_resist_effect.ini</c>),
/// and Tong progression (<c>tong_level_data.txt</c>, <c>workshops.txt</c>, <c>tongstunt_setting.txt</c>)
/// into <c>data/players/progression.players.json</c> per <c>JXData Standard v1</c>.
/// </summary>
public sealed class PlayerDataExporter
{
    private static readonly string[] SeriesSlugs = ["metal", "wood", "water", "fire", "earth"];
    private static readonly string[] GenderSlugs = ["male", "female"];
    private static readonly string[] TeamGrantedResistTypes = ["physics", "poison", "cold", "fire", "light"];

    public int ExportPlayers(string serverRootPath, string outputDataDirectory, ExportCatalogRegistry? registry = null)
    {
        registry ??= new ExportCatalogRegistry();

        string playersOutDir = Path.Combine(outputDataDirectory, "players");
        Directory.CreateDirectory(playersOutDir);

        string obsoleteFile = Path.Combine(playersOutDir, "players.players.json");
        if (File.Exists(obsoleteFile))
        {
            File.Delete(obsoleteFile);
        }

        List<Dictionary<string, object?>> characterTemplates = ExportCharacterTemplates(serverRootPath, registry);
        List<Dictionary<string, object?>> seriesGrowthRules = ExportSeriesGrowthRules(serverRootPath, registry);
        var fiveElementsMatrix = new Dictionary<string, object?>
        {
            ["generates"] = new[] { 2, 3, 1, 4, 0 },
            ["conquers"] = new[] { 1, 4, 3, 0, 2 },
            ["conqueredDamageBonusPercent"] = 20,
            ["conqueredDamagePenaltyPercent"] = -20,
        };
        List<Dictionary<string, object?>> levelExpTable = ExportLevelExpTable(serverRootPath, registry);
        List<Dictionary<string, object?>> factions = ExportFactions(serverRootPath, registry);
        List<Dictionary<string, object?>> starterVillages = ExportStarterVillages(serverRootPath, registry);
        Dictionary<string, object?> pkRules = ExportPkRules(serverRootPath, registry);
        List<Dictionary<string, object?>> meridians = ExportMeridians(serverRootPath, registry);
        Dictionary<string, object?> staminaRules = ExportStaminaRules(serverRootPath);
        List<Dictionary<string, object?>> teamResistEffects = ExportTeamResistEffects(serverRootPath, registry);
        Dictionary<string, object?> tongConfig = ExportTongConfig(serverRootPath, registry);

        var rootDoc = new Dictionary<string, object?>
        {
            ["$schema"] = "../../schemas/player-template.schema.json",
            ["schemaVersion"] = "1.0.0",
            ["characterTemplates"] = characterTemplates,
            ["seriesGrowthRules"] = seriesGrowthRules,
            ["fiveElementsMatrix"] = fiveElementsMatrix,
            ["levelExpTable"] = levelExpTable,
            ["factions"] = factions,
            ["starterVillages"] = starterVillages,
            ["pkRules"] = pkRules,
            ["meridians"] = meridians,
            ["staminaRules"] = staminaRules,
            ["teamResistEffects"] = teamResistEffects,
            ["tongConfig"] = tongConfig,
        };

        LegacyPathResolver.WriteCanonicalJson(Path.Combine(playersOutDir, "progression.players.json"), rootDoc);
        return characterTemplates.Count;
    }

    private static List<Dictionary<string, object?>> ExportCharacterTemplates(string serverRootPath, ExportCatalogRegistry registry)
    {
        string healCode = registry.ItemCodes.Contains("kim-sang-duoc-tieu") ? "kim-sang-duoc-tieu" : "kim-sang-duoc";
        string manaCode = registry.ItemCodes.Contains("ngung-than-dan-tieu") ? "ngung-than-dan-tieu" : "ngung-than-dan";
        string portalCode = "tho-dia-phu";

        var list = new List<Dictionary<string, object?>>(10);
        int legacyFound = 0;

        for (int seriesIdx = 0; seriesIdx < 5; seriesIdx++)
        {
            for (int genderIdx = 0; genderIdx < 2; genderIdx++)
            {
                int tplId = (seriesIdx * 2) + genderIdx;
                string? iniPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, $@"settings\npc\player\newplayerini{tplId:D2}.ini");
                if (iniPath is null || !File.Exists(iniPath))
                {
                    continue;
                }

                legacyFound++;
                Dictionary<string, Dictionary<string, byte[]>> secs = ParseRawIniSections(File.ReadAllBytes(iniPath));
                secs.TryGetValue("ROLE", out Dictionary<string, byte[]>? roleSec);
                roleSec ??= new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

                int baseStr = Math.Max(0, GetIniInt(roleSec, "ipower", 25));
                int baseDex = Math.Max(0, GetIniInt(roleSec, "iagility", 25));
                int baseVit = Math.Max(0, GetIniInt(roleSec, "iouter", 25));
                int baseEng = Math.Max(0, GetIniInt(roleSec, "iinside", 25));
                int baseLucky = Math.Max(0, GetIniInt(roleSec, "iluck", 0));
                int initLife = Math.Max(1, GetIniInt(roleSec, "imaxlife", 100));
                int initMana = Math.Max(1, GetIniInt(roleSec, "imaxinner", 100));
                int initStamina = Math.Max(1, GetIniInt(roleSec, "imaxstamina", 100));

                var starterSkills = new List<Dictionary<string, object?>>();
                if (secs.TryGetValue("FSKILLS", out Dictionary<string, byte[]>? fskillsSec))
                {
                    int skillCount = GetIniInt(fskillsSec, "COUNT", 0);
                    var seenSkillIds = new HashSet<int>();
                    for (int sIdx = 1; sIdx <= skillCount; sIdx++)
                    {
                        int sid = GetIniInt(fskillsSec, $"S{sIdx}", 0);
                        int lvl = Math.Max(1, GetIniInt(fskillsSec, $"L{sIdx}", 1));
                        if (sid >= 1 && seenSkillIds.Add(sid))
                        {
                            var skillObj = new Dictionary<string, object?>
                            {
                                ["skillId"] = sid,
                            };
                            if (registry.SkillCodeById.TryGetValue(sid, out string? sc))
                            {
                                skillObj["skillCode"] = sc;
                            }

                            skillObj["level"] = lvl;
                            starterSkills.Add(skillObj);
                        }
                    }
                }

                var starterItems = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["itemCode"] = healCode,
                        ["container"] = "immediacy",
                        ["gridX"] = 0,
                        ["gridY"] = 0,
                        ["stackCount"] = 5,
                    },
                    new()
                    {
                        ["itemCode"] = manaCode,
                        ["container"] = "immediacy",
                        ["gridX"] = 1,
                        ["gridY"] = 0,
                        ["stackCount"] = 5,
                    },
                    new()
                    {
                        ["itemCode"] = portalCode,
                        ["container"] = "immediacy",
                        ["gridX"] = 2,
                        ["gridY"] = 0,
                        ["stackCount"] = 1,
                    },
                };

                if (secs.TryGetValue("ITEMS", out Dictionary<string, byte[]>? itemsHeaderSec))
                {
                    int itemCount = GetIniInt(itemsHeaderSec, "COUNT", 0);
                    for (int itemIdx = 1; itemIdx <= itemCount; itemIdx++)
                    {
                        if (!secs.TryGetValue($"ITEM{itemIdx}", out Dictionary<string, byte[]>? itemSec))
                        {
                            continue;
                        }

                        int genre = GetIniInt(itemSec, "iequipclasscode", 0);
                        int detail = GetIniInt(itemSec, "idetailtype", 0);
                        int part = GetIniInt(itemSec, "iparticulartype", 0);
                        int lvl = Math.Max(1, GetIniInt(itemSec, "ilevel", 1));
                        int local = GetIniInt(itemSec, "ilocal", 3);
                        int ix = Math.Max(0, GetIniInt(itemSec, "ix", 0));
                        int iy = Math.Max(0, GetIniInt(itemSec, "iy", 0));

                        string? resolvedItemCode = null;
                        if (registry.ItemCodeByTupleAndLevel.TryGetValue((genre, detail, part, lvl), out string? exactCode))
                        {
                            resolvedItemCode = exactCode;
                        }
                        else if (registry.PrimaryItemCodeByTuple.TryGetValue((genre, detail, part), out string? primaryCode))
                        {
                            resolvedItemCode = primaryCode;
                        }

                        if (!string.IsNullOrEmpty(resolvedItemCode))
                        {
                            string container = local switch
                            {
                                1 => "equip",
                                6 => "immediacy",
                                _ => "equipRoom",
                            };

                            starterItems.Add(new Dictionary<string, object?>
                            {
                                ["itemCode"] = resolvedItemCode,
                                ["container"] = container,
                                ["gridX"] = ix,
                                ["gridY"] = iy,
                                ["stackCount"] = 1,
                            });
                        }
                    }
                }

                list.Add(new Dictionary<string, object?>
                {
                    ["id"] = tplId,
                    ["code"] = $"starter-{SeriesSlugs[seriesIdx]}-{GenderSlugs[genderIdx]}",
                    ["elementSeries"] = seriesIdx,
                    ["gender"] = genderIdx,
                    ["baseStrength"] = baseStr,
                    ["baseDexterity"] = baseDex,
                    ["baseVitality"] = baseVit,
                    ["baseEnergy"] = baseEng,
                    ["baseLucky"] = baseLucky,
                    ["initialLifeMax"] = initLife,
                    ["initialManaMax"] = initMana,
                    ["initialStaminaMax"] = initStamina,
                    ["starterSkills"] = starterSkills,
                    ["starterItems"] = starterItems,
                });
            }
        }

        registry.RecordCount("players.characterTemplates", legacyFound, list.Count);
        return list;
    }

    private static List<Dictionary<string, object?>> ExportSeriesGrowthRules(string serverRootPath, ExportCatalogRegistry registry)
    {
        var list = new List<Dictionary<string, object?>>(5);
        string? filePath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\npc\player\level_add.txt");
        if (filePath is null || !File.Exists(filePath))
        {
            return list;
        }

        var table = KTabFileTable.Load(filePath);
        int rows = Math.Min(5, table.DataRowCount);
        for (int r = 1; r <= rows; r++)
        {
            int series = r - 1;
            string name = table.GetString(r, 0, preferVietnamese: true).Trim();
            var props = new Dictionary<string, object?>
            {
                ["elementSeries"] = series,
                ["code"] = $"growth-{SeriesSlugs[series]}",
                ["name"] = name.Length > 0 ? name : SeriesSlugs[series],
                ["lifePerLevel"] = table.GetInt(r, 1, 10),
                ["manaPerLevel"] = table.GetInt(r, 4, 5),
                ["staminaMalePerLevel"] = table.GetInt(r, 2, 4),
                ["staminaFemalePerLevel"] = table.GetInt(r, 3, 4),
                ["lifePerVitality"] = table.GetInt(r, 5, 5),
                ["manaPerEnergy"] = table.GetInt(r, 7, 3),
                ["staminaPerVitality"] = table.GetInt(r, 6, 2),
                ["leadExpShare"] = table.GetInt(r, 8, 25),
                ["baseFireResist"] = table.GetInt(r, 9, 0),
                ["baseColdResist"] = table.GetInt(r, 10, 0),
                ["basePoisonResist"] = table.GetInt(r, 11, 0),
                ["baseLightResist"] = table.GetInt(r, 12, 0),
                ["basePhysicsResist"] = table.GetInt(r, 13, 0),
                ["staminaMaleBase"] = table.GetInt(r, 14, 180),
                ["staminaFemaleBase"] = table.GetInt(r, 15, 180),
                ["attributePointsPerLevel"] = 5,
                ["skillPointsPerLevel"] = 1,
            };
            list.Add(props);
        }

        registry.RecordCount("players.seriesGrowthRules", rows, list.Count);
        return list;
    }

    private static List<Dictionary<string, object?>> ExportLevelExpTable(string serverRootPath, ExportCatalogRegistry registry)
    {
        var leadExpByLevel = new Dictionary<int, long>();
        string? leadPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\npc\player\level_lead_exp.txt");
        int legacyLeadRows = 0;
        if (leadPath is not null && File.Exists(leadPath))
        {
            var leadTable = KTabFileTable.Load(leadPath);
            for (int r = 1; r <= leadTable.DataRowCount; r++)
            {
                int lvl = leadTable.GetInt(r, 0, 0);
                long exp = Math.Max(0L, leadTable.GetLong(r, 1, 0L));
                if (lvl >= 1)
                {
                    legacyLeadRows++;
                    leadExpByLevel[lvl] = exp;
                }
            }
        }

        var list = new List<Dictionary<string, object?>>();
        string? expPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\npc\player\level_exp.txt");
        int legacyExpRows = 0;
        int exportedLeadRows = 0;
        if (expPath is not null && File.Exists(expPath))
        {
            var expTable = KTabFileTable.Load(expPath);
            for (int r = 1; r <= expTable.DataRowCount; r++)
            {
                int lvl = expTable.GetInt(r, 0, 0);
                if (lvl < 1 || lvl > 255)
                {
                    continue;
                }

                legacyExpRows++;
                long exp = Math.Max(0L, expTable.GetLong(r, 1, 0L));
                var entry = new Dictionary<string, object?>
                {
                    ["level"] = lvl,
                    ["expRequired"] = exp,
                };
                if (leadExpByLevel.TryGetValue(lvl, out long leadExp))
                {
                    entry["leadExpRequired"] = leadExp;
                    exportedLeadRows++;
                }

                list.Add(entry);
            }
        }

        registry.RecordCount("players.levelExpTable", legacyExpRows, list.Count);
        registry.RecordCount("players.levelLeadExpTable", legacyLeadRows, exportedLeadRows);
        return list;
    }

    private static List<Dictionary<string, object?>> ExportFactions(string serverRootPath, ExportCatalogRegistry registry)
    {
        var titlesByFactionId = new Dictionary<int, List<Dictionary<string, object?>>>();
        string? titlePath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\factiontitle.txt");
        int legacyTitleCount = 0;
        int exportedTitleCount = 0;
        if (titlePath is not null && File.Exists(titlePath))
        {
            var titleTable = KTabFileTable.Load(titlePath);
            for (int r = 1; r <= titleTable.DataRowCount; r++)
            {
                int rankId = titleTable.GetInt(r, 0, 0);
                string titleStr = titleTable.GetString(r, 1, preferVietnamese: true).Trim();
                int factionOneBased = titleTable.GetInt(r, 2, 0);
                if (rankId < 1 || factionOneBased < 1 || titleStr.Length == 0)
                {
                    continue;
                }

                legacyTitleCount++;
                int factionId = factionOneBased - 1;
                if (!titlesByFactionId.TryGetValue(factionId, out List<Dictionary<string, object?>>? tList))
                {
                    tList = [];
                    titlesByFactionId[factionId] = tList;
                }

                tList.Add(new Dictionary<string, object?>
                {
                    ["rankId"] = rankId,
                    ["titleName"] = titleStr,
                });
            }
        }

        string? factionPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\faction\门派设定.ini");
        if (factionPath is null)
        {
            string factionDir = Path.Combine(serverRootPath, "settings", "faction");
            if (Directory.Exists(factionDir))
            {
                factionPath = Directory.GetFiles(factionDir, "*.ini")
                    .OrderBy(f => Path.GetFileName(f).Length)
                    .FirstOrDefault();
            }
        }

        var rawFactions = new List<(int Id, string ShowName, Dictionary<string, object?> Props)>();
        if (factionPath is not null && File.Exists(factionPath))
        {
            Dictionary<string, Dictionary<string, byte[]>> secs = ParseRawIniSections(File.ReadAllBytes(factionPath));
            for (int fid = 0; fid <= 15; fid++)
            {
                if (!secs.TryGetValue(fid.ToString(CultureInfo.InvariantCulture), out Dictionary<string, byte[]>? sec))
                {
                    continue;
                }

                string showName = GetIniDecodedString(sec, "ShowName", preferVietnamese: true).Trim();
                string rawName = GetIniAsciiString(sec, "Name");
                if (showName.Length == 0)
                {
                    showName = rawName.Length > 0 ? rawName : $"Faction {fid}";
                }

                string seriesStr = GetIniAsciiString(sec, "Series").ToUpperInvariant();
                int series = seriesStr switch
                {
                    "S_GOLD" => 0,
                    "S_WOOD" => 1,
                    "S_WATER" => 2,
                    "S_FIRE" => 3,
                    "S_EARTH" => 4,
                    _ => 0,
                };

                string campStr = GetIniAsciiString(sec, "Camp").ToUpperInvariant();
                int camp = campStr switch
                {
                    "C_JUSTICE" => 1,
                    "C_EVIL" => 2,
                    "C_BALANCE" => 3,
                    _ => 3,
                };

                int[] allowedGenders = fid switch
                {
                    0 => [0],
                    4 or 5 => [1],
                    _ => [0, 1],
                };

                var props = new Dictionary<string, object?>
                {
                    ["id"] = fid,
                    ["code"] = string.Empty,
                    ["name"] = showName,
                    ["elementSeries"] = series,
                    ["camp"] = camp,
                    ["allowedGenders"] = allowedGenders,
                };

                if (titlesByFactionId.TryGetValue(fid, out List<Dictionary<string, object?>>? titles) && titles.Count > 0)
                {
                    props["titles"] = titles;
                    exportedTitleCount += titles.Count;
                }

                rawFactions.Add((fid, showName, props));
            }
        }

        string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(rawFactions, x => x.ShowName, x => x.Id);
        var result = new List<Dictionary<string, object?>>(rawFactions.Count);
        for (int i = 0; i < rawFactions.Count; i++)
        {
            rawFactions[i].Props["code"] = codes[i];
            result.Add(rawFactions[i].Props);
        }

        registry.RecordCount("players.factions", rawFactions.Count, result.Count);
        registry.RecordCount("players.factionTitles", legacyTitleCount, exportedTitleCount);
        return result;
    }

    private static List<Dictionary<string, object?>> ExportStarterVillages(string serverRootPath, ExportCatalogRegistry registry)
    {
        string? nativePath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\nativeplacelist.ini");
        string? revivalIdPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\npc\player\revivalid.ini");
        string? revivePosPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\revivepos.ini");

        if (nativePath is null || !File.Exists(nativePath))
        {
            return [];
        }

        Dictionary<string, Dictionary<string, byte[]>> nativeSecs = ParseRawIniSections(File.ReadAllBytes(nativePath));
        Dictionary<string, Dictionary<string, byte[]>> revIdSecs = revivalIdPath is not null && File.Exists(revivalIdPath)
            ? ParseRawIniSections(File.ReadAllBytes(revivalIdPath))
            : new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Dictionary<string, byte[]>> revPosSecs = revivePosPath is not null && File.Exists(revivePosPath)
            ? ParseRawIniSections(File.ReadAllBytes(revivePosPath))
            : new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.OrdinalIgnoreCase);

        int count = nativeSecs.TryGetValue("List", out Dictionary<string, byte[]>? listSec)
            ? GetIniInt(listSec, "Count", 8)
            : 8;

        var rawVillages = new List<(int Id, string Name, Dictionary<string, object?> Props)>();
        for (int idx = 0; idx < count; idx++)
        {
            if (!nativeSecs.TryGetValue(idx.ToString(CultureInfo.InvariantCulture), out Dictionary<string, byte[]>? sec))
            {
                continue;
            }

            int mapId = GetIniInt(sec, "Id", 0);
            if (mapId < 1)
            {
                continue;
            }

            string name = GetIniDecodedString(sec, "Name", preferVietnamese: true).Trim();
            if (name.Length == 0)
            {
                name = $"Village {mapId}";
            }

            string desc = GetIniDecodedString(sec, "Desc", preferVietnamese: true).Trim();

            int defaultRevId = 0;
            if (revIdSecs.TryGetValue(mapId.ToString(CultureInfo.InvariantCulture), out Dictionary<string, byte[]>? ridSec))
            {
                defaultRevId = Math.Max(0, GetIniInt(ridSec, "RevivalId00", 0));
            }

            var revPoints = new List<Dictionary<string, object?>>();
            var seenRevPointIds = new HashSet<int>();
            if (revPosSecs.TryGetValue(mapId.ToString(CultureInfo.InvariantCulture), out Dictionary<string, byte[]>? posSec))
            {
                foreach (var (k, vBytes) in posSec.OrderBy(kv => int.TryParse(kv.Key, out int pk) ? pk : int.MaxValue))
                {
                    if (!int.TryParse(k, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rpId) || rpId < 0)
                    {
                        continue;
                    }

                    string coords = Encoding.Latin1.GetString(vBytes);
                    string[] parts = coords.Split(',', StringSplitOptions.TrimEntries);
                    if (parts.Length >= 2 &&
                        int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mpsX) &&
                        int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mpsY) &&
                        mpsX >= 0 && mpsY >= 0 &&
                        seenRevPointIds.Add(rpId))
                    {
                        revPoints.Add(new Dictionary<string, object?>
                        {
                            ["revivalPointId"] = rpId,
                            ["mpsX"] = mpsX,
                            ["mpsY"] = mpsY,
                        });
                    }
                }
            }

            if (revPoints.Count == 0)
            {
                revPoints.Add(new Dictionary<string, object?>
                {
                    ["revivalPointId"] = defaultRevId,
                    ["mpsX"] = 52032,
                    ["mpsY"] = 101696,
                });
            }
            else if (!seenRevPointIds.Contains(defaultRevId))
            {
                defaultRevId = (int)revPoints[0]["revivalPointId"]!;
            }

            var props = new Dictionary<string, object?>
            {
                ["id"] = idx,
                ["code"] = string.Empty,
                ["name"] = name,
                ["mapId"] = mapId,
                ["defaultRevivalPointId"] = defaultRevId,
                ["revivalPoints"] = revPoints,
            };
            if (desc.Length > 0)
            {
                props["description"] = desc;
            }

            rawVillages.Add((idx, name, props));
        }

        string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(rawVillages, x => x.Name, x => x.Id);
        var result = new List<Dictionary<string, object?>>(rawVillages.Count);
        for (int i = 0; i < rawVillages.Count; i++)
        {
            rawVillages[i].Props["code"] = codes[i];
            result.Add(rawVillages[i].Props);
        }

        registry.RecordCount("players.starterVillages", rawVillages.Count, result.Count);
        return result;
    }

    private static Dictionary<string, object?> ExportPkRules(string serverRootPath, ExportCatalogRegistry registry)
    {
        string? pkPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\npc\player\pkpunish.txt");
        var penalties = new List<Dictionary<string, object?>>();
        int cooldownFrames = 3240;
        int maxExpLoss = 500000;
        int maxPkValue = 10;
        int legacyRows = 0;

        if (pkPath is not null && File.Exists(pkPath))
        {
            var table = KTabFileTable.Load(pkPath);
            if (table.DataRowCount >= 1)
            {
                cooldownFrames = Math.Max(0, table.GetInt(1, 6, 3240));
                maxExpLoss = Math.Max(0, table.GetInt(1, 7, 500000));
            }

            for (int r = 1; r <= table.DataRowCount; r++)
            {
                int pkVal = table.GetInt(r, 0, -1);
                if (pkVal < 0 || pkVal > 10)
                {
                    continue;
                }

                legacyRows++;
                if (pkVal > maxPkValue)
                {
                    maxPkValue = pkVal;
                }

                int rowMaxExp = table.GetInt(r, 7, maxExpLoss);
                penalties.Add(new Dictionary<string, object?>
                {
                    ["pkValue"] = pkVal,
                    ["expLossPerMil"] = Math.Max(0, table.GetInt(r, 1, 0)),
                    ["maxExpLoss"] = Math.Max(0, rowMaxExp),
                    ["moneyDropPerMil"] = Math.Max(0, table.GetInt(r, 2, 0)),
                    ["bagItemDropPerMil"] = Math.Max(0, table.GetInt(r, 3, 0)),
                    ["equipDropPerMil"] = Math.Max(0, table.GetInt(r, 4, 0)),
                    ["durabilityLoss"] = Math.Max(0, table.GetInt(r, 8, 0)),
                });
            }
        }

        registry.RecordCount("players.pkRules", legacyRows, penalties.Count);
        return new Dictionary<string, object?>
        {
            ["switchToPeaceCooldownFrames"] = cooldownFrames,
            ["maxPkValue"] = Math.Max(1, maxPkValue),
            ["penaltiesByPkValue"] = penalties,
        };
    }

    private static List<Dictionary<string, object?>> ExportMeridians(string serverRootPath, ExportCatalogRegistry registry)
    {
        var acupointsByMeridian = new Dictionary<int, List<Dictionary<string, object?>>>();
        string? levelPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\meridian\meridian_level.txt");
        int legacyAcupoints = 0;
        int exportedAcupoints = 0;

        if (levelPath is not null && File.Exists(levelPath))
        {
            var lvlTable = KTabFileTable.Load(levelPath);
            for (int r = 1; r <= lvlTable.DataRowCount; r++)
            {
                string acuName = lvlTable.GetString(r, 0, preferVietnamese: true).Trim();
                int meridianId = lvlTable.GetInt(r, 1, 0);
                int lvl = lvlTable.GetInt(r, 2, 0);
                if (meridianId < 1 || lvl < 1)
                {
                    continue;
                }

                legacyAcupoints++;
                if (acuName.Length == 0)
                {
                    acuName = $"Huyệt vị {lvl}";
                }

                int fallback = Math.Max(0, lvlTable.GetInt(r, 3, 0));
                int rate = Math.Clamp(lvlTable.GetInt(r, 4, 10000), 0, 10000);
                string desc = lvlTable.GetString(r, 5, preferVietnamese: true).Trim();

                var affixIds = new List<int>(3);
                for (int c = 6; c <= 8; c++)
                {
                    int aid = lvlTable.GetInt(r, c, 0);
                    if (aid > 0)
                    {
                        affixIds.Add(aid);
                    }
                }

                int zhenYuan = Math.Max(0, lvlTable.GetInt(r, 9, 0));
                int huMaiDan = Math.Max(0, lvlTable.GetInt(r, 10, 0));

                var acuObj = new Dictionary<string, object?>
                {
                    ["level"] = lvl,
                    ["name"] = acuName,
                    ["successRatePerTenThousand"] = rate,
                    ["fallbackLevel"] = fallback,
                    ["zhenYuanCost"] = zhenYuan,
                    ["huMaiDanCost"] = huMaiDan,
                    ["magicAffixIds"] = affixIds,
                };
                if (desc.Length > 0)
                {
                    acuObj["description"] = desc;
                }

                if (!acupointsByMeridian.TryGetValue(meridianId, out List<Dictionary<string, object?>>? list))
                {
                    list = [];
                    acupointsByMeridian[meridianId] = list;
                }

                list.Add(acuObj);
            }
        }

        var rawMeridians = new List<(int Id, string Name, Dictionary<string, object?> Props)>();
        string? merPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\meridian\meridian.txt");
        if (merPath is not null && File.Exists(merPath))
        {
            var merTable = KTabFileTable.Load(merPath);
            for (int r = 1; r <= merTable.DataRowCount; r++)
            {
                int id = merTable.GetInt(r, 0, 0);
                if (id < 1 || id > 16)
                {
                    continue;
                }

                string name = merTable.GetString(r, 1, preferVietnamese: true).Trim();
                if (name.Length == 0)
                {
                    name = $"Kinh mạch {id}";
                }

                int taskVarId = Math.Max(1, merTable.GetInt(r, 2, 4000 + id));
                List<Dictionary<string, object?>> acus = acupointsByMeridian.GetValueOrDefault(id) ?? [];
                exportedAcupoints += acus.Count;

                rawMeridians.Add((id, name, new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["code"] = string.Empty,
                    ["name"] = name,
                    ["taskVariableId"] = taskVarId,
                    ["acupoints"] = acus,
                }));
            }
        }

        string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(rawMeridians, x => x.Name, x => x.Id);
        var result = new List<Dictionary<string, object?>>(rawMeridians.Count);
        for (int i = 0; i < rawMeridians.Count; i++)
        {
            rawMeridians[i].Props["code"] = codes[i];
            result.Add(rawMeridians[i].Props);
        }

        registry.RecordCount("players.meridians", rawMeridians.Count, result.Count);
        registry.RecordCount("players.meridianAcupoints", legacyAcupoints, exportedAcupoints);
        return result;
    }

    private static Dictionary<string, object?> ExportStaminaRules(string serverRootPath)
    {
        int normalAdd = 1;
        int exerciseSub = 1;
        int fightSub = 1;
        int killSub = 18;
        int sitAdd = 10;

        string? stamPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\npc\player\stamina.ini");
        if (stamPath is not null && File.Exists(stamPath))
        {
            Dictionary<string, Dictionary<string, byte[]>> secs = ParseRawIniSections(File.ReadAllBytes(stamPath));
            if (secs.TryGetValue("stamina", out Dictionary<string, byte[]>? sec))
            {
                normalAdd = Math.Max(0, GetIniInt(sec, "NormalAdd", 1));
                exerciseSub = Math.Max(0, GetIniInt(sec, "ExerciseRunSub", 1));
                fightSub = Math.Max(0, GetIniInt(sec, "FightRunSub", 1));
                killSub = Math.Max(0, GetIniInt(sec, "KillRunSub", 18));
                sitAdd = Math.Max(0, GetIniInt(sec, "SitAdd", 10));
            }
        }

        return new Dictionary<string, object?>
        {
            ["normalRegenPerTick"] = normalAdd,
            ["exerciseRunDrainPerTick"] = exerciseSub,
            ["fightRunDrainPerTick"] = fightSub,
            ["killRunDrainPerTick"] = killSub,
            ["sitRegenPerMil"] = sitAdd,
        };
    }

    private static List<Dictionary<string, object?>> ExportTeamResistEffects(string serverRootPath, ExportCatalogRegistry registry)
    {
        var result = new List<Dictionary<string, object?>>(5);
        string? teamPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\npc\player\team_resist_effect.ini");
        int legacySections = 0;

        if (teamPath is not null && File.Exists(teamPath))
        {
            Dictionary<string, Dictionary<string, byte[]>> secs = ParseRawIniSections(File.ReadAllBytes(teamPath));
            for (int s = 0; s < 5; s++)
            {
                if (!secs.TryGetValue(s.ToString(CultureInfo.InvariantCulture), out Dictionary<string, byte[]>? sec))
                {
                    continue;
                }

                legacySections++;
                int[] bonuses = new int[5];
                for (int t = 0; t < 5; t++)
                {
                    bonuses[t] = Math.Max(0, GetIniInt(sec, t.ToString(CultureInfo.InvariantCulture), 0));
                }

                result.Add(new Dictionary<string, object?>
                {
                    ["memberSeries"] = s,
                    ["grantedResistType"] = TeamGrantedResistTypes[s],
                    ["bonusByTeammateSeries"] = bonuses,
                });
            }
        }

        registry.RecordCount("players.teamResistEffects", legacySections, result.Count);
        return result;
    }

    private static Dictionary<string, object?> ExportTongConfig(string serverRootPath, ExportCatalogRegistry registry)
    {
        var levels = new List<Dictionary<string, object?>>();
        string? lvlPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\tong\tong_level_data.txt");
        int legacyLvlCount = 0;
        if (lvlPath is not null && File.Exists(lvlPath))
        {
            var lvlTable = KTabFileTable.Load(lvlPath);
            for (int r = 1; r <= lvlTable.DataRowCount; r++)
            {
                int level = lvlTable.GetInt(r, 0, -1);
                if (level < 0)
                {
                    continue;
                }

                legacyLvlCount++;
                levels.Add(new Dictionary<string, object?>
                {
                    ["level"] = level,
                    ["workshopUpperLevel"] = Math.Max(1, lvlTable.GetInt(r, 1, 1)),
                    ["maxWorkshopCount"] = Math.Max(0, lvlTable.GetInt(r, 2, 0)),
                    ["weeklyBuildUpper"] = Math.Max(0, lvlTable.GetInt(r, 3, 0)),
                    ["upgradeFund"] = Math.Max(0, lvlTable.GetInt(r, 4, 0)),
                    ["upgradeWorkshopCount"] = Math.Max(0, lvlTable.GetInt(r, 5, 0)),
                    ["upgradeHiWorkshopCount"] = Math.Max(0, lvlTable.GetInt(r, 6, 0)),
                    ["upgradeHiWorkshopLevel"] = Math.Max(0, lvlTable.GetInt(r, 7, 0)),
                });
            }
        }

        var rawWorkshops = new List<(int Id, string Name, Dictionary<string, object?> Props)>();
        string? wsPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\tong\workshop\workshops.txt");
        if (wsPath is not null && File.Exists(wsPath))
        {
            var wsTable = KTabFileTable.Load(wsPath);
            for (int r = 1; r <= wsTable.DataRowCount; r++)
            {
                int id = wsTable.GetInt(r, 0, 0);
                if (id < 1)
                {
                    continue;
                }

                string name = wsTable.GetString(r, 1, preferVietnamese: true).Trim();
                if (name.Length == 0)
                {
                    name = $"Tác phường {id}";
                }

                string desc = wsTable.GetString(r, 2, preferVietnamese: true).Trim();
                double coeff = Math.Max(0.0, wsTable.GetDouble(r, 3, 1.0));
                string scriptPath = LegacyPathResolver.NormalizeEnginePath(wsTable.GetRawCell(r, 7));

                var props = new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["code"] = string.Empty,
                    ["name"] = name,
                    ["costCoefficient"] = coeff,
                };
                if (desc.Length > 0)
                {
                    props["description"] = desc;
                }

                if (scriptPath.Length > 0)
                {
                    props["scriptPath"] = scriptPath;
                }

                rawWorkshops.Add((id, name, props));
            }
        }

        string[] wsCodes = HybridLegacyTextDecoder.AssignSymmetricCodes(rawWorkshops, x => x.Name, x => x.Id);
        var workshops = new List<Dictionary<string, object?>>(rawWorkshops.Count);
        for (int i = 0; i < rawWorkshops.Count; i++)
        {
            rawWorkshops[i].Props["code"] = wsCodes[i];
            workshops.Add(rawWorkshops[i].Props);
        }

        var rawStunts = new List<(int Id, string Name, Dictionary<string, object?> Props)>();
        string? stuntPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\tong\tongstunt_setting.txt");
        if (stuntPath is not null && File.Exists(stuntPath))
        {
            var stuntTable = KTabFileTable.Load(stuntPath);
            for (int r = 1; r <= stuntTable.DataRowCount; r++)
            {
                // Row 1 in tongstunt_setting.txt is a Vietnamese secondary header ("ID kỹ năng") -> GetInt(r, 0, 0) == 0
                int id = stuntTable.GetInt(r, 0, 0);
                if (id < 1)
                {
                    continue;
                }

                string name = stuntTable.GetString(r, 1, preferVietnamese: true).Trim();
                if (name.Length == 0)
                {
                    name = $"Bang kỹ {id}";
                }

                int maxMembers = Math.Max(1, stuntTable.GetInt(r, 2, 1));
                int maxPerMember = Math.Max(1, stuntTable.GetInt(r, 3, 1));
                int cycleWeeks = Math.Max(1, stuntTable.GetInt(r, 4, 4));
                int weeklyCost = Math.Max(0, stuntTable.GetInt(r, 5, 0));
                int rightLimitRaw = stuntTable.GetInt(r, 6, 2);
                int rightLimit = rightLimitRaw is 1 or 2 ? rightLimitRaw : 2;
                int skillId = Math.Max(1, stuntTable.GetInt(r, 7, 1));
                string desc = stuntTable.GetString(r, 8, preferVietnamese: true).Trim();

                var props = new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["code"] = string.Empty,
                    ["name"] = name,
                    ["skillId"] = skillId,
                    ["maxMembers"] = maxMembers,
                    ["maxCountPerMember"] = maxPerMember,
                    ["cycleWeeks"] = cycleWeeks,
                    ["weeklyMaintenanceCost"] = weeklyCost,
                    ["rightLimit"] = rightLimit,
                };
                if (desc.Length > 0)
                {
                    props["description"] = desc;
                }

                rawStunts.Add((id, name, props));
            }
        }

        string[] stuntCodes = HybridLegacyTextDecoder.AssignSymmetricCodes(rawStunts, x => x.Name, x => x.Id);
        var stunts = new List<Dictionary<string, object?>>(rawStunts.Count);
        for (int i = 0; i < rawStunts.Count; i++)
        {
            rawStunts[i].Props["code"] = stuntCodes[i];
            stunts.Add(rawStunts[i].Props);
        }

        registry.RecordCount("players.tongLevels", legacyLvlCount, levels.Count);
        registry.RecordCount("players.tongWorkshops", rawWorkshops.Count, workshops.Count);
        registry.RecordCount("players.tongStunts", rawStunts.Count, stunts.Count);

        return new Dictionary<string, object?>
        {
            ["levels"] = levels,
            ["workshops"] = workshops,
            ["stunts"] = stunts,
        };
    }

    private static Dictionary<string, Dictionary<string, byte[]>> ParseRawIniSections(ReadOnlySpan<byte> rawBytes)
    {
        var result = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, byte[]>? currentSection = null;

        int lineStart = 0;
        while (lineStart <= rawBytes.Length)
        {
            int lineEnd = lineStart;
            while (lineEnd < rawBytes.Length && rawBytes[lineEnd] != (byte)'\n' && rawBytes[lineEnd] != (byte)'\r')
            {
                lineEnd++;
            }

            ReadOnlySpan<byte> line = TrimAsciiWhitespace(rawBytes[lineStart..lineEnd]);
            if (!line.IsEmpty && line[0] != (byte)';' && !(line.Length >= 2 && line[0] == (byte)'/' && line[1] == (byte)'/'))
            {
                if (line[0] == (byte)'[' && line[^1] == (byte)']' && line.Length > 2)
                {
                    string secName = Encoding.Latin1.GetString(line[1..^1]).Trim();
                    if (!result.TryGetValue(secName, out currentSection))
                    {
                        currentSection = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                        result[secName] = currentSection;
                    }
                }
                else if (currentSection is not null)
                {
                    int eq = line.IndexOf((byte)'=');
                    if (eq > 0)
                    {
                        string key = Encoding.Latin1.GetString(TrimAsciiWhitespace(line[..eq]));
                        byte[] val = TrimAsciiWhitespace(line[(eq + 1)..]).ToArray();
                        currentSection[key] = val;
                    }
                }
            }

            if (lineEnd >= rawBytes.Length)
            {
                break;
            }

            if (lineEnd + 1 < rawBytes.Length && rawBytes[lineEnd] == (byte)'\r' && rawBytes[lineEnd + 1] == (byte)'\n')
            {
                lineStart = lineEnd + 2;
            }
            else
            {
                lineStart = lineEnd + 1;
            }
        }

        return result;
    }

    private static ReadOnlySpan<byte> TrimAsciiWhitespace(ReadOnlySpan<byte> span)
    {
        int s = 0;
        int e = span.Length - 1;
        while (s <= e && span[s] is (byte)' ' or (byte)'\t')
        {
            s++;
        }

        while (e >= s && span[e] is (byte)' ' or (byte)'\t' or 0)
        {
            e--;
        }

        return s <= e ? span[s..(e + 1)] : ReadOnlySpan<byte>.Empty;
    }

    private static int GetIniInt(Dictionary<string, byte[]> sec, string key, int defaultValue)
    {
        if (!sec.TryGetValue(key, out byte[]? raw) || raw.Length == 0)
        {
            return defaultValue;
        }

        string s = Encoding.Latin1.GetString(raw).Trim();
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : defaultValue;
    }

    private static string GetIniAsciiString(Dictionary<string, byte[]> sec, string key) =>
        sec.TryGetValue(key, out byte[]? raw) ? Encoding.Latin1.GetString(raw).Trim() : string.Empty;

    private static string GetIniDecodedString(Dictionary<string, byte[]> sec, string key, bool preferVietnamese) =>
        sec.TryGetValue(key, out byte[]? raw) ? HybridLegacyTextDecoder.DecodeSmart(raw, preferVietnamese) : string.Empty;
}
