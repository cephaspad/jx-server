using JXServer.GameAssets.NpcTemplates;

namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Read-only catalog of loaded world maps (<c>worldset.ini</c>, <c>maplist.ini</c>) and their <c>Region_S.dat</c>
/// obstacle matrices, trap grids, and static NPC placements (`FR-001`, `FR-004`, `FR-008`).
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
    /// Attempts to retrieve the <see cref="RegionTrapGrid"/> for a specific region on a loaded map (`FR-004`, `FR-008`).
    /// </summary>
    bool TryGetRegionTrapGrid(ushort mapId, int regionX, int regionY, out RegionTrapGrid? trapGrid);

    /// <summary>
    /// Returns the primary obstacle kind (<c>0</c> = passable, <c>1</c> = <c>Obstacle_Full</c> blocked) at world pixel coordinates.
    /// </summary>
    int GetCellObstacle(ushort mapId, int mpsX, int mpsY);

    /// <summary>
    /// Returns the 32-bit <c>uTrapId</c> at world pixel coordinates, or <c>0</c> if the cell has no trap.
    /// </summary>
    uint GetCellTrapId(ushort mapId, int mpsX, int mpsY);

    /// <summary>
    /// Returns all static NPC and monster spawn templates for <paramref name="mapId"/> (`FR-004`).
    /// </summary>
    IReadOnlyList<StaticNpcSpawnTemplate> GetStaticNpcSpawnsForMap(ushort mapId);

    /// <summary>
    /// Resolves a target map ID for world entry, redirecting to the configured fallback starter village (<c>Map 53</c>)
    /// when <paramref name="requestedMapId"/> is not loaded and fallback mode is enabled (`FR-005`, `FR-007`).
    /// </summary>
    bool TryResolveActiveOrFallbackMapId(ushort requestedMapId, out ushort resolvedMapId, out bool wasRedirected);
}
