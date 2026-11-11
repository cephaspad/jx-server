using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Items;

/// <summary>
/// Simulation phase 400 (<see cref="ZoneTickPhase.GroundObjects"/>):
/// steps ground object lifetime expiration and AOI despawn notifications.
/// </summary>
public sealed class GroundObjectZoneTickSystem : IZoneTickSystem
{
    public string Name => "GroundObjects";

    public ZoneTickPhase Phase => ZoneTickPhase.GroundObjects;

    public int SubOrder => 0;

    public ValueTask StepTickAsync(
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.StepGroundObjectsTick(context.CurrentFrame);
        return ValueTask.CompletedTask;
    }
}
