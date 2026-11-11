using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JXServer.DataExporter.Legacy;

namespace JXServer.DataExporter.Exporters;

/// <summary>
/// Exports all legacy item catalogs (<c>15</c> normal equipment tables, <c>goldequip.txt</c>, <c>platinaequip.txt</c>,
/// <c>brokenequip.txt</c>, <c>fusion.txt</c>, <c>potion.txt</c>, <c>townportal.txt</c>, <c>questkey.txt</c>, <c>magicscript.txt</c>),
/// all <c>3</c> magic affix pools, equipment suites, <c>ObjData.txt</c>, and <c>atlas_compound.txt</c> (`JXData Standard v1`).
/// </summary>
public sealed class ItemAndAffixDataExporter
{
    private static readonly string[] NormalEquipFiles =
    [
        "meleeweapon.txt",
        "rangeweapon.txt",
        "armor.txt",
        "ring.txt",
        "amulet.txt",
        "boot.txt",
        "belt.txt",
        "helm.txt",
        "cuff.txt",
        "pendant.txt",
        "horse.txt",
        "mask.txt",
        "mantle.txt",
        "signet.txt",
        "shipin.txt",
    ];

    public (int ItemCount, int AffixCount, int GroundObjCount) ExportItemsAndAffixes(
        string serverRootPath,
        string outputDataDirectory,
        ExportCatalogRegistry? registry = null)
    {
        registry ??= new ExportCatalogRegistry();
        MagicAttributeRegistry magicReg = MagicAttributeRegistry.Load(serverRootPath);

        string itemsOutDir = Path.Combine(outputDataDirectory, "items");
        Directory.CreateDirectory(itemsOutDir);

        string obsoleteSingleFile = Path.Combine(itemsOutDir, "items.items.json");
        if (File.Exists(obsoleteSingleFile))
        {
            File.Delete(obsoleteSingleFile);
        }

        // 1. Export all 3 magic affix pools -> magic-affixes.items.json
        int affixCount = ExportMagicAffixes(serverRootPath, itemsOutDir, magicReg, registry);

        // 2. Load auxiliary stacking & forbid rules
        Dictionary<int, int> questKeyStacks = LoadQuestKeyStacks(serverRootPath);
        Dictionary<int, (int MaxStack, bool SameLvl, bool SameSeries, bool SameLuck, bool[] SameMagic)> magicScriptStacks =
            LoadMagicScriptStacks(serverRootPath);
        List<(int Genre, int Quality, int Detail, int Particular, bool NoMove, bool NoThrow, bool NoTrade, bool NoSell, bool NoDrop)> forbidRules =
            LoadForbidItemRules(serverRootPath);

        // 3. Parse all item rows across all 4 output groups before globally disambiguating semantic codes
        // Tuple layout:
        // (int GroupKind [0=equip,1=gold,2=platina,3=consumable], int SourceIdx, int RowId, string Name, int Genre, int Quality, int Detail, int Particular, int Level, int Series, Dictionary<string, object?> Props)
        var allItemEntries = new List<(int GroupKind, int SourceIdx, int RowId, string Name, int Genre, int Quality, int Detail, int Particular, int Level, int Series, Dictionary<string, object?> Props)>(24500);

        int legacyNormalEquipRows = 0;
        for (int fIdx = 0; fIdx < NormalEquipFiles.Length; fIdx++)
        {
            string fileName = NormalEquipFiles[fIdx];
            string? path = LegacyPathResolver.TryResolveStringPath(serverRootPath, $@"settings\item\004\{fileName}");
            if (path is null)
            {
                continue;
            }

            KTabFileTable table = KTabFileTable.Load(path);
            bool isMask = string.Equals(fileName, "mask.txt", StringComparison.OrdinalIgnoreCase);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                legacyNormalEquipRows++;
                var entry = ParseStandardEquipRow(table, r, groupKind: 0, sourceIdx: fIdx, quality: 0, isMask, magicReg, forbidRules);
                allItemEntries.Add(entry);
            }
        }

        // brokenequip.txt -> equipment.items.json (genre=7, quality=4)
        int legacyBrokenRows = 0;
        string? brokenPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\brokenequip.txt");
        if (brokenPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(brokenPath);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                legacyBrokenRows++;
                string name = NonEmptyName(table.GetString(r, 0), "Trang bị tổn hại", r);
                int genre = Math.Clamp(table.GetInt(r, 1, 7), 0, 8);
                int objId = Math.Max(0, table.GetInt(r, 3, 0));
                int w = Math.Clamp(table.GetInt(r, 4, 1), 1, 6);
                int h = Math.Clamp(table.GetInt(r, 5, 1), 1, 10);
                int price = Math.Max(0, table.GetInt(r, 6, 0));
                string desc = table.GetString(r, 7);

                var props = BuildBaseItemDict(r, name, desc, genre, quality: 4, detail: 0, particular: 0, level: 1, series: -1, w, h, objId, price);
                ApplyForbidRestrictions(props, genre, 4, 0, 0, forbidRules);
                allItemEntries.Add((0, 20, r, name, genre, 4, 0, 0, 1, -1, props));
            }
        }

        // fusion.txt -> equipment.items.json (genre=8, quality=0)
        int legacyFusionRows = 0;
        string? fusionPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\fusion.txt");
        if (fusionPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(fusionPath);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                legacyFusionRows++;
                string name = NonEmptyName(table.GetString(r, 0), "Văn Cương", r);
                int genre = Math.Clamp(table.GetInt(r, 1, 8), 0, 8);
                int detail = Math.Max(0, table.GetInt(r, 2, 1));
                int particular = Math.Max(0, table.GetInt(r, 3, 1));
                int objId = Math.Max(0, table.GetInt(r, 5, 0));
                int w = Math.Clamp(table.GetInt(r, 6, 1), 1, 6);
                int h = Math.Clamp(table.GetInt(r, 7, 1), 1, 10);
                string desc = table.GetString(r, 8);
                int series = ReadOptionalSeries(table, r, 9);
                int price = Math.Max(0, table.GetInt(r, 10, 0));
                int level = Math.Clamp(table.GetInt(r, 11, 1), 0, 30);
                int canStack = table.GetInt(r, 12, 0);
                int geAffixRowId = table.GetInt(r, 14, 0);

                var props = BuildBaseItemDict(r, name, desc, genre, quality: 0, detail, particular, level, series, w, h, objId, price);
                if (canStack > 0)
                {
                    props["maxStackCount"] = Math.Max(1, canStack == 1 ? 999 : canStack);
                }

                if (geAffixRowId > 0)
                {
                    var fixedList = new List<Dictionary<string, object?>> { BuildFixedAffixEntry(geAffixRowId, 0, isHidden: false, registry) };
                    props["fixedMagicAffixes"] = fixedList;
                }

                ApplyForbidRestrictions(props, genre, 0, detail, particular, forbidRules);
                allItemEntries.Add((0, 21, r, name, genre, 0, detail, particular, level, series, props));
            }
        }

        // goldequip.txt -> golden-equipment.items.json (quality=1)
        int legacyGoldRows = 0;
        var suiteFirstItemName = new Dictionary<int, string>();
        string? goldPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\goldequip.txt");
        if (goldPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(goldPath);
            legacyGoldRows = table.DataRowCount;
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                var entry = ParseStandardEquipRow(table, r, groupKind: 1, sourceIdx: 30, quality: 1, isMask: false, magicReg, forbidRules);
                var fixedAffixes = new List<Dictionary<string, object?>>();
                for (int slot = 0; slot < 6; slot++)
                {
                    int affixRowId = table.GetInt(r, 46 + slot, 0);
                    if (affixRowId > 0)
                    {
                        fixedAffixes.Add(BuildFixedAffixEntry(affixRowId, 0, isHidden: (slot % 2) == 1, registry));
                    }
                }

                if (fixedAffixes.Count > 0)
                {
                    entry.Props["fixedMagicAffixes"] = fixedAffixes;
                }

                int suiteId = table.GetInt(r, 52, 0);
                int extSuiteId = table.GetInt(r, 53, 0);
                int extPieceIdx = table.GetInt(r, 54, 0);
                int extHid1 = table.GetInt(r, 55, 0);
                int extHid2 = table.GetInt(r, 56, 0);
                if (suiteId > 0 && !suiteFirstItemName.ContainsKey(suiteId))
                {
                    suiteFirstItemName[suiteId] = entry.Name;
                }

                AddSuiteAndSmelting(entry.Props, suiteId, extSuiteId, extPieceIdx, extHid1, extHid2, table.GetInt(r, 57, 0), table.GetInt(r, 58, 0));
                allItemEntries.Add(entry);
            }
        }

        // platinaequip.txt -> platina-equipment.items.json (quality=3)
        int legacyPlatinaRows = 0;
        var extSuiteFirstItemName = new Dictionary<int, string>();
        string? platinaPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\platinaequip.txt");
        if (platinaPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(platinaPath);
            legacyPlatinaRows = table.DataRowCount;
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                var entry = ParseStandardEquipRow(table, r, groupKind: 2, sourceIdx: 31, quality: 3, isMask: false, magicReg, forbidRules);
                var fixedAffixes = new List<Dictionary<string, object?>>();
                for (int slot = 0; slot < 6; slot++)
                {
                    int col0 = 46 + (slot * 2);
                    int affix0 = table.GetInt(r, col0, 0);
                    int affix10 = table.GetInt(r, col0 + 1, 0);
                    if (affix0 > 0 || affix10 > 0)
                    {
                        fixedAffixes.Add(BuildFixedAffixEntry(Math.Max(0, affix0), Math.Max(0, affix10), isHidden: (slot % 2) == 1, registry));
                    }
                }

                if (fixedAffixes.Count > 0)
                {
                    entry.Props["fixedMagicAffixes"] = fixedAffixes;
                }

                int suiteId = table.GetInt(r, 58, 0);
                int lvl6Skill = Math.Max(0, table.GetInt(r, 59, 0));
                int lvl10Skill = Math.Max(0, table.GetInt(r, 60, 0));
                if (lvl6Skill > 0 || lvl10Skill > 0)
                {
                    entry.Props["platinaUpgrade"] = new Dictionary<string, object?>
                    {
                        ["level6SkillId"] = lvl6Skill,
                        ["level10SkillId"] = lvl10Skill,
                    };
                }

                int extSuiteId = table.GetInt(r, 61, 0);
                int extPieceIdx = table.GetInt(r, 62, 0);
                int extHid1 = table.GetInt(r, 63, 0);
                int extHid2 = table.GetInt(r, 64, 0);
                if (extSuiteId > 0 && !extSuiteFirstItemName.ContainsKey(extSuiteId))
                {
                    extSuiteFirstItemName[extSuiteId] = entry.Name;
                }

                AddSuiteAndSmelting(entry.Props, suiteId, extSuiteId, extPieceIdx, extHid1, extHid2, table.GetInt(r, 65, 0), table.GetInt(r, 66, 0));
                int wealth = Math.Max(0, table.GetInt(r, 67, 0));
                if (wealth > 0)
                {
                    entry.Props["wealthValue"] = wealth;
                }

                int iconResId = Math.Max(0, table.GetInt(r, 72, 0));
                if (iconResId > 0)
                {
                    entry.Props["appearanceResId"] = iconResId;
                }

                allItemEntries.Add(entry);
            }
        }

        // Consumables & Scripts -> consumables-and-scripts.items.json
        // potion.txt (genre=1)
        int legacyPotionRows = 0;
        string? potionPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\potion.txt");
        if (potionPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(potionPath);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                legacyPotionRows++;
                string name = NonEmptyName(table.GetString(r, 0), "Dược phẩm", r);
                int genre = Math.Clamp(table.GetInt(r, 1, 1), 0, 8);
                int detail = Math.Max(0, table.GetInt(r, 2, 0));
                int particular = Math.Max(0, table.GetInt(r, 3, 0));
                int objId = Math.Max(0, table.GetInt(r, 5, 0));
                int w = Math.Clamp(table.GetInt(r, 6, 1), 1, 6);
                int h = Math.Clamp(table.GetInt(r, 7, 1), 1, 10);
                string desc = table.GetString(r, 8);
                int series = ReadOptionalSeries(table, r, 9);
                int price = Math.Max(0, table.GetInt(r, 10, 0));
                int level = Math.Clamp(table.GetInt(r, 11, 1), 0, 30);
                int canStack = table.GetInt(r, 12, 0);

                var props = BuildBaseItemDict(r, name, desc, genre, quality: 0, detail, particular, level, series, w, h, objId, price);
                if (canStack > 0)
                {
                    props["maxStackCount"] = Math.Max(1, canStack == 1 ? 50 : canStack);
                }

                var baseAttrs = new List<Dictionary<string, object?>>();
                for (int k = 0; k < 5; k++)
                {
                    int col = 13 + (k * 3);
                    if (table.GetRawCell(r, col).IsEmpty)
                    {
                        continue;
                    }

                    int attrId = table.GetInt(r, col, -1);
                    if (attrId < 0)
                    {
                        continue;
                    }

                    int val = table.GetInt(r, col + 1, 0);
                    int dur = Math.Max(0, table.GetInt(r, col + 2, 0));
                    baseAttrs.Add(new Dictionary<string, object?>
                    {
                        ["attributeId"] = attrId,
                        ["attributeCode"] = magicReg.GetCode(attrId),
                        ["minValue"] = val,
                        ["maxValue"] = val,
                        ["durationFrames"] = dur,
                    });
                }

                if (baseAttrs.Count > 0)
                {
                    props["baseAttributes"] = baseAttrs;
                }

                ApplyForbidRestrictions(props, genre, 0, detail, particular, forbidRules);
                allItemEntries.Add((3, 40, r, name, genre, 0, detail, particular, level, series, props));
            }
        }

        // townportal.txt (genre=5)
        int legacyTownPortalRows = 0;
        string? tpPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\townportal.txt");
        if (tpPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(tpPath);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                legacyTownPortalRows++;
                string name = NonEmptyName(table.GetString(r, 0), "Thổ địa phù", r);
                int genre = Math.Clamp(table.GetInt(r, 1, 5), 0, 8);
                int objId = Math.Max(0, table.GetInt(r, 3, 0));
                int w = Math.Clamp(table.GetInt(r, 4, 1), 1, 6);
                int h = Math.Clamp(table.GetInt(r, 5, 1), 1, 10);
                int price = Math.Max(0, table.GetInt(r, 6, 0));
                string desc = table.GetString(r, 7);

                var props = BuildBaseItemDict(r, name, desc, genre, quality: 0, detail: 0, particular: r - 1, level: 1, series: -1, w, h, objId, price);
                ApplyForbidRestrictions(props, genre, 0, 0, r - 1, forbidRules);
                allItemEntries.Add((3, 41, r, name, genre, 0, 0, r - 1, 1, -1, props));
            }
        }

        // questkey.txt (genre=4)
        int legacyQuestKeyRows = 0;
        string? qkPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\questkey.txt");
        if (qkPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(qkPath);
            legacyQuestKeyRows = table.DataRowCount;
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                string name = NonEmptyName(table.GetString(r, 0), "Vật phẩm nhiệm vụ", r);
                int genre = Math.Clamp(table.GetInt(r, 1, 4), 0, 8);
                int detail = Math.Max(0, table.GetInt(r, 2, r - 1));
                int objId = Math.Max(0, table.GetInt(r, 4, 0));
                int w = Math.Clamp(table.GetInt(r, 5, 1), 1, 6);
                int h = Math.Clamp(table.GetInt(r, 6, 1), 1, 10);
                string desc = table.GetString(r, 7);
                int particular = Math.Max(0, table.GetInt(r, 8, 0));
                int maxStack = Math.Max(1, table.GetInt(r, 9, 1));
                if (questKeyStacks.TryGetValue(r, out int overrideStack) && overrideStack > 0)
                {
                    maxStack = overrideStack;
                }

                var props = BuildBaseItemDict(r, name, desc, genre, quality: 0, detail, particular, level: 1, series: -1, w, h, objId, priceGold: 0);
                if (maxStack > 1)
                {
                    props["maxStackCount"] = maxStack;
                }

                ApplyForbidRestrictions(props, genre, 0, detail, particular, forbidRules);
                allItemEntries.Add((3, 42, r, name, genre, 0, detail, particular, 1, -1, props));
            }
        }

        // magicscript.txt (genre=6) - preserve ALL physical rows including blank rows 1232..1237!
        int legacyMagicScriptRows = 0;
        string? msPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\magicscript.txt");
        if (msPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(msPath);
            legacyMagicScriptRows = table.DataRowCount;
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                string name = NonEmptyName(table.GetString(r, 0), "Vật phẩm kịch bản", r);
                int genre = Math.Clamp(table.GetInt(r, 1, 6), 0, 8);
                int detail = Math.Max(0, table.GetInt(r, 2, 0));
                int particular = Math.Max(0, table.GetInt(r, 3, r - 1));
                int objId = Math.Max(0, table.GetInt(r, 5, 0));
                int w = Math.Clamp(table.GetInt(r, 6, 1), 1, 6);
                int h = Math.Clamp(table.GetInt(r, 7, 1), 1, 10);
                string desc = table.GetString(r, 8);
                int series = ReadOptionalSeries(table, r, 9);
                int price = Math.Max(0, table.GetInt(r, 10, 0));
                int level = Math.Clamp(table.GetInt(r, 11, 1), 0, 30);
                int canStack = table.GetInt(r, 12, 0);
                string rawScriptOrAction = table.GetEnginePath(r, 13);
                int castSkillId = Math.Max(0, table.GetInt(r, 14, 0));
                int castType = table.GetInt(r, 15, 0);
                bool isBroadcast = table.GetInt(r, 16, 0) != 0;
                bool canQuickBelt = table.GetInt(r, 18, 1) != 0;
                int maxStack = canStack > 0 ? Math.Max(1, table.GetInt(r, 20, 1)) : 1;

                bool sameLvl = table.GetInt(r, 21, 0) != 0;
                bool sameSer = table.GetInt(r, 22, 0) != 0;
                bool sameLuck = table.GetInt(r, 23, 0) != 0;
                var sameMag = new bool[6];
                for (int m = 0; m < 6; m++)
                {
                    sameMag[m] = table.GetInt(r, 24 + m, 0) != 0;
                }

                if (magicScriptStacks.TryGetValue(r - 1, out var stOverride))
                {
                    maxStack = Math.Max(1, stOverride.MaxStack);
                    sameLvl = stOverride.SameLvl;
                    sameSer = stOverride.SameSeries;
                    sameLuck = stOverride.SameLuck;
                    sameMag = stOverride.SameMagic;
                }

                var props = BuildBaseItemDict(r, name, desc, genre, quality: 0, detail, particular, level, series, w, h, objId, price);
                if (maxStack > 1)
                {
                    props["maxStackCount"] = maxStack;
                }

                if (sameLvl || sameSer || sameLuck || sameMag.Any(x => x))
                {
                    props["stackingRules"] = new Dictionary<string, object?>
                    {
                        ["requireSameLevel"] = sameLvl,
                        ["requireSameSeries"] = sameSer,
                        ["requireSameLuck"] = sameLuck,
                        ["requireSameMagicLevels"] = sameMag,
                    };
                }

                if (!string.IsNullOrWhiteSpace(rawScriptOrAction) || castSkillId > 0)
                {
                    var su = new Dictionary<string, object?>
                    {
                        ["castType"] = castType,
                        ["isBroadcast"] = isBroadcast,
                        ["canPutInQuickBelt"] = canQuickBelt,
                        ["consumeOnUse"] = true,
                    };
                    if (rawScriptOrAction.Contains(".lua", StringComparison.OrdinalIgnoreCase))
                    {
                        su["scriptPath"] = rawScriptOrAction;
                    }
                    else if (!string.IsNullOrWhiteSpace(rawScriptOrAction) && rawScriptOrAction != "\\0")
                    {
                        su["actionCode"] = rawScriptOrAction.TrimStart('\\');
                    }

                    if (castSkillId > 0)
                    {
                        su["castSkillId"] = castSkillId;
                    }

                    props["scriptUsage"] = su;
                }

                ApplyForbidRestrictions(props, genre, 0, detail, particular, forbidRules);
                allItemEntries.Add((3, 43, r, name, genre, 0, detail, particular, level, series, props));
            }
        }

        // 4. Assign symmetric, globally unique semantic codes across ALL items
        AssignGlobalSemanticItemCodes(allItemEntries, registry);

        // 5. Partition and write the 4 item JSON files
        var equipDocs = allItemEntries.Where(e => e.GroupKind == 0).Select(e => e.Props).ToList();
        var goldDocs = allItemEntries.Where(e => e.GroupKind == 1).Select(e => e.Props).ToList();
        var platinaDocs = allItemEntries.Where(e => e.GroupKind == 2).Select(e => e.Props).ToList();
        var consumableDocs = allItemEntries.Where(e => e.GroupKind == 3).Select(e => e.Props).ToList();

        LegacyPathResolver.WriteCanonicalJson(
            Path.Combine(itemsOutDir, "equipment.items.json"),
            new Dictionary<string, object?>
            {
                ["$schema"] = "../../schemas/item-catalog.schema.json",
                ["schemaVersion"] = "1.0.0",
                ["items"] = equipDocs,
            });

        LegacyPathResolver.WriteCanonicalJson(
            Path.Combine(itemsOutDir, "golden-equipment.items.json"),
            new Dictionary<string, object?>
            {
                ["$schema"] = "../../schemas/item-catalog.schema.json",
                ["schemaVersion"] = "1.0.0",
                ["items"] = goldDocs,
            });

        LegacyPathResolver.WriteCanonicalJson(
            Path.Combine(itemsOutDir, "platina-equipment.items.json"),
            new Dictionary<string, object?>
            {
                ["$schema"] = "../../schemas/item-catalog.schema.json",
                ["schemaVersion"] = "1.0.0",
                ["items"] = platinaDocs,
            });

        LegacyPathResolver.WriteCanonicalJson(
            Path.Combine(itemsOutDir, "consumables-and-scripts.items.json"),
            new Dictionary<string, object?>
            {
                ["$schema"] = "../../schemas/item-catalog.schema.json",
                ["schemaVersion"] = "1.0.0",
                ["items"] = consumableDocs,
            });

        // Record exact Layer-2 parity counts
        registry.RecordCount("items.normalEquipment", legacyNormalEquipRows + legacyBrokenRows + legacyFusionRows, equipDocs.Count);
        registry.RecordCount("items.goldenEquipment", legacyGoldRows, goldDocs.Count);
        registry.RecordCount("items.platinaEquipment", legacyPlatinaRows, platinaDocs.Count);
        registry.RecordCount("items.consumablesAndScripts", legacyPotionRows + legacyTownPortalRows + legacyQuestKeyRows + legacyMagicScriptRows, consumableDocs.Count);

        // 6. Export sets, ground objects, and crafting recipes -> sets-objects-recipes.items.json
        int groundObjCount = ExportSetsObjectsAndRecipes(serverRootPath, itemsOutDir, suiteFirstItemName, extSuiteFirstItemName, registry);

        return (allItemEntries.Count, affixCount, groundObjCount);
    }

    private static int ExportMagicAffixes(
        string serverRootPath,
        string itemsOutDir,
        MagicAttributeRegistry magicReg,
        ExportCatalogRegistry registry)
    {
        var affixDocs = new List<Dictionary<string, object?>>(7200);

        // 1a. blueRandom: magicattrib.txt
        int legacyBlueCount = 0;
        string? bluePath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\magicattrib.txt");
        if (bluePath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(bluePath);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                legacyBlueCount++;
                string rawName = table.GetString(r, 0);
                bool isSuffix = table.GetInt(r, 1, 1) == 0;
                int series = ClampAffixSeries(ReadOptionalSeries(table, r, 2));
                int level = Math.Clamp(table.GetInt(r, 3, 1), 1, 20);
                int attrId = Math.Max(0, table.GetInt(r, 4, 0));
                string attrCode = magicReg.GetCode(attrId);
                string desc = table.GetString(r, 11);
                string nameTpl = magicReg.GetNameTemplate(attrId, !string.IsNullOrWhiteSpace(desc) ? desc : rawName);
                if (string.IsNullOrWhiteSpace(nameTpl))
                {
                    nameTpl = attrCode;
                }

                int p0Min = table.GetInt(r, 5, 0);
                int p0Max = table.GetInt(r, 6, p0Min);
                int p1Min = table.GetInt(r, 7, -1);
                int p1Max = table.GetInt(r, 8, p1Min);
                int p2Min = table.GetInt(r, 9, 0);
                int p2Max = table.GetInt(r, 10, p2Min);

                int wMelee = Math.Max(0, table.GetInt(r, 12, 0));
                int wRange = Math.Max(0, table.GetInt(r, 13, 0));
                int wArmor = Math.Max(0, table.GetInt(r, 14, 0));
                int wRing = Math.Max(0, table.GetInt(r, 15, 0));
                int wAmulet = Math.Max(0, table.GetInt(r, 16, 0));
                int wBoot = Math.Max(0, table.GetInt(r, 17, 0));
                int wBelt = Math.Max(0, table.GetInt(r, 18, 0));
                int wHelm = Math.Max(0, table.GetInt(r, 19, 0));
                int wCuff = Math.Max(0, table.GetInt(r, 20, 0));
                int wPendant = Math.Max(0, table.GetInt(r, 21, 0));
                int wHorse = Math.Max(0, table.GetInt(r, 22, 0));
                int dropWeight = new[] { wMelee, wRange, wArmor, wRing, wAmulet, wBoot, wBelt, wHelm, wCuff, wPendant, wHorse }.Max();

                string slugPart = HybridLegacyTextDecoder.Slugify(!string.IsNullOrWhiteSpace(rawName) ? rawName : attrCode);
                string code = $"blue-{slugPart}-r{r}";
                registry.AffixCodes.Add(code);

                var doc = new Dictionary<string, object?>
                {
                    ["rowId"] = r,
                    ["affixPool"] = "blueRandom",
                    ["code"] = code,
                    ["attributeId"] = attrId,
                    ["attributeCode"] = attrCode,
                    ["nameTemplate"] = nameTpl,
                    ["isSuffix"] = isSuffix,
                    ["isGoldenOnly"] = false,
                    ["elementSeries"] = series,
                    ["level"] = level,
                    ["dropWeight"] = dropWeight,
                    ["weightsByEquipPart"] = new Dictionary<string, object?>
                    {
                        ["meleeWeapon"] = wMelee,
                        ["rangeWeapon"] = wRange,
                        ["armor"] = wArmor,
                        ["ring"] = wRing,
                        ["amulet"] = wAmulet,
                        ["boot"] = wBoot,
                        ["belt"] = wBelt,
                        ["helm"] = wHelm,
                        ["cuff"] = wCuff,
                        ["pendant"] = wPendant,
                        ["horse"] = wHorse,
                    },
                    ["param0Min"] = p0Min,
                    ["param0Max"] = p0Max,
                    ["param1Min"] = p1Min,
                    ["param1Max"] = p1Max,
                    ["param2Min"] = p2Min,
                    ["param2Max"] = p2Max,
                };
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    doc["description"] = desc;
                }

                affixDocs.Add(doc);
            }
        }

        // 1b. purpleEnchase: magicattriblevel.txt
        int legacyPurpleCount = 0;
        string? purplePath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\magicattriblevel.txt");
        if (purplePath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(purplePath);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                legacyPurpleCount++;
                string rawName = table.GetString(r, 0);
                bool isSuffix = table.GetInt(r, 1, 1) == 0;
                int series = ClampAffixSeries(ReadOptionalSeries(table, r, 2));
                int level = Math.Clamp(table.GetInt(r, 3, 1), 1, 20);
                int attrId = Math.Max(0, table.GetInt(r, 4, 0));
                string attrCode = magicReg.GetCode(attrId);
                string nameTpl = magicReg.GetNameTemplate(attrId, !string.IsNullOrWhiteSpace(rawName) ? rawName : attrCode);

                int p0Min = table.GetInt(r, 5, 0);
                int p0Max = table.GetInt(r, 6, p0Min);
                int p1Min = table.GetInt(r, 7, -1);
                int p1Max = table.GetInt(r, 8, p1Min);
                int p2Min = table.GetInt(r, 9, 0);
                int p2Max = table.GetInt(r, 10, p2Min);
                int baseVal = Math.Max(0, table.GetInt(r, 11, 0));

                var partWeights = new Dictionary<string, object?>
                {
                    ["sword"] = Math.Max(0, table.GetInt(r, 12, 0)),
                    ["blade"] = Math.Max(0, table.GetInt(r, 13, 0)),
                    ["staff"] = Math.Max(0, table.GetInt(r, 14, 0)),
                    ["spear"] = Math.Max(0, table.GetInt(r, 15, 0)),
                    ["hammer"] = Math.Max(0, table.GetInt(r, 16, 0)),
                    ["dualBlades"] = Math.Max(0, table.GetInt(r, 17, 0)),
                    ["darts"] = Math.Max(0, table.GetInt(r, 18, 0)),
                    ["throwingKnife"] = Math.Max(0, table.GetInt(r, 19, 0)),
                    ["crossbow"] = Math.Max(0, table.GetInt(r, 20, 0)),
                    ["armor"] = Math.Max(0, table.GetInt(r, 21, 0)),
                    ["ring"] = Math.Max(0, table.GetInt(r, 22, 0)),
                    ["necklace"] = Math.Max(0, table.GetInt(r, 23, 0)),
                    ["amulet"] = Math.Max(0, table.GetInt(r, 24, 0)),
                    ["boot"] = Math.Max(0, table.GetInt(r, 25, 0)),
                    ["belt"] = Math.Max(0, table.GetInt(r, 26, 0)),
                    ["helm"] = Math.Max(0, table.GetInt(r, 27, 0)),
                    ["cuff"] = Math.Max(0, table.GetInt(r, 28, 0)),
                    ["sachet"] = Math.Max(0, table.GetInt(r, 29, 0)),
                    ["pendant"] = Math.Max(0, table.GetInt(r, 30, 0)),
                };
                int dropWeight = 0;
                for (int c = 12; c <= 30; c++)
                {
                    dropWeight = Math.Max(dropWeight, table.GetInt(r, c, 0));
                }

                string slugPart = HybridLegacyTextDecoder.Slugify(!string.IsNullOrWhiteSpace(rawName) ? rawName : attrCode);
                string code = $"purple-{slugPart}-r{r}";
                registry.AffixCodes.Add(code);

                affixDocs.Add(new Dictionary<string, object?>
                {
                    ["rowId"] = r,
                    ["affixPool"] = "purpleEnchase",
                    ["code"] = code,
                    ["attributeId"] = attrId,
                    ["attributeCode"] = attrCode,
                    ["nameTemplate"] = nameTpl,
                    ["isSuffix"] = isSuffix,
                    ["isGoldenOnly"] = false,
                    ["elementSeries"] = series,
                    ["level"] = level,
                    ["baseValue"] = baseVal,
                    ["dropWeight"] = dropWeight,
                    ["weightsByEquipPart"] = partWeights,
                    ["weightsBySeries"] = new Dictionary<string, object?>
                    {
                        ["metal"] = Math.Max(0, table.GetInt(r, 31, 100)),
                        ["wood"] = Math.Max(0, table.GetInt(r, 32, 100)),
                        ["water"] = Math.Max(0, table.GetInt(r, 33, 100)),
                        ["fire"] = Math.Max(0, table.GetInt(r, 34, 100)),
                        ["earth"] = Math.Max(0, table.GetInt(r, 35, 100)),
                    },
                    ["param0Min"] = p0Min,
                    ["param0Max"] = p0Max,
                    ["param1Min"] = p1Min,
                    ["param1Max"] = p1Max,
                    ["param2Min"] = p2Min,
                    ["param2Max"] = p2Max,
                });
            }
        }

        // 1c. goldenFixed: magicattrib_ge.txt
        int legacyGoldAffixCount = 0;
        string? gePath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\magicattrib_ge.txt");
        if (gePath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(gePath);
            legacyGoldAffixCount = table.DataRowCount;
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                string rawName = table.GetString(r, 0, preferVietnamese: false);
                bool isSuffix = table.GetInt(r, 1, 1) == 0;
                int series = ClampAffixSeries(ReadOptionalSeries(table, r, 2));
                int level = Math.Clamp(table.GetInt(r, 3, 10), 1, 20);
                int attrId = Math.Max(0, table.GetInt(r, 4, 0));
                string attrCode = magicReg.GetCode(attrId);
                string nameTpl = magicReg.GetNameTemplate(attrId, !string.IsNullOrWhiteSpace(rawName) ? rawName : attrCode);

                int p0Min = table.GetInt(r, 5, 0);
                int p0Max = table.GetInt(r, 6, p0Min);
                int p1Min = table.GetInt(r, 7, -1);
                int p1Max = table.GetInt(r, 8, p1Min);
                int p2Min = table.GetInt(r, 9, 0);
                int p2Max = table.GetInt(r, 10, p2Min);

                string slugPart = HybridLegacyTextDecoder.Slugify(attrCode);
                string code = $"gold-{slugPart}-r{r}";
                registry.AffixCodes.Add(code);
                registry.GoldenAffixByRowId[r] = (code, attrId, attrCode, p0Min, p0Max, p1Min, p1Max, p2Min, p2Max);

                affixDocs.Add(new Dictionary<string, object?>
                {
                    ["rowId"] = r,
                    ["affixPool"] = "goldenFixed",
                    ["code"] = code,
                    ["attributeId"] = attrId,
                    ["attributeCode"] = attrCode,
                    ["nameTemplate"] = nameTpl,
                    ["isSuffix"] = isSuffix,
                    ["isGoldenOnly"] = true,
                    ["elementSeries"] = series,
                    ["level"] = level,
                    ["dropWeight"] = 0,
                    ["param0Min"] = p0Min,
                    ["param0Max"] = p0Max,
                    ["param1Min"] = p1Min,
                    ["param1Max"] = p1Max,
                    ["param2Min"] = p2Min,
                    ["param2Max"] = p2Max,
                });
            }
        }

        LegacyPathResolver.WriteCanonicalJson(
            Path.Combine(itemsOutDir, "magic-affixes.items.json"),
            new Dictionary<string, object?>
            {
                ["$schema"] = "../../schemas/item-catalog.schema.json",
                ["schemaVersion"] = "1.0.0",
                ["magicAffixes"] = affixDocs,
            });

        registry.RecordCount("affixes.total", legacyBlueCount + legacyPurpleCount + legacyGoldAffixCount, affixDocs.Count);
        return affixDocs.Count;
    }

    private static int ExportSetsObjectsAndRecipes(
        string serverRootPath,
        string itemsOutDir,
        Dictionary<int, string> suiteFirstItemName,
        Dictionary<int, string> extSuiteFirstItemName,
        ExportCatalogRegistry registry)
    {
        var equipSets = new List<Dictionary<string, object?>>();

        // suite_activate_count.txt
        int legacySuiteRows = 0;
        string? suitePath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\suite_activate_count.txt");
        if (suitePath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(suitePath);
            var rawSuites = new List<(int Id, string Name, int Pieces)>();
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                int suiteId = table.GetInt(r, 0, 0);
                int pieces = table.GetInt(r, 1, 0);
                if (suiteId <= 0 || pieces <= 0)
                {
                    continue;
                }

                legacySuiteRows++;
                string name = suiteFirstItemName.TryGetValue(suiteId, out string? firstItem)
                    ? $"Bộ {ExtractSuitePrefix(firstItem, suiteId)}"
                    : $"Bộ Hoàng Kim #{suiteId}";
                rawSuites.Add((suiteId, name, Math.Clamp(pieces, 1, 15)));
            }

            string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(rawSuites, x => x.Name, x => x.Id);
            for (int i = 0; i < rawSuites.Count; i++)
            {
                var s = rawSuites[i];
                equipSets.Add(new Dictionary<string, object?>
                {
                    ["id"] = s.Id,
                    ["code"] = codes[i],
                    ["name"] = s.Name,
                    ["isExtendedSuite"] = false,
                    ["pieceCount"] = s.Pieces,
                    ["activationThresholds"] = new List<Dictionary<string, object?>>
                    {
                        new()
                        {
                            ["equippedPieces"] = s.Pieces,
                            ["unlockedHiddenSlots"] = 3,
                        },
                    },
                });
            }
        }

        // ext_suite_activate_count.txt
        int legacyExtSuiteRows = 0;
        string? extSuitePath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\ext_suite_activate_count.txt");
        if (extSuitePath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(extSuitePath);
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                int suiteId = table.GetInt(r, 0, 0);
                int c1 = table.GetInt(r, 1, 0);
                int c2 = table.GetInt(r, 2, 0);
                if (suiteId <= 0 || (c1 <= 0 && c2 <= 0))
                {
                    continue;
                }

                legacyExtSuiteRows++;
                int maxPieces = Math.Clamp(Math.Max(c1, c2), 1, 15);
                string name = extSuiteFirstItemName.TryGetValue(suiteId, out string? firstItem)
                    ? $"Bộ Mở Rộng {ExtractSuitePrefix(firstItem, suiteId)}"
                    : $"Bộ Mở Rộng #{suiteId}";
                string slug = HybridLegacyTextDecoder.Slugify(name, "ext-suite");
                string code = $"ext-{slug}-{suiteId}";

                var thresholds = new List<Dictionary<string, object?>>();
                if (c1 > 0)
                {
                    thresholds.Add(new Dictionary<string, object?> { ["equippedPieces"] = c1, ["unlockedHiddenSlots"] = 1 });
                }

                if (c2 > 0 && c2 != c1)
                {
                    thresholds.Add(new Dictionary<string, object?> { ["equippedPieces"] = c2, ["unlockedHiddenSlots"] = 2 });
                }

                equipSets.Add(new Dictionary<string, object?>
                {
                    ["id"] = suiteId,
                    ["code"] = code,
                    ["name"] = name,
                    ["isExtendedSuite"] = true,
                    ["pieceCount"] = maxPieces,
                    ["activationThresholds"] = thresholds,
                });
            }
        }

        // Ground Objects: settings/obj/ObjData.txt
        var groundObjs = new List<Dictionary<string, object?>>();
        int legacyObjRows = 0;
        string? objPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\obj\ObjData.txt")
            ?? LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\objdata.txt");
        if (objPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(objPath);
            var rawObjs = new List<(int Id, string Name, string Kind, int LifeFrames, int ObstacleKind, string ScriptPath)>();
            var seenObjIds = new HashSet<int>();
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                int id = table.GetInt(r, 1, 0);
                if (id < 1 || !seenObjIds.Add(id))
                {
                    continue;
                }

                legacyObjRows++;
                string name = NonEmptyName(table.GetString(r, 0), "Vật thể", id);
                string kind = MapGroundObjectKind(table.GetString(r, 2, preferVietnamese: false));
                string scriptPath = table.GetEnginePath(r, 3);
                int lifeFrames = Math.Max(0, table.GetInt(r, 6, 0));
                int obsKind = Math.Max(0, table.GetInt(r, 52, 0));
                rawObjs.Add((id, name, kind, lifeFrames, obsKind, scriptPath));
            }

            string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(rawObjs, x => x.Name, x => x.Id);
            for (int i = 0; i < rawObjs.Count; i++)
            {
                var o = rawObjs[i];
                registry.ObjCodeById[o.Id] = codes[i];
                var objDoc = new Dictionary<string, object?>
                {
                    ["id"] = o.Id,
                    ["code"] = codes[i],
                    ["name"] = o.Name,
                    ["kind"] = o.Kind,
                    ["lifeFrames"] = o.LifeFrames,
                    ["obstacleKind"] = o.ObstacleKind,
                };
                if (!string.IsNullOrWhiteSpace(o.ScriptPath))
                {
                    objDoc["scriptPath"] = o.ScriptPath;
                }

                groundObjs.Add(objDoc);
            }
        }

        // Crafting Recipes: settings/item/004/atlas_compound.txt
        var recipes = new List<Dictionary<string, object?>>();
        int legacyRecipeRows = 0;
        string? atlasPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\atlas_compound.txt");
        if (atlasPath is not null)
        {
            KTabFileTable table = KTabFileTable.Load(atlasPath);
            var rawRecipes = new List<(int Id, string Name, string? AtlasCode, Dictionary<string, object?> OutputItem, List<Dictionary<string, object?>> Ingredients)>();
            for (int r = 1; r <= table.DataRowCount; r++)
            {
                if (table.IsRowCompletelyBlankOrTabsOnly(r))
                {
                    continue;
                }

                legacyRecipeRows++;
                string name = NonEmptyName(table.GetString(r, 0), "Đồ phổ Hoàng Kim", r);
                int atlasGenre = Math.Max(0, table.GetInt(r, 1, 6));
                int atlasDetail = Math.Max(0, table.GetInt(r, 2, 1));
                int atlasPart = Math.Max(0, table.GetInt(r, 3, 0));
                string? atlasCode = TryLookupItemCode(registry, atlasGenre, atlasDetail, atlasPart, level: 1);

                var ingredients = new List<Dictionary<string, object?>>();
                for (int k = 0; k < 6; k++)
                {
                    int bc = 4 + (k * 7);
                    if (table.GetRawCell(r, bc).IsEmpty && table.GetRawCell(r, bc + 1).IsEmpty)
                    {
                        continue;
                    }

                    int iGenre = Math.Max(0, table.GetInt(r, bc + 1, 6));
                    int iDetail = Math.Max(0, table.GetInt(r, bc + 2, 1));
                    int iPart = Math.Max(0, table.GetInt(r, bc + 3, 0));
                    int iLvl = Math.Max(0, table.GetInt(r, bc + 4, 0));
                    int iSer = ClampAffixSeries(ReadOptionalSeries(table, r, bc + 5));
                    int iMag = Math.Max(0, table.GetInt(r, bc + 6, 0));
                    string? iCode = TryLookupItemCode(registry, iGenre, iDetail, iPart, iLvl > 0 ? iLvl : 1);

                    var ing = new Dictionary<string, object?>
                    {
                        ["genre"] = iGenre,
                        ["detailType"] = iDetail,
                        ["particularType"] = iPart,
                        ["count"] = 1,
                    };
                    if (iCode is not null)
                    {
                        ing["itemCode"] = iCode;
                    }

                    if (iLvl > 0)
                    {
                        ing["minLevel"] = iLvl;
                    }

                    if (iSer >= 0)
                    {
                        ing["elementSeries"] = iSer;
                    }

                    if (iMag > 0)
                    {
                        ing["requiredMagicId"] = iMag;
                    }

                    ingredients.Add(ing);
                }

                int desQuality = Math.Clamp(table.GetInt(r, 47, 1), 0, 3);
                int desGenre = Math.Max(0, table.GetInt(r, 48, 0));
                int desDetail = Math.Max(0, table.GetInt(r, 49, 0));
                int desPart = Math.Max(0, table.GetInt(r, 50, 0));
                int desLevel = Math.Max(0, table.GetInt(r, 51, 0));
                int desSeries = ClampAffixSeries(ReadOptionalSeries(table, r, 52));

                string? outItemCode = null;
                if (desQuality == 1 && registry.GoldenCodeByRowId.TryGetValue(desDetail, out string? gCode))
                {
                    outItemCode = gCode;
                }
                else if (desQuality == 3 && registry.PlatinaCodeByRowId.TryGetValue(desDetail, out string? pCode))
                {
                    outItemCode = pCode;
                }
                else
                {
                    outItemCode = TryLookupItemCode(registry, desGenre, desDetail, desPart, desLevel > 0 ? desLevel : 1);
                }

                var outItem = new Dictionary<string, object?>
                {
                    ["quality"] = desQuality,
                    ["genre"] = desGenre,
                    ["detailType"] = desDetail,
                    ["particularType"] = desPart,
                };
                if (outItemCode is not null)
                {
                    outItem["itemCode"] = outItemCode;
                }

                if (desLevel > 0)
                {
                    outItem["level"] = desLevel;
                }

                if (desSeries >= 0)
                {
                    outItem["elementSeries"] = desSeries;
                }

                rawRecipes.Add((r, name, atlasCode, outItem, ingredients));
            }

            string[] codes = HybridLegacyTextDecoder.AssignSymmetricCodes(rawRecipes, x => x.Name, x => x.Id);
            for (int i = 0; i < rawRecipes.Count; i++)
            {
                var rec = rawRecipes[i];
                var recDoc = new Dictionary<string, object?>
                {
                    ["id"] = rec.Id,
                    ["code"] = codes[i],
                    ["name"] = rec.Name,
                    ["recipeKind"] = "atlasCompound",
                    ["outputItem"] = rec.OutputItem,
                    ["ingredients"] = rec.Ingredients,
                };
                if (rec.AtlasCode is not null)
                {
                    recDoc["atlasItemCode"] = rec.AtlasCode;
                }

                recipes.Add(recDoc);
            }
        }

        LegacyPathResolver.WriteCanonicalJson(
            Path.Combine(itemsOutDir, "sets-objects-recipes.items.json"),
            new Dictionary<string, object?>
            {
                ["$schema"] = "../../schemas/item-catalog.schema.json",
                ["schemaVersion"] = "1.0.0",
                ["equipmentSets"] = equipSets,
                ["groundObjects"] = groundObjs,
                ["craftingRecipes"] = recipes,
            });

        registry.RecordCount("items.equipmentSets", legacySuiteRows + legacyExtSuiteRows, equipSets.Count);
        registry.RecordCount("items.groundObjects", legacyObjRows, groundObjs.Count);
        registry.RecordCount("items.craftingRecipes", legacyRecipeRows, recipes.Count);

        return groundObjs.Count;
    }

    private static (int GroupKind, int SourceIdx, int RowId, string Name, int Genre, int Quality, int Detail, int Particular, int Level, int Series, Dictionary<string, object?> Props)
        ParseStandardEquipRow(
            KTabFileTable table,
            int r,
            int groupKind,
            int sourceIdx,
            int quality,
            bool isMask,
            MagicAttributeRegistry magicReg,
            List<(int Genre, int Quality, int Detail, int Particular, bool NoMove, bool NoThrow, bool NoTrade, bool NoSell, bool NoDrop)> forbidRules)
    {
        string fallbackPrefix = quality == 1 ? "Trang bị Hoàng Kim" : (quality == 3 ? "Trang bị Bạch Kim" : "Trang bị");
        string name = NonEmptyName(table.GetString(r, 0), fallbackPrefix, r);
        int genre = Math.Clamp(table.GetInt(r, 1, 0), 0, 8);
        int detail = Math.Max(0, table.GetInt(r, 2, 0));
        int particular = Math.Max(0, table.GetInt(r, 3, 0));
        int objId = Math.Max(0, table.GetInt(r, 5, 0));
        int w = Math.Clamp(table.GetInt(r, 6, 1), 1, 6);
        int h = Math.Clamp(table.GetInt(r, 7, 1), 1, 10);
        string desc = table.GetString(r, 8);
        int series = ReadOptionalSeries(table, r, 9);
        int price = Math.Max(0, table.GetInt(r, 10, 0));
        int level = Math.Clamp(table.GetInt(r, 11, 1), 0, 30);
        int canStack = table.GetInt(r, 12, 0);

        var props = BuildBaseItemDict(r, name, desc, genre, quality, detail, particular, level, series, w, h, objId, price);
        if (quality == 0 && detail <= 9)
        {
            props["maxEnchaseSockets"] = 6;
        }

        if (canStack > 0)
        {
            props["maxStackCount"] = Math.Max(1, canStack == 1 ? 50 : canStack);
        }

        if (isMask && table.GetRawCell(r, 13).IsEmpty)
        {
            int maskNpcId = table.GetInt(r, 14, 0);
            if (maskNpcId > 0)
            {
                props["npcMaskTemplateId"] = maskNpcId;
            }
        }

        var baseAttrs = new List<Dictionary<string, object?>>();
        for (int k = 0; k < 7; k++)
        {
            int col = 13 + (k * 3);
            if (table.GetRawCell(r, col).IsEmpty)
            {
                continue;
            }

            int attrId = table.GetInt(r, col, -1);
            if (attrId < 0)
            {
                continue;
            }

            int minVal = table.GetInt(r, col + 1, 0);
            int maxVal = table.GetInt(r, col + 2, minVal);
            baseAttrs.Add(new Dictionary<string, object?>
            {
                ["attributeId"] = attrId,
                ["attributeCode"] = magicReg.GetCode(attrId),
                ["minValue"] = minVal,
                ["maxValue"] = maxVal,
            });
        }

        if (baseAttrs.Count > 0)
        {
            props["baseAttributes"] = baseAttrs;
        }

        var reqs = new List<Dictionary<string, object?>>();
        for (int k = 0; k < 6; k++)
        {
            int col = 34 + (k * 2);
            if (table.GetRawCell(r, col).IsEmpty)
            {
                continue;
            }

            int reqType = table.GetInt(r, col, -1);
            if (reqType < 0)
            {
                continue;
            }

            int minVal = table.GetInt(r, col + 1, 0);
            reqs.Add(new Dictionary<string, object?>
            {
                ["requirementType"] = reqType,
                ["requirementCode"] = MapRequirementCode(reqType),
                ["minValue"] = minVal,
            });
        }

        if (reqs.Count > 0)
        {
            props["requirements"] = reqs;
        }

        ApplyForbidRestrictions(props, genre, quality, detail, particular, forbidRules);
        return (groupKind, sourceIdx, r, name, genre, quality, detail, particular, level, series, props);
    }

    private static void AssignGlobalSemanticItemCodes(
        List<(int GroupKind, int SourceIdx, int RowId, string Name, int Genre, int Quality, int Detail, int Particular, int Level, int Series, Dictionary<string, object?> Props)> entries,
        ExportCatalogRegistry registry)
    {
        var baseSlugs = new string[entries.Count];
        var indicesByBaseSlug = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++)
        {
            string slug = HybridLegacyTextDecoder.Slugify(entries[i].Name, "item");
            baseSlugs[i] = slug;
            if (!indicesByBaseSlug.TryGetValue(slug, out List<int>? list))
            {
                list = [];
                indicesByBaseSlug[slug] = list;
            }

            list.Add(i);
        }

        var candidates = new string[entries.Count];
        foreach (var (baseSlug, groupIndices) in indicesByBaseSlug)
        {
            if (groupIndices.Count == 1)
            {
                candidates[groupIndices[0]] = baseSlug;
                continue;
            }

            bool variesByQuality = groupIndices.Select(idx => entries[idx].Quality).Distinct().Count() > 1;
            bool variesByLevel = groupIndices.Select(idx => entries[idx].Level).Distinct().Count() > 1;
            bool variesBySeries = groupIndices.Select(idx => entries[idx].Series).Distinct().Count() > 1;

            foreach (int idx in groupIndices)
            {
                var e = entries[idx];
                var sb = new StringBuilder(baseSlug);
                if (variesByQuality)
                {
                    sb.Append(e.Quality switch
                    {
                        1 => "-hk",
                        3 => "-bk",
                        4 => "-hong",
                        _ => string.Empty,
                    });
                }

                if (variesByLevel)
                {
                    sb.Append(CultureInfo.InvariantCulture, $"-lv{e.Level}");
                }

                if (variesBySeries && e.Series is >= 0 and <= 4)
                {
                    sb.Append('-').Append(SeriesSlug(e.Series));
                }

                candidates[idx] = sb.ToString();
            }
        }

        // Second symmetric pass: if any candidates STILL collide, disambiguate ALL colliding members symmetrically
        var candFreq = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < candidates.Length; i++)
        {
            candFreq[candidates[i]] = candFreq.GetValueOrDefault(candidates[i], 0) + 1;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (candFreq[candidates[i]] <= 1)
            {
                continue;
            }

            var e = entries[i];
            candidates[i] = e.Quality switch
            {
                1 => $"{baseSlugs[i]}-hk{e.RowId}",
                3 => $"{baseSlugs[i]}-bk{e.RowId}",
                _ => $"{candidates[i]}-g{e.Genre}-d{e.Detail}-p{e.Particular}-r{e.RowId}",
            };
        }

        // Final guarantee against cross-table identical (g,d,p,r)
        var finalFreq = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < candidates.Length; i++)
        {
            finalFreq[candidates[i]] = finalFreq.GetValueOrDefault(candidates[i], 0) + 1;
        }

        var usedCodes = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            string code = finalFreq[candidates[i]] > 1
                ? $"{candidates[i]}-s{e.SourceIdx}"
                : candidates[i];
            if (!usedCodes.Add(code))
            {
                code = $"{code}-{i + 1}";
                usedCodes.Add(code);
            }

            e.Props["code"] = code;
            registry.ItemCodes.Add(code);

            if (e.Quality == 1)
            {
                registry.GoldenCodeByRowId[e.RowId] = code;
            }
            else if (e.Quality == 3)
            {
                registry.PlatinaCodeByRowId[e.RowId] = code;
            }
            else if (e.Quality == 0)
            {
                registry.ItemCodeByTupleAndLevel.TryAdd((e.Genre, e.Detail, e.Particular, e.Level), code);
                registry.PrimaryItemCodeByTuple.TryAdd((e.Genre, e.Detail, e.Particular), code);
            }
        }
    }

    private static Dictionary<string, object?> BuildBaseItemDict(
        int rowId,
        string name,
        string description,
        int genre,
        int quality,
        int detail,
        int particular,
        int level,
        int series,
        int width,
        int height,
        int objDataId,
        int priceGold)
    {
        var d = new Dictionary<string, object?>
        {
            ["rowId"] = rowId,
            ["code"] = string.Empty, // Filled during global symmetric assignment
            ["name"] = name,
            ["genre"] = genre,
            ["quality"] = quality,
            ["detailType"] = detail,
            ["particularType"] = particular,
            ["level"] = level,
            ["elementSeries"] = series,
            ["gridWidthCells"] = width,
            ["gridHeightCells"] = height,
            ["objDataId"] = objDataId,
            ["priceGold"] = priceGold,
        };
        if (!string.IsNullOrWhiteSpace(description))
        {
            d["description"] = description;
        }

        return d;
    }

    private static Dictionary<string, object?> BuildFixedAffixEntry(
        int affixRowId,
        int level10AffixRowId,
        bool isHidden,
        ExportCatalogRegistry registry)
    {
        var d = new Dictionary<string, object?>
        {
            ["affixRowId"] = affixRowId,
            ["isHiddenSeriesSlot"] = isHidden,
        };

        if (affixRowId > 0 && registry.GoldenAffixByRowId.TryGetValue(affixRowId, out var a0))
        {
            d["affixCode"] = a0.Code;
            d["attributeId"] = a0.AttrId;
            d["attributeCode"] = a0.AttrCode;
            d["param0Min"] = a0.P0Min;
            d["param0Max"] = a0.P0Max;
            d["param1Min"] = a0.P1Min;
            d["param1Max"] = a0.P1Max;
            d["param2Min"] = a0.P2Min;
            d["param2Max"] = a0.P2Max;
        }

        if (level10AffixRowId > 0)
        {
            d["level10AffixRowId"] = level10AffixRowId;
            if (registry.GoldenAffixByRowId.TryGetValue(level10AffixRowId, out var a10))
            {
                if (!d.ContainsKey("attributeId"))
                {
                    d["affixCode"] = a10.Code;
                    d["attributeId"] = a10.AttrId;
                    d["attributeCode"] = a10.AttrCode;
                }

                d["level10Param0Min"] = a10.P0Min;
                d["level10Param0Max"] = a10.P0Max;
                d["level10Param1Min"] = a10.P1Min;
                d["level10Param1Max"] = a10.P1Max;
                d["level10Param2Min"] = a10.P2Min;
                d["level10Param2Max"] = a10.P2Max;
            }
        }

        return d;
    }

    private static void AddSuiteAndSmelting(
        Dictionary<string, object?> props,
        int suiteId,
        int extSuiteId,
        int extPieceIdx,
        int extHid1,
        int extHid2,
        int smeltCount,
        int smeltQuality)
    {
        if (suiteId > 0 || extSuiteId > 0)
        {
            var sb = new Dictionary<string, object?>();
            if (suiteId > 0)
            {
                sb["suiteId"] = suiteId;
            }

            if (extSuiteId > 0)
            {
                sb["extSuiteId"] = extSuiteId;
                sb["extSuitePieceIndex"] = Math.Max(0, extPieceIdx);
                var hid = new List<int>();
                if (extHid1 > 0)
                {
                    hid.Add(extHid1);
                }

                if (extHid2 > 0)
                {
                    hid.Add(extHid2);
                }

                if (hid.Count > 0)
                {
                    sb["extHiddenAffixIds"] = hid;
                }
            }

            props["suiteBinding"] = sb;
        }

        if (smeltCount > 0 || smeltQuality > 0)
        {
            props["smeltingRules"] = new Dictionary<string, object?>
            {
                ["smeltableAffixCount"] = Math.Max(0, smeltCount),
                ["smeltableFusionQuality"] = Math.Max(0, smeltQuality),
            };
        }
    }

    private static Dictionary<int, int> LoadQuestKeyStacks(string serverRootPath)
    {
        var map = new Dictionary<int, int>();
        string? path = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\questkey_stack.txt");
        if (path is null)
        {
            return map;
        }

        KTabFileTable table = KTabFileTable.Load(path);
        for (int r = 1; r <= table.DataRowCount; r++)
        {
            int rowId = table.GetInt(r, 0, 0);
            int maxStack = table.GetInt(r, 1, 0);
            if (rowId > 0 && maxStack > 0)
            {
                map[rowId] = maxStack;
            }
        }

        return map;
    }

    private static Dictionary<int, (int MaxStack, bool SameLvl, bool SameSeries, bool SameLuck, bool[] SameMagic)> LoadMagicScriptStacks(string serverRootPath)
    {
        var map = new Dictionary<int, (int, bool, bool, bool, bool[])>();
        string? path = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\magicscript_stack.txt");
        if (path is null)
        {
            return map;
        }

        KTabFileTable table = KTabFileTable.Load(path);
        for (int r = 1; r <= table.DataRowCount; r++)
        {
            int zeroBasedIdx = table.GetInt(r, 0, -1);
            int maxStack = table.GetInt(r, 1, 0);
            if (zeroBasedIdx < 0 || maxStack <= 0)
            {
                continue;
            }

            var mag = new bool[6];
            for (int m = 0; m < 6; m++)
            {
                mag[m] = table.GetInt(r, 5 + m, 0) != 0;
            }

            map[zeroBasedIdx] = (
                maxStack,
                table.GetInt(r, 2, 0) != 0,
                table.GetInt(r, 3, 0) != 0,
                table.GetInt(r, 4, 0) != 0,
                mag);
        }

        return map;
    }

    private static List<(int Genre, int Quality, int Detail, int Particular, bool NoMove, bool NoThrow, bool NoTrade, bool NoSell, bool NoDrop)> LoadForbidItemRules(string serverRootPath)
    {
        var list = new List<(int, int, int, int, bool, bool, bool, bool, bool)>();
        string? path = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\forbititem.ini")
            ?? LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\item\004\forbititem.ini");
        if (path is null)
        {
            return list;
        }

        var sections = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int>? cur = null;
        foreach (string rawLine in File.ReadAllLines(path, Encoding.Latin1))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                string sec = line[1..^1].Trim();
                cur = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                sections[sec] = cur;
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq > 0 && cur is not null && int.TryParse(line[(eq + 1)..].Trim(), out int v))
            {
                cur[line[..eq].Trim()] = v;
            }
        }

        foreach (var (secName, kv) in sections)
        {
            if (!secName.StartsWith("Item_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            list.Add((
                kv.GetValueOrDefault("Genre", -1),
                kv.GetValueOrDefault("Quality", 0),
                kv.GetValueOrDefault("DetailType", -1),
                kv.GetValueOrDefault("ParticulType", -1),
                kv.GetValueOrDefault("ForbitMove", 0) != 0,
                kv.GetValueOrDefault("ForbitThrowAway", 0) != 0,
                kv.GetValueOrDefault("ForbitTrade", 0) != 0,
                kv.GetValueOrDefault("ForbitSell", 0) != 0,
                kv.GetValueOrDefault("ForbitDrop", 0) != 0));
        }

        return list;
    }

    private static void ApplyForbidRestrictions(
        Dictionary<string, object?> props,
        int genre,
        int quality,
        int detail,
        int particular,
        List<(int Genre, int Quality, int Detail, int Particular, bool NoMove, bool NoThrow, bool NoTrade, bool NoSell, bool NoDrop)> rules)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            if (r.Genre != genre || r.Quality != quality)
            {
                continue;
            }

            int matchDetail = quality is 1 or 3 ? (int)props["rowId"]! : detail;
            if (r.Detail != -1 && r.Detail != matchDetail)
            {
                continue;
            }

            if (r.Particular != -1 && r.Particular != particular)
            {
                continue;
            }

            if (r.NoMove || r.NoThrow || r.NoTrade || r.NoSell || r.NoDrop)
            {
                props["restrictions"] = new Dictionary<string, object?>
                {
                    ["forbidMove"] = r.NoMove,
                    ["forbidThrowAway"] = r.NoThrow,
                    ["forbidTrade"] = r.NoTrade,
                    ["forbidSell"] = r.NoSell,
                    ["forbidDropOnDeath"] = r.NoDrop,
                };
            }

            return;
        }
    }

    private static string? TryLookupItemCode(ExportCatalogRegistry registry, int genre, int detail, int particular, int level)
    {
        if (registry.ItemCodeByTupleAndLevel.TryGetValue((genre, detail, particular, level), out string? exact))
        {
            return exact;
        }

        return registry.PrimaryItemCodeByTuple.TryGetValue((genre, detail, particular), out string? primary) ? primary : null;
    }

    private static int ReadOptionalSeries(KTabFileTable table, int r, int col) =>
        table.GetRawCell(r, col).IsEmpty ? -1 : Math.Clamp(table.GetInt(r, col, -1), -1, 6);

    private static int ClampAffixSeries(int series) =>
        series is >= 0 and <= 4 ? series : -1;

    private static string NonEmptyName(string decoded, string fallbackPrefix, int idOrRow) =>
        string.IsNullOrWhiteSpace(decoded) ? $"{fallbackPrefix} #{idOrRow}" : decoded;

    private static string SeriesSlug(int series) =>
        series switch
        {
            0 => "kim",
            1 => "moc",
            2 => "thuy",
            3 => "hoa",
            4 => "tho",
            _ => "vo",
        };

    private static string MapRequirementCode(int reqType) =>
        reqType switch
        {
            32 => "strength",
            33 => "dexterity",
            34 => "vitality",
            35 => "energy",
            36 => "level",
            37 => "series",
            38 => "gender",
            39 => "faction",
            40 => "transLife",
            _ => "custom",
        };

    private static string MapGroundObjectKind(string rawKind)
    {
        string k = rawKind.Trim();
        if (k.Contains("Item", StringComparison.OrdinalIgnoreCase))
        {
            return "Item";
        }

        if (k.Contains("Money", StringComparison.OrdinalIgnoreCase))
        {
            return "Money";
        }

        if (k.Contains("Box", StringComparison.OrdinalIgnoreCase))
        {
            return "Box";
        }

        if (k.Contains("Switch", StringComparison.OrdinalIgnoreCase))
        {
            return "Switch";
        }

        if (k.Contains("Door", StringComparison.OrdinalIgnoreCase))
        {
            return "Door";
        }

        if (k.Contains("Trap", StringComparison.OrdinalIgnoreCase))
        {
            return "TrapObj";
        }

        if (k.Contains("Obstacle", StringComparison.OrdinalIgnoreCase))
        {
            return "Obstacle";
        }

        if (k.Contains("Body", StringComparison.OrdinalIgnoreCase))
        {
            return "Body";
        }

        if (k.Contains("LoopSound", StringComparison.OrdinalIgnoreCase))
        {
            return "LoopSound";
        }

        if (k.Contains("Sound", StringComparison.OrdinalIgnoreCase))
        {
            return "RandSound";
        }

        return "Prop";
    }

    private static string ExtractSuitePrefix(string itemName, int suiteId)
    {
        string cleaned = itemName.Trim().TrimStart('[');
        int closeBracket = cleaned.IndexOf(']');
        if (closeBracket >= 0 && closeBracket + 1 < cleaned.Length)
        {
            cleaned = cleaned[(closeBracket + 1)..].Trim();
        }

        string[] words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
        {
            return $"{words[0]} {words[1]}";
        }

        return words.Length == 1 ? words[0] : $"#{suiteId}";
    }
}
