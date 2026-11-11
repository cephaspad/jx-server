using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Items;

/// <summary>
/// Domain command handler executing inventory manipulations, equipment changes,
/// consumable use, item dropping/splitting, and ground loot pickups (<c>Domain 008</c> &amp; <c>Domain 011</c>).
/// </summary>
public sealed class InventoryLootCommandHandler :
    IZoneCommandHandler<RequestGroundObjectSyncZoneCommand>,
    IZoneCommandHandler<PickupGroundObjectZoneCommand>,
    IZoneCommandHandler<MoveInventoryItemZoneCommand>,
    IZoneCommandHandler<UseInventoryItemZoneCommand>,
    IZoneCommandHandler<DropInventoryItemZoneCommand>,
    IZoneCommandHandler<DivideInventoryItemZoneCommand>
{
    public ValueTask HandleAsync(
        RequestGroundObjectSyncZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleRequestGroundObjectSync(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        PickupGroundObjectZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandlePickupGroundObject(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        MoveInventoryItemZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleMoveInventoryItem(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        UseInventoryItemZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        return context.Worker.HandleUseInventoryItemAsync(command, context.CurrentFrame, cancellationToken);
    }

    public ValueTask HandleAsync(
        DropInventoryItemZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleDropInventoryItem(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }

    public ValueTask HandleAsync(
        DivideInventoryItemZoneCommand command,
        ZoneExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        context.Worker.HandleDivideInventoryItem(command, context.CurrentFrame);
        return ValueTask.CompletedTask;
    }
}
