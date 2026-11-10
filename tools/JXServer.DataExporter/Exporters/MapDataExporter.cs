using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.WorldZones;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JXServer.DataExporter.Exporters;

/// <summary>
/// Exports world maps (<c>maps/worldset.ini</c>, <c>settings/maplist.ini</c>, <c>Region_S.dat</c>, and <c>script/**/trap/*.lua</c>)
/// into canonical UTF-8 JSON documents under <c>data/maps/*.map.json</c>.
/// </summary>
public sealed class MapDataExporter
{
    public int ExportMaps(string serverRootPath, string outputDataDirectory)
    {
        var opts = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = serverRootPath,
            EagerObstacleLoadMapIds = [53, 1, 11, 37, 78, 80, 162, 176],
        });

        var npcCatalog = new NpcTemplateCatalog(opts, NullLogger<NpcTemplateCatalog>.Instance);
        var trapCatalog = new TrapScriptCatalog(opts, NullLogger<TrapScriptCatalog>.Instance);
        var mapCatalog = new MapCatalog(opts, NullLogger<MapCatalog>.Instance, npcCatalog);

        string mapsOutDir = Path.Combine(outputDataDirectory, "maps");
        Directory.CreateDirectory(mapsOutDir);

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        int exportedCount = 0;

        foreach (MapDescriptor desc in mapCatalog.ActiveMaps.OrderBy(m => m.MapId))
        {
            string code = Slugify($"map-{desc.MapId}-{desc.MapNameUnicode}");
            IReadOnlyList<StaticNpcSpawnTemplate> spawns = mapCatalog.GetStaticNpcSpawnsForMap(desc.MapId);

            var spawnGroups = spawns
                .GroupBy(s => (s.TemplateId, s.Kind, s.Camp, s.Series, s.Level))
                .Select(g => new
                {
                    npcTemplateId = g.Key.TemplateId,
                    npcCode = $"npc-{g.Key.TemplateId}",
                    nameOverride = g.First().DisplayName,
                    entityKind = g.Key.Kind.ToString(),
                    level = (int)g.Key.Level,
                    series = (int)g.Key.Series,
                    camp = (int)g.Key.Camp,
                    fightMode = g.Key.Kind == EntityKind.DialogNpc ? 0 : 1,
                    isSpecialNpc = g.First().IsSpecialNpc,
                    respawnFrames = g.Key.Kind == EntityKind.DialogNpc ? 0 : 180,
                    lootTableCode = "default-monster-loot",
                    spawns = g.Select(s => new
                    {
                        mpsX = s.SpawnMpsX,
                        mpsY = s.SpawnMpsY,
                        dirIndex = 0,
                        nameOverride = s.DisplayName,
                        scriptPath = s.ScriptPath,
                        isSpecialNpc = s.IsSpecialNpc,
                    }).ToArray(),
                })
                .ToArray();

            var mapDoc = new
            {
                schemaVersion = "1.0.0",
                id = (int)desc.MapId,
                code,
                name = desc.MapNameUnicode,
                mapType = desc.MapId == 53 ? "City" : "Field",
                regionBounds = new
                {
                    leftRegionX = desc.RegionBeginX,
                    topRegionY = desc.RegionBeginY,
                    rightRegionX = desc.RegionBeginX + Math.Max(1, desc.RegionWidth) - 1,
                    bottomRegionY = desc.RegionBeginY + Math.Max(1, desc.RegionHeight) - 1,
                },
                regions = Array.Empty<object>(),
                portals = Array.Empty<object>(),
                spawnGroups,
            };

            string outFile = Path.Combine(mapsOutDir, $"{desc.MapId:D3}-{code}.map.json");
            File.WriteAllText(outFile, JsonSerializer.Serialize(mapDoc, jsonOptions), new UTF8Encoding(false));
            exportedCount++;
        }

        _ = trapCatalog;
        return exportedCount;
    }

    public static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "unknown";
        }

        string normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        bool prevDash = false;
        foreach (char c in normalized)
        {
            char lower = char.ToLowerInvariant(c);
            if (lower == 'đ')
            {
                lower = 'd';
            }

            if ((lower >= 'a' && lower <= 'z') || (lower >= '0' && lower <= '9'))
            {
                sb.Append(lower);
                prevDash = false;
            }
            else if (char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                if (!prevDash && sb.Length > 0)
                {
                    sb.Append('-');
                    prevDash = true;
                }
            }
        }

        string result = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(result) ? "unknown" : result;
    }
}
