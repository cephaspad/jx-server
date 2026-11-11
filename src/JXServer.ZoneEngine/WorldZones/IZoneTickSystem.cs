namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Ordered per-frame simulation phase contract (<c>FR-002</c>).
/// </summary>
public interface IZoneTickSystem
{
    string Name { get; }

    ZoneTickPhase Phase { get; }

    /// <summary>
    /// Secondary deterministic ordering key within the same <see cref="Phase"/> (ascending; ties broken by registration order).
    /// </summary>
    int SubOrder => 0;

    ValueTask StepTickAsync(
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default);
}
