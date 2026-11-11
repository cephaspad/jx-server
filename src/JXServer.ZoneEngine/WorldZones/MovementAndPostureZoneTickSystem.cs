namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Simulation phase 500 (<see cref="ZoneTickPhase.MovementAndPosture"/>):
/// steps player interpolation coordinates, obstacle checks, trap evaluations, and sitting regeneration.
/// </summary>
public sealed class MovementAndPostureZoneTickSystem : IZoneTickSystem
{
    public string Name => "MovementAndPosture";

    public ZoneTickPhase Phase => ZoneTickPhase.MovementAndPosture;

    public int SubOrder => 0;

    public ValueTask StepTickAsync(
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Worker.ProcessMovementAndPostureTickAsync(context.CurrentFrame, cancellationToken);
    }
}
