namespace JXServer.Primitives.WorldZones;

/// <summary>
/// Spatial grid and 18-Hz simulation loop constants.
/// </summary>
public static class WorldCoordinateConsts
{
    public const int CellWidthMps = 32;
    public const int CellHeightMps = 32;
    public const int RegionWidthCells = 16;
    public const int RegionHeightCells = 32;
    public const int RegionWidthMps = CellWidthMps * RegionWidthCells;   // 512
    public const int RegionHeightMps = CellHeightMps * RegionHeightCells; // 1024
    public const int TicksPerSecond = 18;
    public const int FixedMapNameBufferSize = 16;
}
