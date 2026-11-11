namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Represents a contiguous horizontal trap cell span (<c>KSPTrap</c>) from <c>Region_S.dat</c> Section 1.
/// </summary>
public readonly record struct ParsedTrapCellSpan(
    int RegionX,
    int RegionY,
    byte CellX,
    byte CellY,
    byte SpanCells,
    uint TrapId);
