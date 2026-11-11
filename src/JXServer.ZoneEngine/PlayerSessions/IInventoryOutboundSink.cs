using JXServer.Primitives.Items;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Outbound wire port for ground loot visibility, inventory/equipment item sync, money balances,
/// and drag-and-drop container move confirmations (<c>FR-010</c>).
/// </summary>
public interface IInventoryOutboundSink
{
    void SendGroundObjectAdd(in GroundObjectSyncSnapshot snapshot) { }

    void SendGroundObjectRemove(int objId, byte reason) { }

    void SendItemSync(in PlayerItemSyncSnapshot snapshot) { }

    void SendItemRemove(uint runtimeItemId) { }

    void SendMoneySync(int inventoryMoney, int repositoryMoney, int tradeMoney = 0) { }

    void SendPlayerMoveItemConfirm(
        byte downY,
        ItemContainerPlace downPos,
        byte downX,
        byte upY,
        byte upX,
        ItemContainerPlace upPos) { }

    void SendItemAutoMoveUnlock() { }
}
