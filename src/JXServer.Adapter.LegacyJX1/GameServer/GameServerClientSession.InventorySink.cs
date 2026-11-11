using System.Buffers;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Items;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.Adapter.LegacyJX1.GameServer;

public sealed partial class GameServerClientSession : IInventoryOutboundSink
{
    public void SendGroundObjectAdd(in GroundObjectSyncSnapshot snapshot)
    {
        var writer = new ArrayBufferWriter<byte>(128);
        byte[] payload = ObjAddS2CPacket.CreatePayloadBytes(
            snapshot.Dir,
            snapshot.ObjId,
            snapshot.DataId,
            snapshot.MpsX,
            snapshot.MpsY,
            snapshot.MoneyNum,
            snapshot.ItemId,
            state: 0,
            snapshot.ItemWidth,
            snapshot.Genre,
            snapshot.ItemHeight,
            snapshot.ColorId,
            flag: 0,
            snapshot.NameUtf8);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendGroundObjectRemove(int objId, byte reason)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new ObjRemoveS2CPacket(objId, reason);
        Span<byte> payload = stackalloc byte[ObjRemoveS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendItemSync(in PlayerItemSyncSnapshot snapshot)
    {
        var writer = new ArrayBufferWriter<byte>(96);
        SyncItemS2CPacket pkt = SyncItemS2CPacket.CreateFromSnapshot(in snapshot);
        Span<byte> payload = stackalloc byte[SyncItemS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendItemRemove(uint runtimeItemId)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new RemoveItemS2CPacket(runtimeItemId);
        Span<byte> payload = stackalloc byte[RemoveItemS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendMoneySync(int inventoryMoney, int repositoryMoney, int tradeMoney = 0)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var pkt = new SyncMoneyS2CPacket(inventoryMoney, repositoryMoney, tradeMoney);
        Span<byte> payload = stackalloc byte[SyncMoneyS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPlayerMoveItemConfirm(
        byte downY,
        ItemContainerPlace downPos,
        byte downX,
        byte upY,
        byte upX,
        ItemContainerPlace upPos)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new PlayerMoveItemS2CPacket(downY, downPos, downX, upY, upX, upPos);
        Span<byte> payload = stackalloc byte[PlayerMoveItemS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendItemAutoMoveUnlock()
    {
        var writer = new ArrayBufferWriter<byte>(8);
        var pkt = new ItemAutoMoveS2CPacket();
        Span<byte> payload = stackalloc byte[ItemAutoMoveS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }
}
