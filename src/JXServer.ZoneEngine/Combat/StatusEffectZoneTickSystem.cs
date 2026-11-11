using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Simulation phase 100 (<see cref="ZoneTickPhase.StatusEffects"/>):
/// processes active status effects (poison damage, freeze/stun decrements, and death checks).
/// </summary>
public sealed class StatusEffectZoneTickSystem : IZoneTickSystem
{
    public string Name => "StatusEffects";

    public ZoneTickPhase Phase => ZoneTickPhase.StatusEffects;

    public int SubOrder => 0;

    public ValueTask StepTickAsync(
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.ProcessStatusEffectsTick();
        return ValueTask.CompletedTask;
    }
}
