namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Simulation phase 600 (<see cref="ZoneTickPhase.SessionHeartbeats"/>):
/// issues periodic ping probes and detaches timed-out sessions.
/// </summary>
public sealed class SessionHeartbeatZoneTickSystem : IZoneTickSystem
{
    public string Name => "SessionHeartbeats";

    public ZoneTickPhase Phase => ZoneTickPhase.SessionHeartbeats;

    public int SubOrder => 0;

    public ValueTask StepTickAsync(
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Worker.ProcessHeartbeatsAsync(context.CurrentFrame, cancellationToken);
    }
}
