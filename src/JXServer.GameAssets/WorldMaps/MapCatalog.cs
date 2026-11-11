using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Logging;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.Snapshots;
using JXServer.Primitives.WorldZones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Singleton read-only catalog of world maps and region cell obstacle grids loaded via
/// <c>JXData Standard v1</c> Dual-Tier runtime loaders (<c>data/maps/*.map.json</c> or <c>artifacts/*.pack</c>).
/// </summary>
public sealed class MapCatalog : IMapCatalog
{
    private static readonly ConcurrentDictionary<string, Lazy<FrozenDictionary<ushort, string>>> MapFileIndexCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<(string MapFilePath, long LastWriteTicks), Lazy<(FrozenDictionary<(int RegionX, int RegionY), RegionObstacleGrid> Obstacles, FrozenDictionary<(int RegionX, int RegionY), RegionTrapGrid> Traps)>> ParsedMapGridCache = new();
    private static readonly RegionObstacleGrid EmptyZeroOriginGrid = new(0, 0, new int[512]);

    private readonly GameAssetsOptions _options;
    private readonly INpcTemplateCatalog? _npcTemplateCatalog;
    private readonly FrozenDictionary<ushort, MapDescriptor> _activeMaps;
    private readonly ConcurrentDictionary<(ushort MapId, int RegionX, int RegionY), RegionObstacleGrid> _emptyFallbackGrids = new();
    private readonly ConcurrentDictionary<ushort, Lazy<(FrozenDictionary<(int RegionX, int RegionY), RegionObstacleGrid> Obstacles, FrozenDictionary<(int RegionX, int RegionY), RegionTrapGrid> Traps)>> _loadedMaps = new();

    public IReadOnlyCollection<MapDescriptor> ActiveMaps => _activeMaps.Values;

    public MapCatalog(
        IOptions<GameAssetsOptions> options,
        ILogger<MapCatalog> logger,
        INpcTemplateCatalog? npcTemplateCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _npcTemplateCatalog = npcTemplateCatalog;

        string serverRoot = _options.ServerRootPath;
        if (!string.IsNullOrWhiteSpace(serverRoot) && !Directory.Exists(serverRoot))
        {
            throw new DirectoryNotFoundException($"ServerRootPath '{serverRoot}' does not exist (FR-012).");
        }

        var descriptors = new Dictionary<ushort, MapDescriptor>();

        if (GameAssetsSnapshotLoader.TryLoadBundle(_options, out GameAssetsSnapshotBundle? bundle, out _) &&
            bundle is not null &&
            bundle.MapDescriptors.Count > 0)
        {
            foreach (MapDescriptor desc in bundle.MapDescriptors)
            {
                descriptors[desc.MapId] = desc;
            }
        }
        else
        {
            descriptors[53] = new MapDescriptor(
                53,
                "Ba Lăng Huyện",
                "maps/worldset/53",
                82,
                89,
                33,
                20);
        }

        if (_options.ExcludedWorldSetMapIds is { Length: > 0 })
        {
            foreach (ushort excludedMapId in _options.ExcludedWorldSetMapIds)
            {
                descriptors.Remove(excludedMapId);
            }
        }

        HashSet<ushort> eagerSet = _options.EagerObstacleLoadMapIds is { Length: > 0 }
            ? [.. _options.EagerObstacleLoadMapIds]
            : [];

        int eagerCount = 0;
        foreach (ushort mapId in descriptors.Keys)
        {
            if (eagerSet.Contains(mapId))
            {
                _ = EnsureMapDataLoaded(mapId);
                eagerCount++;
            }
        }

        _activeMaps = descriptors.ToFrozenDictionary();
        GameAssetsLogMessages.LogWorldMapsLoaded(logger, _activeMaps.Count, _options.DataDirectory, eagerCount);
    }

    public bool IsMapLoaded(ushort mapId) => _activeMaps.ContainsKey(mapId);

    public bool TryGetMapDescriptor(ushort mapId, out MapDescriptor? descriptor) =>
        _activeMaps.TryGetValue(mapId, out descriptor);

    public bool TryGetRegionObstacleGrid(ushort mapId, int regionX, int regionY, out RegionObstacleGrid? grid)
    {
        if (!_activeMaps.TryGetValue(mapId, out MapDescriptor? desc))
        {
            grid = null;
            return false;
        }

        var (obstacles, _) = EnsureMapDataLoaded(mapId);
        if (obstacles.TryGetValue((regionX, regionY), out RegionObstacleGrid? loadedGrid))
        {
            grid = loadedGrid;
            return true;
        }

        if (regionX >= desc.RegionBeginX &&
            regionX < desc.RegionBeginX + desc.RegionWidth &&
            regionY >= desc.RegionBeginY &&
            regionY < desc.RegionBeginY + desc.RegionHeight)
        {
            grid = regionX == 0 && regionY == 0
                ? EmptyZeroOriginGrid
                : _emptyFallbackGrids.GetOrAdd((mapId, regionX, regionY), static key => new RegionObstacleGrid(key.RegionX, key.RegionY, new int[512]));
            return true;
        }

        grid = null;
        return false;
    }

    public bool TryGetRegionTrapGrid(ushort mapId, int regionX, int regionY, out RegionTrapGrid? trapGrid)
    {
        if (!_activeMaps.ContainsKey(mapId))
        {
            trapGrid = null;
            return false;
        }

        var (_, traps) = EnsureMapDataLoaded(mapId);
        return traps.TryGetValue((regionX, regionY), out trapGrid);
    }

    public int GetCellObstacle(ushort mapId, int mpsX, int mpsY)
    {
        if (mpsX < 0 || mpsY < 0 || !_activeMaps.TryGetValue(mapId, out MapDescriptor? desc))
        {
            return 1;
        }

        var pos = new WorldPosition(mapId, mpsX, mpsY);
        if (pos.RegionX < desc.RegionBeginX ||
            pos.RegionX >= desc.RegionBeginX + desc.RegionWidth ||
            pos.RegionY < desc.RegionBeginY ||
            pos.RegionY >= desc.RegionBeginY + desc.RegionHeight)
        {
            return 1;
        }

        if (!TryGetRegionObstacleGrid(mapId, pos.RegionX, pos.RegionY, out RegionObstacleGrid? grid) || grid is null)
        {
            return 0;
        }

        return grid.GetObstacle(pos.CellX, pos.CellY) & 0x0F;
    }

    public uint GetCellTrapId(ushort mapId, int mpsX, int mpsY)
    {
        if (mpsX < 0 || mpsY < 0)
        {
            return 0;
        }

        var pos = new WorldPosition(mapId, mpsX, mpsY);
        if (!TryGetRegionTrapGrid(mapId, pos.RegionX, pos.RegionY, out RegionTrapGrid? trapGrid) || trapGrid is null)
        {
            return 0;
        }

        return trapGrid.GetTrapId(pos.CellX, pos.CellY);
    }

    public IReadOnlyList<StaticNpcSpawnTemplate> GetStaticNpcSpawnsForMap(ushort mapId)
    {
        var result = new List<StaticNpcSpawnTemplate>();
        if (_activeMaps.TryGetValue(mapId, out MapDescriptor? desc) && desc.RegionNpcSpawns.Count > 0)
        {
            result.AddRange(desc.RegionNpcSpawns);
        }

        if (_npcTemplateCatalog is not null)
        {
            var autoSpawns = _npcTemplateCatalog.GetAutoExecSpawnsForMap(mapId);
            if (autoSpawns.Count > 0)
            {
                result.AddRange(autoSpawns);
            }
        }

        return result;
    }

    public bool TryResolveActiveOrFallbackMapId(ushort requestedMapId, out ushort resolvedMapId, out bool wasRedirected)
    {
        if (_activeMaps.ContainsKey(requestedMapId))
        {
            resolvedMapId = requestedMapId;
            wasRedirected = false;
            return true;
        }

        if (_options.FallbackUnloadedStarterVillage &&
            _activeMaps.ContainsKey(_options.DefaultFallbackStarterVillageMapId))
        {
            resolvedMapId = _options.DefaultFallbackStarterVillageMapId;
            wasRedirected = true;
            return true;
        }

        resolvedMapId = 0;
        wasRedirected = false;
        return false;
    }

    private (FrozenDictionary<(int RegionX, int RegionY), RegionObstacleGrid> Obstacles, FrozenDictionary<(int RegionX, int RegionY), RegionTrapGrid> Traps) EnsureMapDataLoaded(ushort mapId) =>
        _loadedMaps.GetOrAdd(
            mapId,
            id => new Lazy<(FrozenDictionary<(int RegionX, int RegionY), RegionObstacleGrid> Obstacles, FrozenDictionary<(int RegionX, int RegionY), RegionTrapGrid> Traps)>(
                () => LoadMapJsonData(id),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    private (FrozenDictionary<(int RegionX, int RegionY), RegionObstacleGrid> Obstacles, FrozenDictionary<(int RegionX, int RegionY), RegionTrapGrid> Traps) LoadMapJsonData(ushort mapId)
    {
        string mapsDir = Path.GetFullPath(Path.Combine(_options.DataDirectory, "maps"));
        if (!Directory.Exists(mapsDir))
        {
            return (FrozenDictionary<(int RegionX, int RegionY), RegionObstacleGrid>.Empty, FrozenDictionary<(int RegionX, int RegionY), RegionTrapGrid>.Empty);
        }

        FrozenDictionary<ushort, string> fileIndex = MapFileIndexCache.GetOrAdd(
            mapsDir,
            static dir => new Lazy<FrozenDictionary<ushort, string>>(
                () => BuildMapFileIndex(dir),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        if (!fileIndex.TryGetValue(mapId, out string? mapFile) || !File.Exists(mapFile))
        {
            return (FrozenDictionary<(int RegionX, int RegionY), RegionObstacleGrid>.Empty, FrozenDictionary<(int RegionX, int RegionY), RegionTrapGrid>.Empty);
        }

        long writeTicks = File.GetLastWriteTimeUtc(mapFile).Ticks;
        return ParsedMapGridCache.GetOrAdd(
            (mapFile, writeTicks),
            static key => new Lazy<(FrozenDictionary<(int RegionX, int RegionY), RegionObstacleGrid> Obstacles, FrozenDictionary<(int RegionX, int RegionY), RegionTrapGrid> Traps)>(
                () => ParseMapGridsFromFile(key.MapFilePath),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static FrozenDictionary<ushort, string> BuildMapFileIndex(string mapsDir)
    {
        var index = new Dictionary<ushort, string>();
        if (!Directory.Exists(mapsDir))
        {
            return FrozenDictionary<ushort, string>.Empty;
        }

        foreach (string file in Directory.EnumerateFiles(mapsDir, "*.map.json"))
        {
            ReadOnlySpan<char> fileName = Path.GetFileName(file.AsSpan());
            int underscoreIdx = fileName.IndexOf('_');
            if (underscoreIdx > 0 && ushort.TryParse(fileName[..underscoreIdx], out ushort id))
            {
                index.TryAdd(id, file);
            }
        }

        return index.ToFrozenDictionary();
    }

    private static (FrozenDictionary<(int RegionX, int RegionY), RegionObstacleGrid> Obstacles, FrozenDictionary<(int RegionX, int RegionY), RegionTrapGrid> Traps) ParseMapGridsFromFile(string mapFile)
    {
        var obstacles = new Dictionary<(int RegionX, int RegionY), RegionObstacleGrid>();
        var traps = new Dictionary<(int RegionX, int RegionY), RegionTrapGrid>();

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(mapFile));
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("regions", out JsonElement regionsElement) &&
                regionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement region in regionsElement.EnumerateArray())
                {
                    int rx = region.GetProperty("regionX").GetInt32();
                    int ry = region.GetProperty("regionY").GetInt32();
                    string? rleBase64 = region.TryGetProperty("obstacleRleBase64", out JsonElement rleElement) &&
                        rleElement.ValueKind == JsonValueKind.String
                        ? rleElement.GetString()
                        : null;

                    int[] cells = !string.IsNullOrEmpty(rleBase64)
                        ? ObstacleRleCodec.DecodeBase64ToCells(rleBase64)
                        : new int[512];

                    obstacles[(rx, ry)] = new RegionObstacleGrid(rx, ry, cells);
                }
            }

            if (root.TryGetProperty("portals", out JsonElement portalsElement) &&
                portalsElement.ValueKind == JsonValueKind.Array)
            {
                var trapsByRegion = new Dictionary<(int rx, int ry), uint[]>();

                foreach (JsonElement portal in portalsElement.EnumerateArray())
                {
                    uint trapId = portal.GetProperty("trapId").GetUInt32();
                    if (!portal.TryGetProperty("triggerCells", out JsonElement cellsElement) ||
                        cellsElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement span in cellsElement.EnumerateArray())
                    {
                        int rx = span.GetProperty("regionX").GetInt32();
                        int ry = span.GetProperty("regionY").GetInt32();
                        int cy = span.GetProperty("cellY").GetInt32();
                        int startCx = span.GetProperty("startCellX").GetInt32();
                        int count = span.GetProperty("cellCount").GetInt32();

                        if (!trapsByRegion.TryGetValue((rx, ry), out uint[]? trapCells))
                        {
                            trapCells = new uint[512];
                            trapsByRegion[(rx, ry)] = trapCells;
                        }

                        for (int c = 0; c < count; c++)
                        {
                            int cx = startCx + c;
                            if (cx < WorldCoordinateConsts.RegionWidthCells &&
                                cy < WorldCoordinateConsts.RegionHeightCells)
                            {
                                trapCells[(cx * WorldCoordinateConsts.RegionHeightCells) + cy] = trapId;
                            }
                        }
                    }
                }

                foreach (var ((rx, ry), trapCells) in trapsByRegion)
                {
                    traps[(rx, ry)] = new RegionTrapGrid(rx, ry, trapCells);
                }
            }
        }
        catch
        {
            // Graceful fallback on corrupt JSON
        }

        return (obstacles.ToFrozenDictionary(), traps.ToFrozenDictionary());
    }
}
