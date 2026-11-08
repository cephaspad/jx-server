using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text;
using JXServer.GameAssets.Archives;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Logging;
using JXServer.Primitives.Encodings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Singleton read-only catalog of world maps loaded from <c>maps/worldset.ini</c>, <c>settings/maplist.ini</c>,
/// and unpacked <c>maps/*.wor</c> / <c>Region_S.dat</c> or packed <c>pak/maps.pak</c>.
/// </summary>
public sealed class MapCatalog : IMapCatalog
{
    private readonly GameAssetsOptions _options;
    private readonly FrozenDictionary<ushort, MapDescriptor> _activeMaps;
    private readonly FrozenDictionary<ushort, byte[]> _rawGbkPaths;
    private readonly ConcurrentDictionary<(ushort MapId, int RegionX, int RegionY), RegionObstacleGrid?> _regionGrids = new();
    private readonly XPackArchiveReader? _mapsPakReader;

    public IReadOnlyCollection<MapDescriptor> ActiveMaps => _activeMaps.Values;

    public MapCatalog(IOptions<GameAssetsOptions> options, ILogger<MapCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;

        string serverRoot = _options.ServerRootPath;
        if (string.IsNullOrWhiteSpace(serverRoot) || !Directory.Exists(serverRoot))
        {
            throw new DirectoryNotFoundException($"ServerRootPath '{serverRoot}' does not exist (FR-012).");
        }

        string worldSetPath = Path.Combine(serverRoot, "maps", "worldset.ini");
        if (!File.Exists(worldSetPath))
        {
            throw new FileNotFoundException($"Required worldset.ini was not found at '{worldSetPath}' (FR-012).", worldSetPath);
        }

        string mapListPath = Path.Combine(serverRoot, "settings", "maplist.ini");
        if (!File.Exists(mapListPath))
        {
            throw new FileNotFoundException($"Required maplist.ini was not found at '{mapListPath}' (FR-012).", mapListPath);
        }

        byte[] worldSetBytes = File.ReadAllBytes(worldSetPath);
        Dictionary<string, Dictionary<string, string>> worldSetSections = IniFileParser.ParseSections(worldSetBytes);
        HashSet<ushort> loadedWorldMapIds = ParseWorldSetMapIds(worldSetSections);
        if (loadedWorldMapIds.Count == 0)
        {
            throw new InvalidDataException($"No active World entries were found in '{worldSetPath}' (FR-012).");
        }

        byte[] mapListBytes = File.ReadAllBytes(mapListPath);
        Dictionary<ushort, byte[]> rawPaths = IniFileParser.ParseRawByteEntriesByUShortKey(mapListBytes);
        Dictionary<ushort, string> mapNames = ParseMapNames(mapListBytes);
        _rawGbkPaths = rawPaths.ToFrozenDictionary();

        string[] candidatePaks =
        [
            Path.Combine(serverRoot, "pak", "maps.pak"),
            Path.Combine(serverRoot, "data", "maps.pak"),
            Path.Combine(serverRoot, "maps.pak")
        ];
        string? pakPath = candidatePaks.FirstOrDefault(File.Exists);
        if (pakPath is not null)
        {
            _mapsPakReader = XPackArchiveReader.TryOpen(pakPath);
        }

        var descriptors = new Dictionary<ushort, MapDescriptor>();
        foreach (ushort mapId in loadedWorldMapIds)
        {
            string mapName = mapNames.GetValueOrDefault(mapId, $"Map #{mapId}");
            rawPaths.TryGetValue(mapId, out byte[]? rawPathBytes);
            string rawSubPath = rawPathBytes is not null ? GbkEncoding.DecodeNullTerminated(rawPathBytes) : mapId.ToString();

            (int beginX, int beginY, int width, int height) = TryLoadWorBounds(mapId, rawPathBytes);
            descriptors[mapId] = new MapDescriptor(mapId, mapName, rawSubPath, beginX, beginY, width, height);
        }

        _activeMaps = descriptors.ToFrozenDictionary();

        int eagerCount = 0;
        if (_options.EagerObstacleLoadMapIds is { Length: > 0 })
        {
            foreach (ushort eagerMapId in _options.EagerObstacleLoadMapIds)
            {
                if (_activeMaps.TryGetValue(eagerMapId, out MapDescriptor? desc))
                {
                    PreloadMapRegions(desc);
                    eagerCount++;
                }
            }
        }

        GameAssetsLogMessages.LogWorldMapsLoaded(logger, _activeMaps.Count, worldSetPath, eagerCount);
    }

    public bool IsMapLoaded(ushort mapId) => _activeMaps.ContainsKey(mapId);

    public bool TryGetMapDescriptor(ushort mapId, out MapDescriptor? descriptor) =>
        _activeMaps.TryGetValue(mapId, out descriptor);

    public bool TryGetRegionObstacleGrid(ushort mapId, int regionX, int regionY, out RegionObstacleGrid? grid)
    {
        if (!_activeMaps.ContainsKey(mapId))
        {
            grid = null;
            return false;
        }

        grid = _regionGrids.GetOrAdd((mapId, regionX, regionY), key => LoadRegionObstacleGrid(key.MapId, key.RegionX, key.RegionY));
        return grid is not null;
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

    private void PreloadMapRegions(MapDescriptor descriptor)
    {
        int endX = descriptor.RegionBeginX + descriptor.RegionWidth - 1;
        int endY = descriptor.RegionBeginY + descriptor.RegionHeight - 1;
        for (int ry = descriptor.RegionBeginY; ry <= endY; ry++)
        {
            for (int rx = descriptor.RegionBeginX; rx <= endX; rx++)
            {
                _ = TryGetRegionObstacleGrid(descriptor.MapId, rx, ry, out _);
            }
        }
    }

    private (int BeginX, int BeginY, int Width, int Height) TryLoadWorBounds(ushort mapId, byte[]? rawGbkPath)
    {
        if (rawGbkPath is not null && _mapsPakReader is not null)
        {
            byte[] prefix = "\\maps\\"u8.ToArray();
            byte[] suffix = ".wor"u8.ToArray();
            byte[] worPath = CombinePathBytes(prefix, rawGbkPath, suffix);
            if (_mapsPakReader.TryReadFile(worPath, out byte[] worData))
            {
                return ParseWorRect(Encoding.ASCII.GetString(worData));
            }
        }

        string mapsDir = Path.Combine(_options.ServerRootPath, "maps");
        if (rawGbkPath is not null)
        {
            string relPath = GbkEncoding.DecodeNullTerminated(rawGbkPath).Replace('\\', Path.DirectorySeparatorChar);
            string unpackedWor = Path.Combine(mapsDir, relPath + ".wor");
            if (File.Exists(unpackedWor))
            {
                return ParseWorRect(File.ReadAllText(unpackedWor));
            }
        }

        return (96, 88, 16, 20);
    }

    private RegionObstacleGrid? LoadRegionObstacleGrid(ushort mapId, int regionX, int regionY)
    {
        if (_rawGbkPaths.TryGetValue(mapId, out byte[]? rawGbkPath) && _mapsPakReader is not null)
        {
            byte[] prefix = "\\maps\\"u8.ToArray();
            byte[] serverSuffix = Encoding.ASCII.GetBytes($"\\v_{regionY:D3}\\{regionX:D3}_region_s.dat");
            byte[] regionPathBytes = CombinePathBytes(prefix, rawGbkPath, serverSuffix);
            if (_mapsPakReader.TryReadFile(regionPathBytes, out byte[] regionDat) &&
                RegionObstacleGrid.TryParse(regionDat, regionX, regionY, out RegionObstacleGrid? grid))
            {
                return grid;
            }

            byte[] clientSuffix = Encoding.ASCII.GetBytes($"\\v_{regionY:D3}\\{regionX:D3}_region_c.dat");
            regionPathBytes = CombinePathBytes(prefix, rawGbkPath, clientSuffix);
            if (_mapsPakReader.TryReadFile(regionPathBytes, out regionDat) &&
                RegionObstacleGrid.TryParse(regionDat, regionX, regionY, out grid))
            {
                return grid;
            }
        }

        if (_rawGbkPaths.TryGetValue(mapId, out byte[]? unpackedPathBytes))
        {
            string relPath = GbkEncoding.DecodeNullTerminated(unpackedPathBytes).Replace('\\', Path.DirectorySeparatorChar);
            string mapFolder = Path.Combine(_options.ServerRootPath, "maps", relPath);
            string serverDat = Path.Combine(mapFolder, $"v_{regionY:D3}", $"{regionX:D3}_Region_S.dat");
            if (File.Exists(serverDat) &&
                RegionObstacleGrid.TryParse(File.ReadAllBytes(serverDat), regionX, regionY, out RegionObstacleGrid? grid))
            {
                return grid;
            }
        }

        return null;
    }

    private static (int BeginX, int BeginY, int Width, int Height) ParseWorRect(string worText)
    {
        foreach (string rawLine in worText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("rect=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] parts = line[5..].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length >= 4 &&
                int.TryParse(parts[0], out int left) &&
                int.TryParse(parts[1], out int top) &&
                int.TryParse(parts[2], out int right) &&
                int.TryParse(parts[3], out int bottom) &&
                right >= left &&
                bottom >= top)
            {
                return (left, top, right - left + 1, bottom - top + 1);
            }
        }

        return (96, 88, 16, 20);
    }

    private static HashSet<ushort> ParseWorldSetMapIds(Dictionary<string, Dictionary<string, string>> sections)
    {
        var ids = new HashSet<ushort>();
        if (!sections.TryGetValue("World", out Dictionary<string, string>? worldSection))
        {
            return ids;
        }

        foreach (var (key, val) in worldSection)
        {
            if (key.StartsWith("World", StringComparison.OrdinalIgnoreCase) &&
                ushort.TryParse(val, out ushort mapId) &&
                mapId > 0)
            {
                ids.Add(mapId);
            }
        }

        return ids;
    }

    private static Dictionary<ushort, string> ParseMapNames(ReadOnlySpan<byte> mapListBytes)
    {
        var names = new Dictionary<ushort, string>();
        string text = Tcvn3Encoding.DecodeNullTerminated(mapListBytes);
        foreach (string rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.StartsWith(';') || line.StartsWith('['))
            {
                continue;
            }

            int eqIdx = line.IndexOf('=');
            if (eqIdx <= 0)
            {
                continue;
            }

            string keyPart = line[..eqIdx].Trim();
            string valPart = line[(eqIdx + 1)..].Trim();
            if (keyPart.EndsWith("_name", StringComparison.OrdinalIgnoreCase) &&
                ushort.TryParse(keyPart[..^5], out ushort mapId) &&
                !string.IsNullOrWhiteSpace(valPart))
            {
                names[mapId] = valPart;
            }
        }

        return names;
    }

    private static byte[] CombinePathBytes(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> middle, ReadOnlySpan<byte> suffix)
    {
        byte[] combined = new byte[prefix.Length + middle.Length + suffix.Length];
        prefix.CopyTo(combined);
        middle.CopyTo(combined.AsSpan(prefix.Length));
        suffix.CopyTo(combined.AsSpan(prefix.Length + middle.Length));
        return combined;
    }
}
