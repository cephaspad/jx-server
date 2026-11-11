using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Simulation phase 300 (<see cref="ZoneTickPhase.Missiles"/>):
/// steps 64-direction ballistic trajectories, obstacle collisions, and combat hit detection.
/// </summary>
public sealed class MissileZoneTickSystem : IZoneTickSystem
{
    public string Name => "Missiles";

    public ZoneTickPhase Phase => ZoneTickPhase.Missiles;

    public int SubOrder => 0;

    public ValueTask StepTickAsync(
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.StepMissilesTick();
        return ValueTask.CompletedTask;
    }
}
