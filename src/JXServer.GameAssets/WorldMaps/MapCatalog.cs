using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text;
using JXServer.GameAssets.Archives;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Logging;
using JXServer.GameAssets.NpcTemplates;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Encodings;
using JXServer.Primitives.WorldZones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Singleton read-only catalog of world maps loaded from <c>maps/worldset.ini</c>, <c>settings/maplist.ini</c>,
/// and unpacked <c>maps/*.wor</c> / <c>Region_S.dat</c> or packed <c>pak/maps.pak</c> (`FR-001`, `FR-004`, `FR-008`).
/// </summary>
public sealed class MapCatalog : IMapCatalog
{
    private readonly GameAssetsOptions _options;
    private readonly INpcTemplateCatalog? _npcTemplateCatalog;
    private readonly FrozenDictionary<ushort, MapDescriptor> _activeMaps;
    private readonly FrozenDictionary<ushort, byte[]> _rawGbkPaths;
    private readonly ConcurrentDictionary<(ushort MapId, int RegionX, int RegionY), RegionObstacleGrid?> _regionGrids = new();
    private readonly ConcurrentDictionary<(ushort MapId, int RegionX, int RegionY), RegionTrapGrid?> _regionTraps = new();
    private readonly ConcurrentDictionary<ushort, IReadOnlyList<StaticNpcSpawnTemplate>> _mapNpcSpawns = new();
    private readonly XPackArchiveReader? _mapsPakReader;

    public IReadOnlyCollection<MapDescriptor> ActiveMaps => _activeMaps.Values;

    public MapCatalog(
        IOptions<GameAssetsOptions> options,
        ILogger<MapCatalog> logger,
        INpcTemplateCatalog? npcTemplateCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;

        string serverRoot = _options.ServerRootPath;
        if (string.IsNullOrWhiteSpace(serverRoot) || !Directory.Exists(serverRoot))
        {
            throw new DirectoryNotFoundException($"ServerRootPath '{serverRoot}' does not exist (FR-012).");
        }

        if (npcTemplateCatalog is null && File.Exists(Path.Combine(serverRoot, "settings", "npcs.txt")))
        {
            npcTemplateCatalog = new NpcTemplateCatalog(options, NullLogger<NpcTemplateCatalog>.Instance);
        }

        _npcTemplateCatalog = npcTemplateCatalog;

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
        if (_options.ExcludedWorldSetMapIds is { Length: > 0 })
        {
            loadedWorldMapIds.ExceptWith(_options.ExcludedWorldSetMapIds);
        }

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

        HashSet<ushort> eagerSet = _options.EagerObstacleLoadMapIds is { Length: > 0 }
            ? [.. _options.EagerObstacleLoadMapIds]
            : [];

        var descriptors = new Dictionary<ushort, MapDescriptor>();
        int eagerCount = 0;
        foreach (ushort mapId in loadedWorldMapIds)
        {
            string mapName = mapNames.GetValueOrDefault(mapId, $"Map #{mapId}");
            rawPaths.TryGetValue(mapId, out byte[]? rawPathBytes);
            string rawSubPath = rawPathBytes is not null ? GbkEncoding.DecodeNullTerminated(rawPathBytes) : mapId.ToString();

            (int beginX, int beginY, int width, int height) = TryLoadWorBounds(mapId, rawPathBytes);
            var desc = new MapDescriptor(mapId, mapName, rawSubPath, beginX, beginY, width, height);

            if (eagerSet.Contains(mapId))
            {
                IReadOnlyList<StaticNpcSpawnTemplate> spawns = PreloadMapRegions(desc);
                desc = desc with { RegionNpcSpawns = spawns };
                eagerCount++;
            }

            descriptors[mapId] = desc;
        }

        _activeMaps = descriptors.ToFrozenDictionary();

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

    public bool TryGetRegionTrapGrid(ushort mapId, int regionX, int regionY, out RegionTrapGrid? trapGrid)
    {
        if (!_activeMaps.ContainsKey(mapId))
        {
            trapGrid = null;
            return false;
        }

        trapGrid = _regionTraps.GetOrAdd((mapId, regionX, regionY), key => LoadRegionTrapGrid(key.MapId, key.RegionX, key.RegionY));
        return trapGrid is not null;
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
            // Empty/wilderness region inside valid map bounds has no obstacles
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
        if (!_activeMaps.TryGetValue(mapId, out MapDescriptor? desc))
        {
            return [];
        }

        if (desc.RegionNpcSpawns.Count > 0)
        {
            return desc.RegionNpcSpawns;
        }

        return _mapNpcSpawns.GetOrAdd(mapId, _ => PreloadMapRegions(desc));
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

    private IReadOnlyList<StaticNpcSpawnTemplate> PreloadMapRegions(MapDescriptor descriptor)
    {
        var spawns = new List<StaticNpcSpawnTemplate>();
        int endX = descriptor.RegionBeginX + descriptor.RegionWidth - 1;
        int endY = descriptor.RegionBeginY + descriptor.RegionHeight - 1;
        for (int ry = descriptor.RegionBeginY; ry <= endY; ry++)
        {
            for (int rx = descriptor.RegionBeginX; rx <= endX; rx++)
            {
                if (!TryReadRegionDatBytes(descriptor.MapId, rx, ry, out byte[]? regionDat) || regionDat is null)
                {
                    _regionGrids.TryAdd((descriptor.MapId, rx, ry), null);
                    _regionTraps.TryAdd((descriptor.MapId, rx, ry), null);
                    continue;
                }

                if (RegionObstacleGrid.TryParse(regionDat, rx, ry, out RegionObstacleGrid? grid))
                {
                    _regionGrids[(descriptor.MapId, rx, ry)] = grid;
                }
                else
                {
                    _regionGrids.TryAdd((descriptor.MapId, rx, ry), null);
                }

                if (RegionTrapGrid.TryParse(regionDat, rx, ry, out RegionTrapGrid? trapGrid))
                {
                    _regionTraps[(descriptor.MapId, rx, ry)] = trapGrid;
                }
                else
                {
                    _regionTraps.TryAdd((descriptor.MapId, rx, ry), null);
                }

                ParseRegionNpcSection(descriptor.MapId, regionDat, spawns);
            }
        }

        if (_npcTemplateCatalog is not null)
        {
            foreach (StaticNpcSpawnTemplate autoSpawn in _npcTemplateCatalog.GetAutoExecSpawnsForMap(descriptor.MapId))
            {
                spawns.Add(autoSpawn);
            }
        }

        IReadOnlyList<StaticNpcSpawnTemplate> frozenSpawns = spawns.ToArray();
        _mapNpcSpawns[descriptor.MapId] = frozenSpawns;
        return frozenSpawns;
    }

    private void ParseRegionNpcSection(
        ushort mapId,
        ReadOnlySpan<byte> regionDatBytes,
        List<StaticNpcSpawnTemplate> output)
    {
        if (regionDatBytes.Length < 28)
        {
            return;
        }

        uint sectionCount = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes[..4]);
        if (sectionCount < 3 || sectionCount > 32)
        {
            return;
        }

        int header8Size = 4 + (int)(sectionCount * 8);
        if (regionDatBytes.Length < header8Size)
        {
            return;
        }

        uint section0Offset = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(4, 4));
        bool offsetsAreRelative = section0Offset == 0;

        // Section 2 (NPC_SECTION) descriptor at 4 + (2 * 8) = 20
        uint rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(20, 4));
        uint rawLength = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(24, 4));
        if (rawLength < 12)
        {
            return;
        }

        int resolvedOffset = offsetsAreRelative ? header8Size + (int)rawOffset : (int)rawOffset;
        if (resolvedOffset < header8Size || resolvedOffset + (int)rawLength > regionDatBytes.Length)
        {
            return;
        }

        ReadOnlySpan<byte> npcSection = regionDatBytes.Slice(resolvedOffset, (int)rawLength);
        uint numNpcs = BinaryPrimitives.ReadUInt32LittleEndian(npcSection[..4]);
        if (numNpcs == 0)
        {
            return;
        }

        int pos = 12; // After 12-byte KNpcFileHead
        for (int i = 0; i < numNpcs; i++)
        {
            if (pos + 60 > npcSection.Length)
            {
                break;
            }

            ReadOnlySpan<byte> header = npcSection.Slice(pos, 60);
            int templateId = BinaryPrimitives.ReadInt32LittleEndian(header[..4]);
            int positionX = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(4, 4));
            int positionY = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(8, 4));
            bool isSpecial = header[12] != 0;
            ReadOnlySpan<byte> rawNameSpan = header.Slice(16, 32);
            short level = BinaryPrimitives.ReadInt16LittleEndian(header.Slice(48, 2));
            short shKind = BinaryPrimitives.ReadInt16LittleEndian(header.Slice(54, 2));
            byte cCamp = header[56];
            byte cSeries = header[57];
            ushort scriptNameLen = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(58, 2));

            pos += 60;
            if (pos + scriptNameLen > npcSection.Length)
            {
                break;
            }

            string scriptPath = scriptNameLen > 0
                ? GbkEncoding.DecodeNullTerminated(npcSection.Slice(pos, scriptNameLen))
                : string.Empty;
            pos += scriptNameLen;

            if (templateId < 0)
            {
                continue;
            }

            NpcTemplate? tpl = null;
            _npcTemplateCatalog?.TryGetTemplate(templateId, out tpl);

            // Prefer localized Vietnamese name from settings/npcs.txt over raw Chinese GBK KSPNpc.szName
            string displayName = tpl is not null && !string.IsNullOrWhiteSpace(tpl.Name)
                ? tpl.Name
                : Tcvn3Encoding.DecodeNullTerminated(rawNameSpan);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = $"NPC #{templateId}";
            }

            EntityKind kind = shKind switch
            {
                1 => EntityKind.Player,
                2 => EntityKind.Partner,
                3 => EntityKind.DialogNpc,
                _ => tpl?.Kind ?? EntityKind.Monster,
            };

            EntityCamp camp = Enum.IsDefined(typeof(EntityCamp), cCamp)
                ? (EntityCamp)cCamp
                : (tpl?.Camp ?? EntityCamp.Begin);

            ElementSeries series = Enum.IsDefined(typeof(ElementSeries), cSeries)
                ? (ElementSeries)cSeries
                : (tpl?.Series ?? ElementSeries.Metal);

            output.Add(new StaticNpcSpawnTemplate(
                TemplateId: templateId,
                MapId: mapId,
                SpawnMpsX: positionX,
                SpawnMpsY: positionY,
                Level: Math.Max((short)1, level),
                Kind: kind,
                Camp: camp,
                Series: series,
                IsSpecialNpc: isSpecial,
                DisplayName: displayName,
                ScriptPath: scriptPath));
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

    private bool TryReadRegionDatBytes(ushort mapId, int regionX, int regionY, out byte[]? regionDat)
    {
        regionDat = null;
        if (_rawGbkPaths.TryGetValue(mapId, out byte[]? rawGbkPath) && _mapsPakReader is not null)
        {
            byte[] prefix = "\\maps\\"u8.ToArray();
            byte[] serverSuffix = Encoding.ASCII.GetBytes($"\\v_{regionY:D3}\\{regionX:D3}_region_s.dat");
            byte[] regionPathBytes = CombinePathBytes(prefix, rawGbkPath, serverSuffix);
            if (_mapsPakReader.TryReadFile(regionPathBytes, out byte[] pakDat))
            {
                regionDat = pakDat;
                return true;
            }

            byte[] clientSuffix = Encoding.ASCII.GetBytes($"\\v_{regionY:D3}\\{regionX:D3}_region_c.dat");
            regionPathBytes = CombinePathBytes(prefix, rawGbkPath, clientSuffix);
            if (_mapsPakReader.TryReadFile(regionPathBytes, out pakDat))
            {
                regionDat = pakDat;
                return true;
            }
        }

        if (_rawGbkPaths.TryGetValue(mapId, out byte[]? unpackedPathBytes))
        {
            string relPath = GbkEncoding.DecodeNullTerminated(unpackedPathBytes).Replace('\\', Path.DirectorySeparatorChar);
            string mapFolder = Path.Combine(_options.ServerRootPath, "maps", relPath);
            string serverDat = Path.Combine(mapFolder, $"v_{regionY:D3}", $"{regionX:D3}_Region_S.dat");
            if (File.Exists(serverDat))
            {
                regionDat = File.ReadAllBytes(serverDat);
                return true;
            }
        }

        return false;
    }

    private RegionObstacleGrid? LoadRegionObstacleGrid(ushort mapId, int regionX, int regionY)
    {
        if (TryReadRegionDatBytes(mapId, regionX, regionY, out byte[]? regionDat) &&
            regionDat is not null &&
            RegionObstacleGrid.TryParse(regionDat, regionX, regionY, out RegionObstacleGrid? grid))
        {
            return grid;
        }

        return null;
    }

    private RegionTrapGrid? LoadRegionTrapGrid(ushort mapId, int regionX, int regionY)
    {
        if (TryReadRegionDatBytes(mapId, regionX, regionY, out byte[]? regionDat) &&
            regionDat is not null &&
            RegionTrapGrid.TryParse(regionDat, regionX, regionY, out RegionTrapGrid? trapGrid))
        {
            return trapGrid;
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
