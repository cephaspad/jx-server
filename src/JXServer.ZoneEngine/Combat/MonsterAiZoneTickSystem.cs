using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Simulation phase 200 (<see cref="ZoneTickPhase.MonsterAi"/>):
/// steps AI state machines, monster patrol/respawns, aggro checks, and automated skill casting.
/// </summary>
public sealed class MonsterAiZoneTickSystem : IZoneTickSystem
{
    public string Name => "MonsterAi";

    public ZoneTickPhase Phase => ZoneTickPhase.MonsterAi;

    public int SubOrder => 0;

    public ValueTask StepTickAsync(
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.StepMonsterAiTick(context.CurrentFrame);
        return ValueTask.CompletedTask;
    }
}
