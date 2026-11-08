namespace JXServer.Primitives.WorldZones;

/// <summary>
/// Unit-explicit world position (<c>MapId</c>, <c>MpsX</c>, <c>MpsY</c>) with deterministic Region, Cell, and Sub-Cell offset conversions.
/// </summary>
public readonly record struct WorldPosition(ushort MapId, int MpsX, int MpsY)
{
    public int RegionX => MpsX / WorldCoordinateConsts.RegionWidthMps;

    public int RegionY => MpsY / WorldCoordinateConsts.RegionHeightMps;

    public int PackedRegionId => (RegionY << 16) | (RegionX & 0xFFFF);

    public int CellX => (MpsX % WorldCoordinateConsts.RegionWidthMps) / WorldCoordinateConsts.CellWidthMps;

    public int CellY => (MpsY % WorldCoordinateConsts.RegionHeightMps) / WorldCoordinateConsts.CellHeightMps;

    public ushort OffX => (ushort)((MpsX % WorldCoordinateConsts.CellWidthMps) << 10);

    public ushort OffY => (ushort)((MpsY % WorldCoordinateConsts.CellHeightMps) << 10);
}
