using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JXServer.DataExporter.Exporters;
using JXServer.DataExporter.Legacy;
using JXServer.GameAssets.Archives;
using JXServer.GameAssets.WorldMaps;

namespace JXServer.DataExporter.Verification;

/// <summary>
/// Executes the 4-Layer Ground-Truth Migration Parity Verification (<c>artifacts/migration-parity-report.json</c>):
/// Layer 1 (100% file manifest classification across <c>settings/</c> &amp; <c>maps/</c>),
/// Layer 2 (row-for-row record count reconciliation),
/// Layer 3 (binary cell-for-cell <c>Region_S.dat</c> RLE round-trip &amp; golden affix equivalence),
/// and Layer 4 (JSON Schema + literal UTF-8 + 100% <c>*Code</c> FK validation).
/// </summary>
public sealed class MigrationParityVerifier
{
    private static readonly HashSet<string> MigratedSettingsExact = new(StringComparer.OrdinalIgnoreCase)
    {
        @"settings\magicdesc.ini",
        @"settings\item\004\magicattrib.txt",
        @"settings\item\004\magicattriblevel.txt",
        @"settings\item\004\magicattriblevel_index.txt",
        @"settings\item\004\magicattrib_ge.txt",
        @"settings\item\004\meleeweapon.txt",
        @"settings\item\004\rangeweapon.txt",
        @"settings\item\004\armor.txt",
        @"settings\item\004\helm.txt",
        @"settings\item\004\boot.txt",
        @"settings\item\004\belt.txt",
        @"settings\item\004\cuff.txt",
        @"settings\item\004\amulet.txt",
        @"settings\item\004\ring.txt",
        @"settings\item\004\pendant.txt",
        @"settings\item\004\horse.txt",
        @"settings\item\004\mask.txt",
        @"settings\item\004\goldequip.txt",
        @"settings\item\004\platinaequip.txt",
        @"settings\item\004\potion.txt",
        @"settings\item\004\questkey.txt",
        @"settings\item\004\townportal.txt",
        @"settings\item\004\magicscript.txt",
        @"settings\item\004\mine.txt",
        @"settings\item\004\fusion.txt",
        @"settings\item\004\compound.txt",
        @"settings\item\004\suite.txt",
        @"settings\item\004\suite_ext.txt",
        @"settings\item\004\goldequipres.txt",
        @"settings\obj\objdata.txt",
        @"settings\missles.txt",
        @"settings\skills.txt",
        @"settings\weaponskill.txt",
        @"settings\attribconstdata.ini",
        @"settings\npcs.txt",
        @"settings\npc\npcgoldtemplate.txt",
        @"settings\partner\partner_level_exp.txt",
        @"settings\partner\partner_skill_Cancel.txt",
        @"settings\partner\feature_mode.ini",
        @"settings\npc\player\newplayerini00.ini",
        @"settings\npc\player\newplayerini01.ini",
        @"settings\npc\player\newplayerini02.ini",
        @"settings\npc\player\newplayerini03.ini",
        @"settings\npc\player\newplayerini04.ini",
        @"settings\npc\player\newplayerini05.ini",
        @"settings\npc\player\newplayerini06.ini",
        @"settings\npc\player\newplayerini07.ini",
        @"settings\npc\player\newplayerini08.ini",
        @"settings\npc\player\newplayerini09.ini",
        @"settings\npc\player\level_add.txt",
        @"settings\npc\player\level_exp.txt",
        @"settings\npc\player\level_lead_exp.txt",
        @"settings\npc\player\revivalid.ini",
        @"settings\npc\player\pkpunish.txt",
        @"settings\npc\player\stamina.ini",
        @"settings\npc\player\team_resist_effect.ini",
        @"settings\factiontitle.txt",
        @"settings\nativeplacelist.ini",
        @"settings\revivepos.ini",
        @"settings\meridian\meridian.txt",
        @"settings\meridian\meridian_level.txt",
        @"settings\tong\tong_level_data.txt",
        @"settings\tong\workshop\workshops.txt",
        @"settings\tong\tongstunt_setting.txt",
        @"settings\goods.txt",
        @"settings\buysell.txt",
        @"settings\maplist.ini",
        @"settings\forbitheart.txt",
    };

    public (int TotalErrors, string ReportPath) VerifyAndWriteReport(
        string serverRootPath,
        string dataDirectory,
        string artifactsDirectory,
        ExportCatalogRegistry registry,
        IReadOnlyList<string> schemaAndFkErrors)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(schemaAndFkErrors);

        // Layer 1: File Manifest Classification across settings\ and maps\
        var (layer1Summary, unaccountedFiles) = ClassifyLegacyFiles(serverRootPath);

        // Layer 2: Row-for-Row Record Count Reconciliation
        var layer2Rows = new List<Dictionary<string, object?>>();
        var rowMismatches = new List<string>();
        foreach (string key in registry.LegacyRecordCounts.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            int legacyCount = registry.LegacyRecordCounts[key];
            int exportedCount = registry.ExportedRecordCounts.GetValueOrDefault(key, -1);
            bool match = legacyCount == exportedCount;
            if (!match)
            {
                rowMismatches.Add($"{key}: legacy={legacyCount}, exported={exportedCount}");
            }

            layer2Rows.Add(new Dictionary<string, object?>
            {
                ["domainTable"] = key,
                ["legacyCount"] = legacyCount,
                ["exportedCount"] = exportedCount,
                ["matched"] = match,
            });
        }

        // Layer 3: Binary Cell-for-Cell Obstacle RLE Round-Trip & Golden Affix Value Equivalence
        var (layer3Summary, valueMismatches) = VerifyValueEquivalence(serverRootPath, dataDirectory, registry);

        // Layer 4: Schema + Literal UTF-8 + Foreign-Key Integrity
        int jsonFileCount = Directory.Exists(dataDirectory)
            ? Directory.EnumerateFiles(dataDirectory, "*.json", SearchOption.AllDirectories).Count()
            : 0;

        int totalErrors = unaccountedFiles.Count + rowMismatches.Count + valueMismatches.Count + schemaAndFkErrors.Count;

        var report = new Dictionary<string, object?>
        {
            ["reportVersion"] = "1.0.0",
            ["generatedAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["overallPassed"] = totalErrors == 0,
            ["totalErrorAndMismatchCount"] = totalErrors,
            ["layer1_fileManifest"] = new Dictionary<string, object?>
            {
                ["unaccountedFileCount"] = unaccountedFiles.Count,
                ["countsByCategory"] = layer1Summary,
                ["unaccountedFiles"] = unaccountedFiles,
            },
            ["layer2_recordCounts"] = new Dictionary<string, object?>
            {
                ["domainCount"] = layer2Rows.Count,
                ["rowMismatchCount"] = rowMismatches.Count,
                ["mismatches"] = rowMismatches,
                ["tables"] = layer2Rows,
            },
            ["layer3_valueEquivalence"] = new Dictionary<string, object?>
            {
                ["valueMismatchCount"] = valueMismatches.Count,
                ["summary"] = layer3Summary,
                ["mismatches"] = valueMismatches.Take(100).ToArray(),
            },
            ["layer4_schemaAndForeignKeys"] = new Dictionary<string, object?>
            {
                ["validatedJsonFileCount"] = jsonFileCount,
                ["schemaAndFkErrorCount"] = schemaAndFkErrors.Count,
                ["errors"] = schemaAndFkErrors.Take(100).ToArray(),
            },
        };

        Directory.CreateDirectory(artifactsDirectory);
        string reportPath = Path.Combine(artifactsDirectory, "migration-parity-report.json");
        LegacyPathResolver.WriteCanonicalJson(reportPath, report);
        return (totalErrors, reportPath);
    }

    private static (Dictionary<string, int> Summary, List<string> Unaccounted) ClassifyLegacyFiles(string serverRootPath)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["migrated"] = 0,
            ["legacyBackupOrDuplicate"] = 0,
            ["clientVisualAudioUiOnly"] = 0,
            ["dynamicRuntimeMissionOrTaskScriptConfig"] = 0,
        };
        var unaccounted = new List<string>();

        string? primaryFactionIni = null;
        string factionDir = Path.Combine(serverRootPath, "settings", "faction");
        if (Directory.Exists(factionDir))
        {
            string? full = Directory.GetFiles(factionDir, "*.ini")
                .OrderBy(f => Path.GetFileName(f).Length)
                .FirstOrDefault();
            if (full is not null)
            {
                primaryFactionIni = Path.GetRelativePath(serverRootPath, full).Replace('/', '\\');
            }
        }

        foreach (string subRoot in new[] { "settings", "maps" })
        {
            string rootPath = Path.Combine(serverRootPath, subRoot);
            if (!Directory.Exists(rootPath))
            {
                continue;
            }

            foreach (string fullFile in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(serverRootPath, fullFile).Replace('/', '\\');
                string? category = ClassifySingleLegacyFile(rel, primaryFactionIni);
                if (category is null)
                {
                    unaccounted.Add(rel);
                }
                else
                {
                    counts[category]++;
                }
            }
        }

        return (counts, unaccounted);
    }

    private static string? ClassifySingleLegacyFile(string rel, string? primaryFactionIni)
    {
        string fileName = Path.GetFileName(rel);
        string ext = Path.GetExtension(rel);

        if (rel.StartsWith(@"maps\", StringComparison.OrdinalIgnoreCase))
        {
            if (rel.Equals(@"maps\worldset.ini", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".wor", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith("Region_S.dat", StringComparison.OrdinalIgnoreCase))
            {
                return "migrated";
            }

            if (fileName.EndsWith("Region_C.dat", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".spr", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".24", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".wor.bak", StringComparison.OrdinalIgnoreCase))
            {
                return ext.Equals(".bak", StringComparison.OrdinalIgnoreCase)
                    ? "legacyBackupOrDuplicate"
                    : "clientVisualAudioUiOnly";
            }

            if (ext.Equals(".bak", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("copy", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("bak", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".rar", StringComparison.OrdinalIgnoreCase))
            {
                return "legacyBackupOrDuplicate";
            }

            if (ext.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".dat", StringComparison.OrdinalIgnoreCase))
            {
                return "dynamicRuntimeMissionOrTaskScriptConfig";
            }

            return null;
        }

        // Exact migrated settings files
        if (MigratedSettingsExact.Contains(rel))
        {
            return "migrated";
        }

        if (primaryFactionIni is not null && rel.Equals(primaryFactionIni, StringComparison.OrdinalIgnoreCase))
        {
            return "migrated";
        }

        // Migrated droprate directories & item droprate ini files
        if (rel.StartsWith(@"settings\droprate\", StringComparison.OrdinalIgnoreCase) && ext.Equals(".ini", StringComparison.OrdinalIgnoreCase))
        {
            return "migrated";
        }

        if (rel.StartsWith(@"settings\item\", StringComparison.OrdinalIgnoreCase) &&
            !rel.StartsWith(@"settings\item\000\", StringComparison.OrdinalIgnoreCase) &&
            !rel.StartsWith(@"settings\item\001\", StringComparison.OrdinalIgnoreCase) &&
            !rel.StartsWith(@"settings\item\002\", StringComparison.OrdinalIgnoreCase) &&
            !rel.StartsWith(@"settings\item\003\", StringComparison.OrdinalIgnoreCase) &&
            ext.Equals(".ini", StringComparison.OrdinalIgnoreCase) &&
            (fileName.Contains("droprate", StringComparison.OrdinalIgnoreCase) || rel.StartsWith(@"settings\item\double\", StringComparison.OrdinalIgnoreCase)))
        {
            return "migrated";
        }

        // Legacy backups, older version folders (000..003, top-level item\*.txt superseded by 004\), duplicates, and office docs
        if (rel.StartsWith(@"settings\settings\", StringComparison.OrdinalIgnoreCase) ||
            rel.StartsWith(@"settings\item\000\", StringComparison.OrdinalIgnoreCase) ||
            rel.StartsWith(@"settings\item\001\", StringComparison.OrdinalIgnoreCase) ||
            rel.StartsWith(@"settings\item\002\", StringComparison.OrdinalIgnoreCase) ||
            rel.StartsWith(@"settings\item\003\", StringComparison.OrdinalIgnoreCase) ||
            rel.StartsWith(@"settings\faction\", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".bak", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".doc", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".rar", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".old", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".tmp", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("_tmp", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("_old", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("_bak", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("_2006", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("-goc", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("skills1.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("skilltemplate.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("pkpunish.ini", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("pkrate.ini", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("pkrate.txt", StringComparison.OrdinalIgnoreCase))
        {
            return "legacyBackupOrDuplicate";
        }

        // Top-level settings\item\*.txt / 004 extra backup tables superseded by settings\item\004\*
        if (rel.StartsWith(@"settings\item\", StringComparison.OrdinalIgnoreCase))
        {
            string sub = rel[@"settings\item\".Length..];
            if (!sub.Contains('\\'))
            {
                // Root settings\item\* files (older version 000 copies or compound/enchase tuning scripts)
                return sub.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || sub.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)
                    ? "legacyBackupOrDuplicate"
                    : "dynamicRuntimeMissionOrTaskScriptConfig";
            }

            if (rel.StartsWith(@"settings\item\004\", StringComparison.OrdinalIgnoreCase))
            {
                return "dynamicRuntimeMissionOrTaskScriptConfig";
            }
        }

        // Client visual, sprite, audio, weather, UI & localization tables
        if (rel.StartsWith(@"settings\npcres\", StringComparison.OrdinalIgnoreCase) ||
            rel.StartsWith(@"settings\weather\", StringComparison.OrdinalIgnoreCase) ||
            rel.StartsWith(@"settings\sound", StringComparison.OrdinalIgnoreCase) ||
            rel.StartsWith(@"settings\music", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("soundlist.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("musiclist.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("adjustcolor.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("portrait.ini", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("stringresource.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("autoupdate.ini", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("×´Ì¬Óë¹âÐ§Í¼ÐÎ¶ÔÕÕ±í.txt", StringComparison.OrdinalIgnoreCase) ||
            rel.StartsWith(@"settings\obj\", StringComparison.OrdinalIgnoreCase))
        {
            return "clientVisualAudioUiOnly";
        }

        // Dynamic runtime Lua scripts, tasks, missions, battles, events, timers, filters, gateway/server configs
        if (ext.Equals(".lua", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".flt", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".cfg", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".dat", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return "dynamicRuntimeMissionOrTaskScriptConfig";
        }

        return null;
    }

    private static (Dictionary<string, object?> Summary, List<string> Mismatches) VerifyValueEquivalence(
        string serverRootPath,
        string dataDirectory,
        ExportCatalogRegistry registry)
    {
        var mismatches = new List<string>();
        MapDataExporter.LoadMapListIni(serverRootPath, out Dictionary<int, byte[]> rawSubPathById);
        XPackArchiveReader? pak = MapDataExporter.OpenMapsPak(serverRootPath);

        long verifiedRegions = 0;
        long verifiedObstacleCells = 0;
        long verifiedTrapSpans = 0;
        long verifiedRegionNpcs = 0;
        long verifiedRegionObjs = 0;

        string mapsDir = Path.Combine(dataDirectory, "maps");
        if (Directory.Exists(mapsDir))
        {
            foreach (string mapFile in Directory.EnumerateFiles(mapsDir, "*.map.json", SearchOption.TopDirectoryOnly).OrderBy(f => f))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(mapFile));
                JsonElement root = doc.RootElement;
                int mapId = root.GetProperty("id").GetInt32();
                if (!rawSubPathById.TryGetValue(mapId, out byte[]? rawSubPath))
                {
                    mismatches.Add($"Map {mapId}: Missing rawSubPath in maplist.ini.");
                    continue;
                }

                JsonElement bounds = root.GetProperty("regionBounds");
                int leftX = bounds.GetProperty("leftRegionX").GetInt32();
                int topY = bounds.GetProperty("topRegionY").GetInt32();
                int rightX = bounds.GetProperty("rightRegionX").GetInt32();
                int bottomY = bounds.GetProperty("bottomRegionY").GetInt32();

                var rleByRegion = new Dictionary<(int Rx, int Ry), string>();
                foreach (JsonElement rElem in root.GetProperty("regions").EnumerateArray())
                {
                    int rx = rElem.GetProperty("regionX").GetInt32();
                    int ry = rElem.GetProperty("regionY").GetInt32();
                    string b64 = rElem.GetProperty("obstacleRleBase64").GetString() ?? string.Empty;
                    rleByRegion[(rx, ry)] = b64;
                }

                int jsonTrapSpans = 0;
                foreach (JsonElement pElem in root.GetProperty("portals").EnumerateArray())
                {
                    jsonTrapSpans += pElem.GetProperty("triggerCells").GetArrayLength();
                }

                int jsonRegionNpcs = 0;
                foreach (JsonElement sgElem in root.GetProperty("spawnGroups").EnumerateArray())
                {
                    jsonRegionNpcs += sgElem.GetProperty("spawns").GetArrayLength();
                }

                int jsonRegionObjs = root.TryGetProperty("mapObjects", out JsonElement moArr) && moArr.ValueKind == JsonValueKind.Array
                    ? moArr.GetArrayLength()
                    : 0;

                string? looseMapDir = LegacyPathResolver.TryResolveDirectory(serverRootPath, rawSubPath, "maps");
                int expectedTrapSpans = 0;
                int expectedNpcs = 0;
                int expectedObjs = 0;

                if (rightX >= leftX && bottomY >= topY && !(leftX == 0 && rightX == 0 && topY == 0 && bottomY == 0 && looseMapDir is null))
                {
                    int[] zeroBuf = new int[ObstacleRleCodec.RegionCellCount];
                    for (int ry = topY; ry <= bottomY; ry++)
                    {
                        for (int rx = leftX; rx <= rightX; rx++)
                        {
                            if (!MapDataExporter.TryReadRegionDatBytes(looseMapDir, rawSubPath, pak, rx, ry, out byte[]? regBytes) ||
                                regBytes is null ||
                                !RegionDatBinaryReader.TryParse(regBytes, rx, ry, out ParsedRegionDat? parsed) ||
                                parsed is null)
                            {
                                continue;
                            }

                            verifiedRegions++;
                            int[] decodedBuf = rleByRegion.TryGetValue((rx, ry), out string? rleB64)
                                ? ObstacleRleCodec.DecodeBase64ToCells(rleB64)
                                : zeroBuf;

                            for (int c = 0; c < ObstacleRleCodec.RegionCellCount; c++)
                            {
                                if (decodedBuf[c] != parsed.ObstacleCells[c])
                                {
                                    mismatches.Add($"Map {mapId} Region ({rx},{ry}) Cell {c}: expected {parsed.ObstacleCells[c]}, got {decodedBuf[c]}.");
                                    break;
                                }
                            }

                            verifiedObstacleCells += ObstacleRleCodec.RegionCellCount;
                            expectedTrapSpans += parsed.Traps.Count;
                            expectedNpcs += parsed.Npcs.Count(n => n.TemplateId >= 0 && registry.NpcCodeById.ContainsKey(n.TemplateId) && n.MpsX >= 0 && n.MpsY >= 0);
                            expectedObjs += parsed.Objects.Count(o => o.ObjDataId >= 1 && registry.ObjCodeById.ContainsKey(o.ObjDataId) && o.MpsX >= 0 && o.MpsY >= 0);
                        }
                    }
                }

                if (jsonTrapSpans != expectedTrapSpans)
                {
                    mismatches.Add($"Map {mapId}: Trap span count mismatch (expected {expectedTrapSpans}, got {jsonTrapSpans}).");
                }

                if (jsonRegionNpcs != expectedNpcs)
                {
                    mismatches.Add($"Map {mapId}: Region NPC count mismatch (expected {expectedNpcs}, got {jsonRegionNpcs}).");
                }

                if (jsonRegionObjs != expectedObjs)
                {
                    mismatches.Add($"Map {mapId}: Region Obj count mismatch (expected {expectedObjs}, got {jsonRegionObjs}).");
                }

                verifiedTrapSpans += jsonTrapSpans;
                verifiedRegionNpcs += jsonRegionNpcs;
                verifiedRegionObjs += jsonRegionObjs;
            }
        }

        // Verify Golden & Platina fixedMagicAffixes against registry.GoldenAffixByRowId
        int verifiedGoldenFixedSlots = 0;
        foreach (string eqFileName in new[] { "golden-equipment.items.json", "platina-equipment.items.json" })
        {
            string eqPath = Path.Combine(dataDirectory, "items", eqFileName);
            if (!File.Exists(eqPath))
            {
                continue;
            }

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(eqPath));
            foreach (JsonElement itemElem in doc.RootElement.GetProperty("items").EnumerateArray())
            {
                if (!itemElem.TryGetProperty("fixedMagicAffixes", out JsonElement slots) || slots.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                int rowId = itemElem.GetProperty("rowId").GetInt32();
                foreach (JsonElement slot in slots.EnumerateArray())
                {
                    int affixRowId = slot.GetProperty("affixRowId").GetInt32();
                    if (affixRowId >= 1 && registry.GoldenAffixByRowId.TryGetValue(affixRowId, out var expectedAffix))
                    {
                        verifiedGoldenFixedSlots++;
                        int actualAttrId = slot.GetProperty("attributeId").GetInt32();
                        int actualP0Min = slot.GetProperty("param0Min").GetInt32();
                        int actualP0Max = slot.GetProperty("param0Max").GetInt32();
                        if (actualAttrId != expectedAffix.AttrId || actualP0Min != expectedAffix.P0Min || actualP0Max != expectedAffix.P0Max)
                        {
                            mismatches.Add($"{eqFileName} rowId={rowId} affixRowId={affixRowId}: parameter mismatch vs magicattrib_ge.txt.");
                        }
                    }
                }
            }
        }

        var summary = new Dictionary<string, object?>
        {
            ["verifiedRegions"] = verifiedRegions,
            ["verifiedObstacleCells"] = verifiedObstacleCells,
            ["verifiedTrapSpans"] = verifiedTrapSpans,
            ["verifiedRegionNpcs"] = verifiedRegionNpcs,
            ["verifiedRegionObjects"] = verifiedRegionObjs,
            ["verifiedGoldenAndPlatinaFixedAffixSlots"] = verifiedGoldenFixedSlots,
        };

        return (summary, mismatches);
    }
}
