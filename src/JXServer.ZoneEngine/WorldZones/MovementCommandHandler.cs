using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Domain command handler executing movement, jump, and posture commands (<c>Domain 006</c>).
/// </summary>
public sealed class MovementCommandHandler :
    IZoneCommandHandler<MovePlayerZoneCommand>,
    IZoneCommandHandler<JumpPlayerZoneCommand>,
    IZoneCommandHandler<TogglePlayerSitZoneCommand>
{
    public ValueTask HandleAsync(
        MovePlayerZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        return context.Worker.HandleMovePlayerAsync(command, context.CurrentFrame, cancellationToken);
    }

    public ValueTask HandleAsync(
        JumpPlayerZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        return context.Worker.HandleJumpPlayerAsync(command, context.CurrentFrame, cancellationToken);
    }

    public ValueTask HandleAsync(
        TogglePlayerSitZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleTogglePlayerSit(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }
}
