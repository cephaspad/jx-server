using JXServer.Adapter.LegacyJX1.Framing;
using JXServer.Adapter.LegacyJX1.Packets;
using Xunit;

namespace JXServer.Protocol.Tests;

public sealed class GroundObjectPacketTests
{
    [Fact]
    public void RequestObjAndObjMouseClickC2SPackets_ShouldRoundTripExactWireBytes()
    {
        var reqObj = new RequestObjC2SPacket(ObjId: 42);
        byte[] reqBuf = new byte[RequestObjC2SPacket.PayloadSize];
        reqObj.WritePayload(reqBuf);

        Assert.Equal(5, reqBuf.Length);
        Assert.Equal(ProtocolOpcodes.c2s_requestobj, reqBuf[0]);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(reqBuf));
        Assert.True(RequestObjC2SPacket.TryParse(reqBuf, out RequestObjC2SPacket parsedReq));
        Assert.Equal(42, parsedReq.ObjId);

        var clickObj = new ObjMouseClickC2SPacket(RegionId: 1001u, ObjId: 42);
        byte[] clickBuf = new byte[ObjMouseClickC2SPacket.PayloadSize];
        clickObj.WritePayload(clickBuf);

        Assert.Equal(9, clickBuf.Length);
        Assert.Equal(ProtocolOpcodes.c2s_objmouseclick, clickBuf[0]);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(clickBuf));
        Assert.True(ObjMouseClickC2SPacket.TryParse(clickBuf, out ObjMouseClickC2SPacket parsedClick));
        Assert.Equal(1001u, parsedClick.RegionId);
        Assert.Equal(42, parsedClick.ObjId);
    }

    [Fact]
    public void ObjAddAndObjRemoveS2CPackets_ShouldRoundTripExactWireBytesWithTcvn3Name()
    {
        byte[] addBytes = ObjAddS2CPacket.CreatePayloadBytes(
            dir: 16,
            objId: 105,
            dataId: 4,
            mpsX: 52032,
            mpsY: 101696,
            moneyNum: 0,
            itemId: 500001,
            state: 0,
            itemWidth: 2,
            genre: 0,
            itemHeight: 3,
            colorId: 1,
            flag: 0,
            nameUtf8: "Thanh Cương Kiếm");

        Assert.Equal(ProtocolOpcodes.s2c_objadd, addBytes[0]);
        Assert.True(ObjAddS2CPacket.TryParse(addBytes, out ObjAddS2CPacket parsedAdd));
        Assert.Equal(16, parsedAdd.Dir);
        Assert.Equal(105, parsedAdd.ObjId);
        Assert.Equal(4, parsedAdd.DataId);
        Assert.Equal(52032, parsedAdd.MpsX);
        Assert.Equal(101696, parsedAdd.MpsY);
        Assert.Equal(500001, parsedAdd.ItemId);
        Assert.Equal((byte)2, parsedAdd.ItemWidth);
        Assert.Equal((byte)3, parsedAdd.ItemHeight);
        Assert.Equal((byte)1, parsedAdd.ColorId);
        Assert.Equal("Thanh Cương Kiếm", parsedAdd.NameUtf8);

        var removePkt = new ObjRemoveS2CPacket(ObjId: 105, Reason: 1);
        byte[] removeBuf = new byte[ObjRemoveS2CPacket.PayloadSize];
        removePkt.WritePayload(removeBuf);

        Assert.Equal(6, removeBuf.Length);
        Assert.Equal(ProtocolOpcodes.s2c_objremove, removeBuf[0]);
        Assert.True(ObjRemoveS2CPacket.TryParse(removeBuf, out ObjRemoveS2CPacket parsedRemove));
        Assert.Equal(105, parsedRemove.ObjId);
        Assert.Equal((byte)1, parsedRemove.Reason);
    }
}
