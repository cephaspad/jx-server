using System;
using System.Collections.Generic;
using System.IO;
using JXServer.DataExporter.Compilation;
using JXServer.DataExporter.Exporters;
using JXServer.DataExporter.Verification;

namespace JXServer.DataExporter;

/// <summary>
/// Offline CLI utility extracting legacy <c>jx-linux-bin/server1</c> assets into canonical unescaped UTF-8 JSON documents
/// (<c>data/**/*.json</c>), verifying 4-Layer Ground-Truth Parity (<c>artifacts/migration-parity-report.json</c>),
/// and compiling deterministic <c>MemoryPack</c> snapshots (<c>artifacts/jxdata.pack</c>) per <c>JXData Standard v1</c> (`FR-001`).
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        string serverRoot = @"D:\Projects\jx-linux-bin\server1";
        string dataDir = Path.GetFullPath("data");
        string schemasDir = Path.GetFullPath("schemas");
        string artifactsDir = Path.GetFullPath("artifacts");
        bool exportAllMaps = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--source" when i + 1 < args.Length:
                    serverRoot = args[++i];
                    break;
                case "--data" when i + 1 < args.Length:
                    dataDir = Path.GetFullPath(args[++i]);
                    break;
                case "--schemas" when i + 1 < args.Length:
                    schemasDir = Path.GetFullPath(args[++i]);
                    break;
                case "--artifacts" when i + 1 < args.Length:
                    artifactsDir = Path.GetFullPath(args[++i]);
                    break;
                case "--all-maps":
                    exportAllMaps = true;
                    break;
            }
        }

        Console.WriteLine($"[JXServer.DataExporter] Exporting from '{serverRoot}' -> '{dataDir}' (allMaps={exportAllMaps})...");

        var registry = new ExportCatalogRegistry();

        var mapExporter = new MapDataExporter();
        mapExporter.PreloadMapMetadata(serverRoot, exportAllMaps, registry);
        Console.WriteLine($"  Preloaded Active Maps: {registry.ActiveMapIds.Count}");

        var itemExporter = new ItemAndAffixDataExporter();
        var (itemCount, affixCount, objCount) = itemExporter.ExportItemsAndAffixes(serverRoot, dataDir, registry);
        Console.WriteLine($"  Items: {itemCount}, MagicAffixes: {affixCount}, GroundObjects: {objCount}");

        var skillExporter = new SkillAndMissileDataExporter();
        var (skillCount, missileCount) = skillExporter.ExportSkillsAndMissiles(serverRoot, dataDir, registry);
        Console.WriteLine($"  Skills: {skillCount}, Missiles: {missileCount}");

        var npcExporter = new NpcAndLootDataExporter();
        var (npcCount, lootCount, globalSpawns, eventSpawns) = npcExporter.ExportNpcsAndLoot(serverRoot, dataDir, registry);
        Console.WriteLine($"  Npcs: {npcCount}, LootTables: {lootCount}, GlobalSpawns: {globalSpawns}, EventSpawns: {eventSpawns}");

        var playerExporter = new PlayerDataExporter();
        int playerCount = playerExporter.ExportPlayers(serverRoot, dataDir, registry);
        Console.WriteLine($"  PlayerTemplates: {playerCount}");

        var shopExporter = new ShopDataExporter();
        var (shopCount, goodsCount) = shopExporter.ExportShops(serverRoot, dataDir, registry);
        Console.WriteLine($"  Shops: {shopCount}, Goods: {goodsCount}");

        int mapCount = mapExporter.ExportMaps(serverRoot, dataDir, exportAllMaps, registry);
        Console.WriteLine($"  Exported Maps: {mapCount}");

        var validator = new JsonSchemaAssetValidator();
        IReadOnlyList<string> errors = validator.ValidateAll(dataDir, schemasDir);
        if (errors.Count > 0)
        {
            foreach (string err in errors)
            {
                Console.Error.WriteLine($"[SCHEMA/FK ERROR] {err}");
            }
        }
        else
        {
            Console.WriteLine("[JXServer.DataExporter] Layer 4 passed: 0 schema, UTF-8, or foreign-key errors.");
        }

        var parityVerifier = new MigrationParityVerifier();
        var (totalParityErrors, reportPath) = parityVerifier.VerifyAndWriteReport(
            serverRoot,
            dataDir,
            artifactsDir,
            registry,
            errors);
        Console.WriteLine($"[JXServer.DataExporter] Wrote 4-Layer Parity Report -> '{reportPath}' (totalErrors={totalParityErrors}).");

        if (totalParityErrors > 0)
        {
            Console.Error.WriteLine($"[JXServer.DataExporter] FAILED with {totalParityErrors} parity/schema error(s). Inspect '{reportPath}'.");
            return 1;
        }

        var compiler = new MemoryPackSnapshotCompiler();
        string packPath = compiler.CompileSnapshot(dataDir, artifactsDir);
        Console.WriteLine($"[JXServer.DataExporter] Compiled MemoryPack snapshot -> '{packPath}'.");

        return 0;
    }
}
