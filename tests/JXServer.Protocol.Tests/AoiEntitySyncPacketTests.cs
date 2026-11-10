using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Characters;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.PlayerSessions;
using Xunit;

namespace JXServer.Protocol.Tests;

public sealed class AoiEntitySyncPacketTests
{
    [Fact]
    public void RequestPlayerC2SPacket_ShouldRoundTrip_0x48()
    {
        var original = new RequestPlayerC2SPacket(TargetNpcId: 10042u);
        Span<byte> buffer = stackalloc byte[RequestPlayerC2SPacket.PayloadSize];
        original.WritePayload(buffer);

        Assert.Equal(ProtocolOpcodes.c2s_requestplayer, buffer[0]);
        Assert.True(RequestPlayerC2SPacket.TryParse(buffer, out RequestPlayerC2SPacket parsed));
        Assert.Equal(10042u, parsed.TargetNpcId);
    }

    [Fact]
    public void RequestNpcC2SPacket_ShouldRoundTrip_0x49()
    {
        var original = new RequestNpcC2SPacket(TargetNpcId: 15u);
        Span<byte> buffer = stackalloc byte[RequestNpcC2SPacket.PayloadSize];
        original.WritePayload(buffer);

        Assert.Equal(ProtocolOpcodes.c2s_requestnpc, buffer[0]);
        Assert.True(RequestNpcC2SPacket.TryParse(buffer, out RequestNpcC2SPacket parsed));
        Assert.Equal(15u, parsed.TargetNpcId);
    }

    [Fact]
    public void SyncNpcMinS2CPacket_ShouldRoundTrip_0x4D_18Bytes()
    {
        var original = new SyncNpcMinS2CPacket(
            NpcId: 10001u,
            MpsX: 51200,
            MpsY: 102400,
            Doing: (byte)EntityDoingPosture.Run,
            LifePer: 95,
            State: 0,
            Camp: (byte)EntityCamp.Begin,
            Dir: 1);

        Span<byte> buffer = stackalloc byte[SyncNpcMinS2CPacket.PayloadSize];
        original.WritePayload(buffer);

        Assert.Equal(18, buffer.Length);
        Assert.Equal(ProtocolOpcodes.s2c_syncnpcmin, buffer[0]);
        Assert.True(SyncNpcMinS2CPacket.TryParse(buffer, out SyncNpcMinS2CPacket parsed));
        Assert.Equal(original, parsed);
    }

    [Fact]
    public void SyncNpcFullS2CPacket_CreateFromSnapshot_ShouldEncodeNpcSettingIdxAndVietnameseName()
    {
        var snapshot = new EntityFullSyncSnapshot(
            NpcId: 7u,
            MpsX: 52288,
            MpsY: 101856,
            NpcSettingIdx: (10u << 16) | 382u,
            Series: ElementSeries.Earth,
            LifePer: 100,
            Doing: EntityDoingPosture.Stand,
            Camp: EntityCamp.Animal,
            Dir: 0,
            Kind: EntityKind.DialogNpc,
            IsSpecialNpc: false,
            NameUtf8: "Chủ tiệm tạp hóa");

        SyncNpcFullS2CPacket packet = SyncNpcFullS2CPacket.CreateFromSnapshot(in snapshot);
        byte[] payload = packet.ToPayloadBytes();

        Assert.Equal(ProtocolOpcodes.s2c_syncnpc, payload[0]);
        Assert.Equal(7u, packet.NpcId);
        Assert.Equal(382, packet.TemplateId);
        Assert.Equal(10, packet.Level);
        Assert.Equal(EntityKind.DialogNpc, packet.Kind);
        Assert.True(payload.Length > SyncNpcFullS2CPacket.HeaderSize);
    }
}
