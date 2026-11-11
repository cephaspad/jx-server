namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Zero-boxing invoker delegate stored in <see cref="ZoneExtensionRegistry"/>.
/// </summary>
public readonly struct ZoneCommandRoute
{
    public delegate ValueTask CommandInvoker(
        IZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken);

    public CommandInvoker Invoker { get; }

    public ZoneCommandRoute(CommandInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(invoker);
        Invoker = invoker;
    }

    public ValueTask InvokeAsync(
        IZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken) =>
        Invoker(command, context, cancellationToken);
}
