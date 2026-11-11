using JXServer.Primitives.Items;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Allocation-free singleton fallback when <see cref="IInventoryOutboundSink"/> is unsubscribed (<c>FR-011</c>).
/// </summary>
public sealed class NoOpInventoryOutboundSink : IInventoryOutboundSink
{
    public static NoOpInventoryOutboundSink Instance { get; } = new();

    private NoOpInventoryOutboundSink()
    {
    }

    public void SendGroundObjectAdd(in GroundObjectSyncSnapshot snapshot)
    {
    }

    public void SendGroundObjectRemove(int objId, byte reason)
    {
    }

    public void SendItemSync(in PlayerItemSyncSnapshot snapshot)
    {
    }

    public void SendItemRemove(uint runtimeItemId)
    {
    }

    public void SendMoneySync(int inventoryMoney, int repositoryMoney, int tradeMoney = 0)
    {
    }

    public void SendPlayerMoveItemConfirm(
        byte downY,
        ItemContainerPlace downPos,
        byte downX,
        byte upY,
        byte upX,
        ItemContainerPlace upPos)
    {
    }

    public void SendItemAutoMoveUnlock()
    {
    }
}
