using JXServer.GameAssets.WorldMaps;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging;

namespace JXServer.ZoneEngine.Tests.SimulationHarnesses;

/// <summary>
/// Configuration options for <see cref="ZoneSimulationHarness"/> (<c>FR-007</c>, <c>FR-008</c>).
/// </summary>
public sealed class ZoneSimulationHarnessOptions
{
    public int? RandomSeed { get; set; }

    public DateTimeOffset InitialUtcTimestamp { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public int TicksPerSecond { get; set; } = 18;

    public int HeartbeatIntervalSeconds { get; set; } = 5;

    public int HeartbeatTimeoutSeconds { get; set; } = 15;

    public List<MapDescriptor> InitialMaps { get; } = [];

    public List<IZoneModule> CustomModules { get; } = [];

    public ZoneAssetServices? Assets { get; set; }

    public ILoggerFactory? LoggerFactory { get; set; }
}
