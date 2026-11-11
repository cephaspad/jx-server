using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Domain command handler executing world session lifecycle commands (<c>Domain 004</c>):
/// player attachment, map-transfer arrival, sync completion, entity sync requests,
/// heartbeat ping processing, and session detachment.
/// </summary>
public sealed class WorldSessionCommandHandler :
    IZoneCommandHandler<AttachPlayerZoneCommand>,
    IZoneCommandHandler<AttachTransferredPlayerZoneCommand>,
    IZoneCommandHandler<CompletePlayerSyncZoneCommand>,
    IZoneCommandHandler<RequestEntitySyncZoneCommand>,
    IZoneCommandHandler<ProcessPlayerPingZoneCommand>,
    IZoneCommandHandler<DetachPlayerZoneCommand>
{
    public ValueTask HandleAsync(
        AttachPlayerZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        return context.Worker.HandleAttachPlayerAsync(command, context.CurrentFrame, cancellationToken);
    }

    public ValueTask HandleAsync(
        AttachTransferredPlayerZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleAttachTransferredPlayer(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        CompletePlayerSyncZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleCompletePlayerSync(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        RequestEntitySyncZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleRequestEntitySync(command);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        ProcessPlayerPingZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        return context.Worker.HandleProcessPlayerPingAsync(command, context.CurrentFrame, cancellationToken);
    }

    public ValueTask HandleAsync(
        DetachPlayerZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        return context.Worker.HandleDetachPlayerAsync(command, cancellationToken);
    }
}
