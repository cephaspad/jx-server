using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Tests.SimulationHarnesses;

/// <summary>
/// Holds a scheduled command to execute at a specific future simulation frame (<c>FR-008</c>).
/// </summary>
internal readonly record struct ScheduledZoneCommandEntry(
    long ExecuteAtFrame,
    ushort MapId,
    IZoneCommand Command);
