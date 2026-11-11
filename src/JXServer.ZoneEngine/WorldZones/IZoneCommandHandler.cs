namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Typed single-threaded zone command handler port (<c>FR-001</c>).
/// </summary>
public interface IZoneCommandHandler<in TCommand>
    where TCommand : class, IZoneCommand
{
    ValueTask HandleAsync(
        TCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default);
}
