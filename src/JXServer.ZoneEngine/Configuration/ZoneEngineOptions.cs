using JXServer.Primitives.SessionLeases;
using JXServer.Primitives.WorldZones;

namespace JXServer.ZoneEngine.Configuration;

/// <summary>
/// Configuration options for the sharded 18-Hz <c>MapZoneWorker</c> simulation engine.
/// </summary>
public sealed class ZoneEngineOptions
{
    public const string SectionName = "ZoneEngine";

    public int TicksPerSecond { get; set; } = WorldCoordinateConsts.TicksPerSecond;

    public int HeartbeatIntervalSeconds { get; set; } = SessionLeaseConsts.DefaultHeartbeatIntervalSeconds;

    public int HeartbeatTimeoutSeconds { get; set; } = SessionLeaseConsts.DefaultHeartbeatTimeoutSeconds;

    /// <summary>
    /// Optional master random seed for reproducible deterministic simulation testing (<c>FR-007</c>).
    /// </summary>
    public int? RandomSeed { get; set; }
}
