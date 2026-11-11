using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JXServer.DataExporter.Legacy;

namespace JXServer.DataExporter.Exporters;

/// <summary>
/// Exports <c>settings/missles.txt</c>, <c>settings/skills.txt</c> (+ <c>script/skill/**/*.lua</c> <c>GetSkillLevelData</c> curves),
/// <c>settings/clientweaponskill.txt</c>, and <c>settings/attribconstdata.ini</c> into canonical UTF-8 JSON (`JXData Standard v1`).
/// </summary>
public sealed class SkillAndMissileDataExporter
{
    public (int SkillCount, int MissileCount) ExportSkillsAndMissiles(
        string serverRootPath,
        string outputDataDirectory,
        ExportCatalogRegistry? registry = null)
    {
        registry ??= new ExportCatalogRegistry();
        MagicAttributeRegistry magicReg = MagicAttributeRegistry.Load(serverRootPath);

        string skillsOutDir = Path.Combine(outputDataDirectory, "skills");
        Directory.CreateDirectory(skillsOutDir);

        string obsoleteSingle = Path.Combine(skillsOutDir, "skills.skills.json");
        if (File.Exists(obsoleteSingle))
        {
            File.Delete(obsoleteSingle);
        }

        int missileCount = ExportMissiles(serverRootPath, skillsOutDir, registry);
        int skillCount = ExportSkillsWeaponMappingsAndConstRules(serverRootPath, skillsOutDir, magicReg, registry);

        return (skillCount, missileCount);
    }

    private static int ExportMissiles(string serverRootPath, string skillsOutDir, ExportCatalogRegistry registry)
    {
        var entries = new List<(int Id, string Name, Dictionary<string, object?> Props)>(600);
        var seenIds = new HashSet<int>();
        int legacyValidRows = 0;

        string? misslesPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\missles.txt");
        if (misslesPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(misslesPath);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                int id = table.GetInt(r, 0, 0, inheritRow2Default: false);
                if (id < 1 || !seenIds.Add(id))
                {
                    continue;
                }

                legacyValidRows++;
                string rawName = table.GetString(r, 1, preferVietnamese: true, inheritRow2Default: false);
                string name = string.IsNullOrWhiteSpace(rawName) ? $"Phi đạn {id}" : StripQuotes(rawName);

                int moveKindVal = table.GetInt(r, 2, 1, inheritRow2Default: true);
                int followKindVal = table.GetInt(r, 3, 0, inheritRow2Default: true);
                bool colFollow = table.GetInt(r, 4, 0, inheritRow2Default: true) != 0;
                int heightMps = Math.Max(0, table.GetInt(r, 5, 0, inheritRow2Default: true));
                int collideCells = Math.Max(0, table.GetInt(r, 6, 1, inheritRow2Default: true));
                bool isRangeDmg = table.GetInt(r, 7, 0, inheritRow2Default: true) != 0;
                int dmgCells = Math.Max(0, table.GetInt(r, 8, 1, inheritRow2Default: true));
                int dmgInterval = Math.Max(0, table.GetInt(r, 9, 0, inheritRow2Default: true));
                int lifeFrames = Math.Max(0, table.GetInt(r, 10, 16, inheritRow2Default: true));
                int speed = Math.Max(0, table.GetInt(r, 11, 16, inheritRow2Default: true));
                int zSpeed = table.GetInt(r, 12, 0, inheritRow2Default: true);
                int zAcc = table.GetInt(r, 13, 0, inheritRow2Default: true);
                int responseSkillId = Math.Max(0, table.GetInt(r, 18, 0, inheritRow2Default: true));
                bool canDestroy = table.GetInt(r, 19, 0, inheritRow2Default: true) != 0;
                bool colVanish = table.GetInt(r, 20, 1, inheritRow2Default: true) != 0;
                bool canSlow = table.GetInt(r, 21, 0, inheritRow2Default: true) != 0;
                bool canColFriend = table.GetInt(r, 22, 0, inheritRow2Default: true) != 0;
                bool autoExplode = table.GetInt(r, 23, 0, inheritRow2Default: true) != 0;
                int p1 = table.GetInt(r, 24, 0, inheritRow2Default: true);
                int p2 = table.GetInt(r, 25, 0, inheritRow2Default: true);
                int p3 = table.GetInt(r, 26, 0, inheritRow2Default: true);

                int hitRadiusMps = Math.Max(32, collideCells * 32);

                var props = new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["code"] = string.Empty,
                    ["name"] = name,
                    ["moveKind"] = MapMissileMoveKind(moveKindVal),
                    ["followKind"] = followKindVal switch { 1 => "npc", 2 => "missile", _ => "none" },
                    ["collideFollowTarget"] = colFollow,
                    ["speedMpsPerFrame"] = speed,
                    ["heightMps"] = heightMps,
                    ["zSpeedMpsPerFrame"] = zSpeed,
                    ["zAccelMpsPerFrame2"] = zAcc,
                    ["lifeFrames"] = lifeFrames,
                    ["hitRadiusMps"] = hitRadiusMps,
                    ["collideRadiusCells"] = collideCells,
                    ["damageRadiusCells"] = dmgCells,
                    ["isRangeDamage"] = isRangeDmg,
                    ["damageIntervalFrames"] = dmgInterval,
                    ["pierceCount"] = colVanish ? 1 : 99,
                    ["collideVanish"] = colVanish,
                    ["canCollideFriend"] = canColFriend,
                    ["canDestroy"] = canDestroy,
                    ["canSlow"] = canSlow,
                    ["autoExplode"] = autoExplode,
                    ["mustHit"] = false,
                    ["responseSkillId"] = responseSkillId,
                    ["subMissileId"] = 0,
                    ["param1"] = p1,
                    ["param2"] = p2,
                    ["param3"] = p3,
                };

                entries.Add((id, name, props));
            }
        }

        string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(entries, e => e.Name, e => e.Id);
        var docs = new List<Dictionary<string, object?>>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].Props["code"] = codes[i];
            registry.MissileCodeById[entries[i].Id] = codes[i];
            docs.Add(entries[i].Props);
        }

        LegacyPathResolver.WriteCanonicalJson(
            Path.Combine(skillsOutDir, "missiles.skills.json"),
            new Dictionary<string, object?>
            {
                ["$schema"] = "../../schemas/skill-catalog.schema.json",
                ["schemaVersion"] = "1.0.0",
                ["missiles"] = docs,
            });

        registry.RecordCount("skills.missiles", legacyValidRows, docs.Count);
        return docs.Count;
    }

    private static int ExportSkillsWeaponMappingsAndConstRules(
        string serverRootPath,
        string skillsOutDir,
        MagicAttributeRegistry magicReg,
        ExportCatalogRegistry registry)
    {
        var entries = new List<(int Id, string Name, Dictionary<string, object?> Props)>(1500);
        var seenIds = new HashSet<int>();
        var sharedTokenCache = new Dictionary<string, List<Lua4Token>>(StringComparer.OrdinalIgnoreCase);
        var interpreterByScriptPath = new Dictionary<string, Lua4Interpreter?>(StringComparer.OrdinalIgnoreCase);
        int legacyValidRows = 0;

        string? skillsPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\skills.txt");
        if (skillsPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(skillsPath);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                int id = table.GetInt(r, 2, 0, inheritRow2Default: false);
                if (id < 1)
                {
                    continue;
                }

                // Filter out column-shifted Phong Than leftover rows where SkillStyle [4] holds a sprite path (\spr\...)
                ReadOnlySpan<byte> rawStyle = table.GetRawCell(r, 4, inheritRow2Default: false);
                if (!rawStyle.IsEmpty)
                {
                    string rawStyleStr = Encoding.Latin1.GetString(rawStyle).Trim();
                    if (rawStyleStr.Length > 0 && !int.TryParse(rawStyleStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        continue;
                    }
                }

                if (!seenIds.Add(id))
                {
                    continue;
                }

                legacyValidRows++;
                string rawName = table.GetString(r, 0, preferVietnamese: true, inheritRow2Default: false);
                string name = string.IsNullOrWhiteSpace(rawName) ? $"Kỹ năng {id}" : StripQuotes(rawName);
                string desc = StripQuotes(table.GetString(r, 112, preferVietnamese: true, inheritRow2Default: false));

                int skillStyle = table.GetInt(r, 4, 0, inheritRow2Default: true);
                int stateSpecId = Math.Max(0, table.GetInt(r, 9, 0, inheritRow2Default: true));
                int statePrio = Math.Max(0, table.GetInt(r, 10, 0, inheritRow2Default: true));
                bool isAura = table.GetInt(r, 11, 0, inheritRow2Default: true) != 0;
                int lrSkill = table.GetInt(r, 12, 0, inheritRow2Default: true);
                int castRangeMps = Math.Max(0, table.GetInt(r, 14, 0, inheritRow2Default: true));
                int mslsGen = table.GetInt(r, 16, 0, inheritRow2Default: true);
                int mslsGenDelay = Math.Max(0, table.GetInt(r, 17, 0, inheritRow2Default: true));
                int charClass = Math.Max(-1, table.GetInt(r, 18, 0, inheritRow2Default: true));
                int misslesForm = table.GetInt(r, 19, 1, inheritRow2Default: true);
                int childSkillId = Math.Max(0, table.GetInt(r, 20, 0, inheritRow2Default: true));
                int childSkillLvl = Math.Max(-1, table.GetInt(r, 21, -1, inheritRow2Default: true));
                int childSkillNum = Math.Max(1, table.GetInt(r, 22, 1, inheritRow2Default: true));
                bool isBaseSkill = table.GetInt(r, 23, 1, inheritRow2Default: true) != 0;
                int charAnimId = Math.Max(0, table.GetInt(r, 24, 9, inheritRow2Default: true));
                int eventSkillLvl = Math.Max(-1, table.GetInt(r, 25, 0, inheritRow2Default: true));
                bool isMelee = table.GetInt(r, 26, 0, inheritRow2Default: true) != 0;
                int waitFrames = Math.Max(0, table.GetInt(r, 27, 0, inheritRow2Default: true));
                bool clientSend = table.GetInt(r, 28, 0, inheritRow2Default: true) != 0;
                int costTypeVal = table.GetInt(r, 29, 0, inheritRow2Default: true);
                int costBaseVal = Math.Max(0, table.GetInt(r, 30, 0, inheritRow2Default: true));
                int timePerCast = Math.Max(0, table.GetInt(r, 31, 0, inheritRow2Default: true));
                int timePerCastHorse = Math.Max(0, table.GetInt(r, 32, 0, inheritRow2Default: true));
                bool isPhysical = table.GetInt(r, 33, 0, inheritRow2Default: true) != 0;

                bool targetOnly = table.GetInt(r, 34, 0, inheritRow2Default: true) != 0;
                bool targetEnemy = table.GetInt(r, 35, 0, inheritRow2Default: true) != 0;
                bool targetAlly = table.GetInt(r, 36, 0, inheritRow2Default: true) != 0;
                bool targetSelf = table.GetInt(r, 37, 0, inheritRow2Default: true) != 0;
                bool targetOther = table.GetInt(r, 38, 0, inheritRow2Default: true) != 0;
                bool targetObj = table.GetInt(r, 39, 0, inheritRow2Default: true) != 0;
                bool targetNoNpc = table.GetInt(r, 40, 0, inheritRow2Default: true) != 0;
                bool byMissile = table.GetInt(r, 41, 0, inheritRow2Default: true) != 0;
                bool useAr = table.GetInt(r, 42, 0, inheritRow2Default: true) != 0;

                int startEvent = table.GetInt(r, 43, 0, inheritRow2Default: true);
                int startSkillId = Math.Max(0, table.GetInt(r, 44, 0, inheritRow2Default: true));
                int flyEvent = table.GetInt(r, 45, 0, inheritRow2Default: true);
                int flySkillId = Math.Max(0, table.GetInt(r, 46, 0, inheritRow2Default: true));
                int flyEventTime = Math.Max(0, table.GetInt(r, 47, 0, inheritRow2Default: true));
                int collideEvent = table.GetInt(r, 48, 0, inheritRow2Default: true);
                int collideSkillId = Math.Max(0, table.GetInt(r, 49, 0, inheritRow2Default: true));
                int vanishEvent = table.GetInt(r, 50, 0, inheritRow2Default: true);
                int vanishSkillId = Math.Max(0, table.GetInt(r, 51, 0, inheritRow2Default: true));

                int reqLevel = Math.Max(-2, table.GetInt(r, 52, 0, inheritRow2Default: true));
                int maxLevel = Math.Clamp(table.GetInt(r, 53, 20, inheritRow2Default: true), 0, 64);
                int eqLimit = Math.Max(-2, table.GetInt(r, 54, -2, inheritRow2Default: true));
                int rawHorseLimit = table.GetInt(r, 55, 0, inheritRow2Default: true);
                int horseLimit = rawHorseLimit is 0 or 1 or 2 ? rawHorseLimit : 0;
                bool doHurt = table.GetInt(r, 56, 0, inheritRow2Default: true) != 0;
                bool weaponSkill = table.GetInt(r, 57, 0, inheritRow2Default: true) != 0;
                int param1 = table.GetInt(r, 58, 0, inheritRow2Default: true);
                int param2 = table.GetInt(r, 60, 0, inheritRow2Default: true);
                int rawStopMove = table.GetInt(r, 62, 0, inheritRow2Default: true);
                int stopWhenMove = rawStopMove is 0 or 1 or 2 ? rawStopMove : 0;
                bool heelAtParent = table.GetInt(r, 63, 0, inheritRow2Default: true) != 0;
                int relPosType = table.GetInt(r, 64, 0, inheritRow2Default: true);
                bool peaceCanUse = table.GetInt(r, 65, 0, inheritRow2Default: true) != 0;
                bool isExpSkill = table.GetInt(r, 67, 0, inheritRow2Default: true) != 0;
                int series = Math.Clamp(table.GetInt(r, 68, -1, inheritRow2Default: true), -1, 4);

                string style = MapSkillStyle(skillStyle, isMelee, isAura, targetEnemy);
                bool isOffensive = targetEnemy || (skillStyle is 0 or 1 or 6 or 8 && !targetAlly && !targetSelf);

                string costTypeStr = costTypeVal switch
                {
                    1 => "life",
                    2 => "stamina",
                    3 => "money",
                    _ => "mana",
                };

                var costScaling = new Dictionary<string, object?>
                {
                    ["costType"] = costTypeStr,
                };
                if (costTypeStr == "mana")
                {
                    costScaling["baseMana"] = costBaseVal;
                }
                else if (costTypeStr == "stamina")
                {
                    costScaling["baseStamina"] = costBaseVal;
                }
                else if (costTypeStr == "life")
                {
                    costScaling["baseLife"] = costBaseVal;
                }

                var effectScaling = new List<Dictionary<string, object?>>();

                // Evaluate Lua LvlSetScript -> GetSkillLevelData(settingName, dataName, level)
                ReadOnlySpan<byte> scriptRaw = table.GetRawCell(r, 70, inheritRow2Default: true);
                Lua4Interpreter? interp = ResolveSkillInterpreter(serverRootPath, scriptRaw, sharedTokenCache, interpreterByScriptPath);
                if (interp is not null)
                {
                    int evalLevels = Math.Clamp(Math.Max(maxLevel > 0 ? maxLevel : 20, 20) + 10, 1, 64);
                    for (int k = 0; k < 20; k++)
                    {
                        int colSet = 71 + (k * 2);
                        int colDat = 72 + (k * 2);
                        ReadOnlySpan<byte> rawSet = table.GetRawCell(r, colSet, inheritRow2Default: false);
                        if (rawSet.IsEmpty)
                        {
                            continue;
                        }

                        string settingLatin1 = Encoding.Latin1.GetString(rawSet).Trim();
                        if (settingLatin1.Length == 0)
                        {
                            continue;
                        }

                        ReadOnlySpan<byte> rawDat = table.GetRawCell(r, colDat, inheritRow2Default: false);
                        string dataLatin1 = rawDat.IsEmpty ? string.Empty : Encoding.Latin1.GetString(rawDat).Trim();

                        var p0Vals = new int[evalLevels];
                        var p1Vals = new int[evalLevels];
                        var p2Vals = new int[evalLevels];
                        bool anyNonZeroP1 = false;
                        bool anyNonZeroP2 = false;
                        bool evaluatedAny = false;

                        for (int lvl = 1; lvl <= evalLevels; lvl++)
                        {
                            interp.ResetStepBudget(300_000);
                            Lua4Value[] ret = interp.CallGlobal(
                                "GetSkillLevelData",
                                Lua4Value.FromString(settingLatin1),
                                Lua4Value.FromString(dataLatin1),
                                Lua4Value.FromNumber(lvl));

                            string retStr = ret.Length > 0 ? ret[0].AsLuaString() : string.Empty;
                            if (TryParseParamTriple(retStr, out int v0, out int v1, out int v2))
                            {
                                evaluatedAny = true;
                                p0Vals[lvl - 1] = v0;
                                p1Vals[lvl - 1] = v1;
                                p2Vals[lvl - 1] = v2;
                                if (v1 != 0)
                                {
                                    anyNonZeroP1 = true;
                                }

                                if (v2 != 0)
                                {
                                    anyNonZeroP2 = true;
                                }
                            }
                        }

                        if (!evaluatedAny)
                        {
                            continue;
                        }

                        string attrCode = settingLatin1.ToLowerInvariant();
                        if (attrCode == "skill_cost_v")
                        {
                            for (int i = 0; i < p0Vals.Length; i++)
                            {
                                p0Vals[i] = Math.Max(0, p0Vals[i]);
                            }

                            costScaling["valuesByLevel"] = p0Vals;
                            costScaling["baseMana"] = p0Vals[0];
                            continue;
                        }

                        int attrId = magicReg.GetId(attrCode);
                        string slotCat = MagicAttributeRegistry.GetSkillSlotCategory(attrId, attrCode);
                        var effDict = new Dictionary<string, object?>
                        {
                            ["attributeId"] = attrId,
                            ["attributeCode"] = attrCode,
                            ["slotCategory"] = slotCat,
                            ["curve"] = "table",
                            ["valuesByLevel"] = p0Vals,
                            ["param0"] = new Dictionary<string, object?>
                            {
                                ["curve"] = "table",
                                ["valuesByLevel"] = p0Vals,
                            },
                        };

                        if (anyNonZeroP1)
                        {
                            effDict["param1"] = new Dictionary<string, object?>
                            {
                                ["curve"] = "table",
                                ["valuesByLevel"] = p1Vals,
                            };
                            if (p1Vals[0] > 0)
                            {
                                effDict["durationFrames"] = p1Vals[0];
                            }
                        }

                        if (anyNonZeroP2)
                        {
                            effDict["param2"] = new Dictionary<string, object?>
                            {
                                ["curve"] = "table",
                                ["valuesByLevel"] = p2Vals,
                            };
                        }

                        effectScaling.Add(effDict);
                    }
                }

                var props = new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["code"] = string.Empty,
                    ["name"] = name,
                    ["style"] = style,
                    ["elementSeries"] = series,
                    ["charClass"] = charClass,
                    ["isOffensive"] = isOffensive,
                    ["isPhysical"] = isPhysical,
                    ["isMelee"] = isMelee,
                    ["isAura"] = isAura,
                    ["isBaseSkill"] = isBaseSkill,
                    ["isWeaponSkill"] = weaponSkill,
                    ["isExpSkill"] = isExpSkill,
                    ["useAttackRating"] = useAr,
                    ["doHurtAnimation"] = doHurt,
                    ["peaceCanUse"] = peaceCanUse,
                    ["clientSend"] = clientSend,
                    ["lrSkillInfo"] = lrSkill switch { 0 => "both", 1 => "leftOnly", 2 => "rightOnly", _ => "none" },
                    ["reqLevel"] = reqLevel,
                    ["maxLevel"] = maxLevel,
                    ["equipLimit"] = eqLimit,
                    ["horseLimit"] = horseLimit,
                    ["castRangeMps"] = castRangeMps,
                    ["waitFrames"] = waitFrames,
                    ["minFramesPerCast"] = timePerCast,
                    ["minFramesPerCastOnHorse"] = timePerCastHorse,
                    ["stateSpecialId"] = stateSpecId,
                    ["statePriority"] = statePrio,
                    ["charAnimId"] = charAnimId,
                    ["stopWhenMove"] = stopWhenMove,
                    ["heelAtParent"] = heelAtParent,
                    ["byMissile"] = byMissile,
                    ["relativePosType"] = relPosType,
                    ["param1"] = param1,
                    ["param2"] = param2,
                    ["targeting"] = new Dictionary<string, object?>
                    {
                        ["targetOnly"] = targetOnly,
                        ["targetEnemy"] = targetEnemy,
                        ["targetAlly"] = targetAlly,
                        ["targetSelf"] = targetSelf,
                        ["targetOther"] = targetOther,
                        ["targetObj"] = targetObj,
                        ["targetNoNpc"] = targetNoNpc,
                    },
                    ["missileForm"] = MapMissileForm(misslesForm),
                    ["missileGenerateStyle"] = MapMissileGenerateStyle(mslsGen),
                    ["missileGenerateDelayFrames"] = mslsGenDelay,
                    ["costScaling"] = costScaling,
                };

                if (!string.IsNullOrWhiteSpace(desc))
                {
                    props["description"] = desc;
                }

                if (skillStyle == 0 && childSkillId > 0)
                {
                    props["missileId"] = childSkillId;
                }
                else if (childSkillId > 0)
                {
                    props["childSkill"] = new Dictionary<string, object?>
                    {
                        ["skillId"] = childSkillId,
                        ["skillLevel"] = childSkillLvl,
                        ["count"] = childSkillNum,
                    };
                }

                if (startEvent != 0 || flyEvent != 0 || collideEvent != 0 || vanishEvent != 0)
                {
                    props["eventSkills"] = new Dictionary<string, object?>
                    {
                        ["eventSkillLevel"] = eventSkillLvl,
                        ["onStartSkillId"] = startEvent != 0 ? startSkillId : 0,
                        ["onStartMinParentLevel"] = Math.Max(0, startEvent),
                        ["onFlySkillId"] = flyEvent != 0 ? flySkillId : 0,
                        ["onFlyIntervalFrames"] = flyEventTime,
                        ["onFlyMinParentLevel"] = Math.Max(0, flyEvent),
                        ["onCollideSkillId"] = collideEvent != 0 ? collideSkillId : 0,
                        ["onCollideMinParentLevel"] = Math.Max(0, collideEvent),
                        ["onVanishSkillId"] = vanishEvent != 0 ? vanishSkillId : 0,
                        ["onVanishMinParentLevel"] = Math.Max(0, vanishEvent),
                    };
                }

                if (effectScaling.Count > 0)
                {
                    props["effectScaling"] = effectScaling;
                }

                entries.Add((id, name, props));
                registry.SkillIdByName.TryAdd(name, id);
            }
        }

        string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(entries, e => e.Name, e => e.Id);
        var skillDocs = new List<Dictionary<string, object?>>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].Props["code"] = codes[i];
            registry.SkillCodeById[entries[i].Id] = codes[i];
            skillDocs.Add(entries[i].Props);
        }

        var weaponMappings = LoadWeaponSkillMappings(serverRootPath, registry);
        Dictionary<string, object?>? constRules = LoadAttributeConstRules(serverRootPath);

        var rootDoc = new Dictionary<string, object?>
        {
            ["$schema"] = "../../schemas/skill-catalog.schema.json",
            ["schemaVersion"] = "1.0.0",
            ["skills"] = skillDocs,
            ["weaponSkillMappings"] = weaponMappings,
        };
        if (constRules is not null)
        {
            rootDoc["attributeConstRules"] = constRules;
        }

        LegacyPathResolver.WriteCanonicalJson(Path.Combine(skillsOutDir, "base-and-factions.skills.json"), rootDoc);
        registry.RecordCount("skills.baseAndFactions", legacyValidRows, skillDocs.Count);

        return skillDocs.Count;
    }

    private static List<Dictionary<string, object?>> LoadWeaponSkillMappings(string serverRootPath, ExportCatalogRegistry registry)
    {
        var list = new List<Dictionary<string, object?>>();
        int legacyRows = 0;
        string? path = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\clientweaponskill.txt");
        if (path is not null)
        {
            KTabFileTable table = KTabFileTable.Load(path);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                int basicSkillId = table.GetInt(r, 2, 0);
                if (basicSkillId < 1)
                {
                    continue;
                }

                legacyRows++;
                int weaponTypeId = Math.Max(0, table.GetInt(r, 0, r));
                string name = table.GetString(r, 1);
                list.Add(new Dictionary<string, object?>
                {
                    ["weaponTypeId"] = weaponTypeId,
                    ["name"] = string.IsNullOrWhiteSpace(name) ? $"Weapon {weaponTypeId}" : name,
                    ["basicSkillId"] = basicSkillId,
                });
            }
        }

        registry.RecordCount("skills.weaponSkillMappings", legacyRows, list.Count);
        return list;
    }

    private static Dictionary<string, object?>? LoadAttributeConstRules(string serverRootPath)
    {
        string? path = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\attribconstdata.ini");
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        var sections = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        string currentSection = string.Empty;

        foreach (string rawLine in File.ReadAllLines(path, Encoding.Latin1))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                if (!sections.ContainsKey(currentSection))
                {
                    sections[currentSection] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                }

                continue;
            }

            int eq = line.IndexOf('=');
            if (eq > 0 && currentSection.Length > 0 &&
                int.TryParse(line[(eq + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int val))
            {
                sections[currentSection][line[..eq].Trim()] = val;
            }
        }

        var result = new Dictionary<string, object?>();
        if (TryBuildCurseRule(sections, "ignoreskill_p", out Dictionary<string, object?>? ignoreRule))
        {
            result["ignoreSkillRule"] = ignoreRule;
        }

        if (TryBuildCurseRule(sections, "returnskill_p", out Dictionary<string, object?>? returnRule))
        {
            result["returnSkillRule"] = returnRule;
        }

        return result.Count > 0 ? result : null;
    }

    private static bool TryBuildCurseRule(
        Dictionary<string, Dictionary<string, int>> sections,
        string sectionName,
        out Dictionary<string, object?>? ruleObj)
    {
        ruleObj = null;
        if (!sections.TryGetValue(sectionName, out Dictionary<string, int>? kv))
        {
            return false;
        }

        int count = kv.GetValueOrDefault("Count", 0);
        int stateSkillId = kv.GetValueOrDefault("Data0", 0);
        int durationFrames = kv.GetValueOrDefault("Data1", 0);
        if (stateSkillId < 1 || durationFrames < 1)
        {
            return false;
        }

        var curseIds = new List<int>();
        for (int i = 2; i < count; i++)
        {
            if (kv.TryGetValue($"Data{i}", out int sid) && sid >= 1)
            {
                curseIds.Add(sid);
            }
        }

        ruleObj = new Dictionary<string, object?>
        {
            ["stateSkillId"] = stateSkillId,
            ["durationFrames"] = durationFrames,
            ["eligibleCurseSkillIds"] = curseIds,
        };
        return true;
    }

    private static Lua4Interpreter? ResolveSkillInterpreter(
        string serverRootPath,
        ReadOnlySpan<byte> rawScriptCell,
        Dictionary<string, List<Lua4Token>> sharedTokenCache,
        Dictionary<string, Lua4Interpreter?> cache)
    {
        if (rawScriptCell.IsEmpty)
        {
            return null;
        }

        string? resolved = LegacyPathResolver.TryResolveFile(serverRootPath, rawScriptCell);
        if (resolved is null)
        {
            return null;
        }

        if (cache.TryGetValue(resolved, out Lua4Interpreter? existing))
        {
            return existing;
        }

        try
        {
            var interp = new Lua4Interpreter(serverRootPath, sharedTokenCache, lenientMissingGlobals: true);
            if (interp.ExecuteFile(resolved))
            {
                cache[resolved] = interp;
                return interp;
            }
        }
        catch
        {
            // Ignore malformed unused legacy script
        }

        cache[resolved] = null;
        return null;
    }

    private static bool TryParseParamTriple(string text, out int p0, out int p1, out int p2)
    {
        p0 = 0;
        p1 = 0;
        p2 = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Split(',');
        if (parts.Length == 0)
        {
            return false;
        }

        bool ok0 = TryParseLooseInt(parts[0], out p0);
        if (parts.Length > 1)
        {
            TryParseLooseInt(parts[1], out p1);
        }

        if (parts.Length > 2)
        {
            TryParseLooseInt(parts[2], out p2);
        }

        return ok0;
    }

    private static bool TryParseLooseInt(string part, out int value)
    {
        string trimmed = part.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && !double.IsNaN(d) && !double.IsInfinity(d))
        {
            value = (int)Math.Round(d);
            return true;
        }

        value = 0;
        return false;
    }

    private static string MapMissileMoveKind(int moveKind) =>
        moveKind switch
        {
            0 => "stand",
            1 => "line64",
            2 => "random",
            3 => "circle",
            4 => "helix",
            5 => "follow",
            6 => "motion",
            7 => "parabola",
            8 => "singleLine",
            9 => "stationary",
            10 => "rollBack",
            _ => "custom",
        };

    private static string MapSkillStyle(int skillStyle, bool isMelee, bool isAura, bool targetEnemy) =>
        skillStyle switch
        {
            0 => isMelee ? "melee" : "projectile",
            1 => "melee",
            2 => isAura ? "aura" : (targetEnemy ? "targetBuff" : "selfBuff"),
            3 => "passive",
            4 => "summonNpc",
            5 => "strideObstacle",
            6 => "corpseExplode",
            8 => "jumpAndAttack",
            7 or 9 or 10 => "steal",
            _ => "melee",
        };

    private static string MapMissileForm(int form) =>
        form switch
        {
            0 => "wall",
            1 => "line",
            2 => "spread",
            3 => "circle",
            4 => "random",
            5 => "zone",
            6 => "atTarget",
            7 => "atFirer",
            8 => "meleeBlur",
            9 => "meleeJump",
            10 => "meleeJumpAttack",
            11 => "meleeRunAttack",
            12 => "meleeMany",
            _ => "line",
        };

    private static string MapMissileGenerateStyle(int gen) =>
        gen switch
        {
            1 => "sameTime",
            2 => "order",
            3 => "randomOrder",
            4 => "randomSame",
            5 => "centerExtendLine",
            _ => "none",
        };

    private static string StripQuotes(string s)
    {
        string t = s.Trim();
        if (t.Length >= 2 && t[0] == '"' && t[^1] == '"')
        {
            return t[1..^1].Trim();
        }

        return t;
    }
}
