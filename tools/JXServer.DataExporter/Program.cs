using System;
using System.Collections.Generic;
using System.IO;
using JXServer.DataExporter.Compilation;
using JXServer.DataExporter.Exporters;

namespace JXServer.DataExporter;

/// <summary>
/// Offline CLI utility extracting legacy <c>jx-linux-bin/server1</c> assets into canonical UTF-8 JSON documents
/// (<c>data/**/*.json</c>) and <c>MemoryPack</c> snapshots (<c>artifacts/*.pack</c>) per <c>JXData Standard v1</c> (`FR-001`).
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string serverRoot = @"D:\Projects\jx-linux-bin\server1";
        string dataDir = Path.GetFullPath("data");
        string schemasDir = Path.GetFullPath("schemas");
        string artifactsDir = Path.GetFullPath("artifacts");

        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--source":
                    serverRoot = args[++i];
                    break;
                case "--data":
                    dataDir = Path.GetFullPath(args[++i]);
                    break;
                case "--schemas":
                    schemasDir = Path.GetFullPath(args[++i]);
                    break;
                case "--artifacts":
                    artifactsDir = Path.GetFullPath(args[++i]);
                    break;
            }
        }

        Console.WriteLine($"[JXServer.DataExporter] Exporting from '{serverRoot}' -> '{dataDir}'...");

        var itemExporter = new ItemAndAffixDataExporter();
        var (itemCount, affixCount, objCount) = itemExporter.ExportItemsAndAffixes(serverRoot, dataDir);
        Console.WriteLine($"  Items: {itemCount}, MagicAffixes: {affixCount}, GroundObjects: {objCount}");

        var skillExporter = new SkillAndMissileDataExporter();
        var (skillCount, missileCount) = skillExporter.ExportSkillsAndMissiles(serverRoot, dataDir);
        Console.WriteLine($"  Skills: {skillCount}, Missiles: {missileCount}");

        var playerExporter = new PlayerDataExporter();
        int playerCount = playerExporter.ExportPlayers(serverRoot, dataDir);
        Console.WriteLine($"  PlayerTemplates: {playerCount}");

        var npcExporter = new NpcAndLootDataExporter();
        var (npcCount, lootCount) = npcExporter.ExportNpcsAndLoot(serverRoot, dataDir);
        Console.WriteLine($"  Npcs: {npcCount}, LootTables: {lootCount}");

        var mapExporter = new MapDataExporter();
        int mapCount = mapExporter.ExportMaps(serverRoot, dataDir);
        Console.WriteLine($"  Maps: {mapCount}");

        var validator = new JsonSchemaAssetValidator();
        IReadOnlyList<string> errors = validator.ValidateAll(dataDir, schemasDir);
        if (errors.Count > 0)
        {
            foreach (string err in errors)
            {
                Console.Error.WriteLine($"[SCHEMA ERROR] {err}");
            }

            return 1;
        }

        Console.WriteLine("[JXServer.DataExporter] All JSON documents passed schema validation.");

        var compiler = new MemoryPackSnapshotCompiler();
        string packPath = compiler.CompileSnapshot(dataDir, artifactsDir);
        Console.WriteLine($"[JXServer.DataExporter] Compiled MemoryPack snapshot -> '{packPath}'.");

        return 0;
    }
}
