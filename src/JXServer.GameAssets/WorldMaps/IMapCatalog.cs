namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Read-only catalog of loaded world maps (<c>worldset.ini</c>, <c>maplist.ini</c>) and their <c>Region_S.dat</c> obstacle matrices.
/// </summary>
public interface IMapCatalog
{
    /// <summary>
    /// Gets the collection of active map descriptors loaded from <c>worldset.ini</c>.
    /// </summary>
    IReadOnlyCollection<MapDescriptor> ActiveMaps { get; }

    /// <summary>
    /// Determines whether <paramref name="mapId"/> is loaded in <c>worldset.ini</c>.
    /// </summary>
    bool IsMapLoaded(ushort mapId);

    /// <summary>
    /// Attempts to retrieve the <see cref="MapDescriptor"/> for an active map.
    /// </summary>
    bool TryGetMapDescriptor(ushort mapId, out MapDescriptor? descriptor);

    /// <summary>
    /// Attempts to retrieve the <see cref="RegionObstacleGrid"/> for a specific region on a loaded map.
    /// </summary>
    bool TryGetRegionObstacleGrid(ushort mapId, int regionX, int regionY, out RegionObstacleGrid? grid);

    /// <summary>
    /// Resolves a target map ID for world entry, redirecting to the configured fallback starter village (<c>Map 53</c>)
    /// when <paramref name="requestedMapId"/> is not loaded and fallback mode is enabled (`FR-005`, `FR-007`).
    /// </summary>
    bool TryResolveActiveOrFallbackMapId(ushort requestedMapId, out ushort resolvedMapId, out bool wasRedirected);
}
