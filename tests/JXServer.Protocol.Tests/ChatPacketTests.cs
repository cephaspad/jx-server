using JXServer.Adapter.LegacyJX1.Framing;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Chats;
using Xunit;

namespace JXServer.Protocol.Tests;

public sealed class ChatPacketTests
{
    [Fact]
    public void PlayerTalk_C2S_And_S2C_ShouldRoundTripVietnameseTcvn3()
    {
        var c2s = new PlayerTalkC2SPacket(10001u, "Xin chào Ba Lăng huyện!");
        byte[] c2sBytes = c2s.ToPayloadBytes();

        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(c2sBytes));
        Assert.True(PlayerTalkC2SPacket.TryParse(c2sBytes, out PlayerTalkC2SPacket parsedC2S));
        Assert.Equal(10001u, parsedC2S.TalkerNpcId);
        Assert.Equal("Xin chào Ba Lăng huyện!", parsedC2S.MessageUtf8);

        var s2c = new PlayerTalkS2CPacket(10001u, "Xin chào Ba Lăng huyện!");
        byte[] s2cBytes = s2c.ToPayloadBytes();
        Assert.True(PlayerTalkS2CPacket.TryParse(s2cBytes, out PlayerTalkS2CPacket parsedS2C));
        Assert.Equal(10001u, parsedS2C.TalkerNpcId);
        Assert.Equal("Xin chào Ba Lăng huyện!", parsedS2C.MessageUtf8);
    }

    [Fact]
    public void PlayerSendChat_And_ChatGetSomeone_And_SystemExtend_ShouldRoundTrip()
    {
        var sendChat = new PlayerSendChatC2SPacket(
            Channel: ChatChannelKind.PrivateWhisper,
            ChatType: 0,
            TargetNpcId: 10002u,
            TargetIdx: 0,
            SentencePrefixUtf8: "Độc Cô Cầu Bại",
            SentenceUtf8: "Mật đàm tại Vũ Lăng Sơn");

        byte[] sendChatBytes = sendChat.ToPayloadBytes();
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(sendChatBytes));
        Assert.True(PlayerSendChatC2SPacket.TryParse(sendChatBytes, out PlayerSendChatC2SPacket parsedChat));
        Assert.Equal(ChatChannelKind.PrivateWhisper, parsedChat.Channel);
        Assert.Equal(10002u, parsedChat.TargetNpcId);
        Assert.Equal("Độc Cô Cầu Bại", parsedChat.SentencePrefixUtf8);
        Assert.Equal("Mật đàm tại Vũ Lăng Sơn", parsedChat.SentenceUtf8);

        var getSomeone = new ChatGetSomeoneS2CPacket(10001u, "Lệnh Hồ Xung", "Huynh đệ tương phùng!");
        byte[] getSomeoneBytes = getSomeone.ToPayloadBytes();
        Assert.True(ChatGetSomeoneS2CPacket.TryParse(getSomeoneBytes, out ChatGetSomeoneS2CPacket parsedGet));
        Assert.Equal(10001u, parsedGet.SenderNpcId);
        Assert.Equal("Lệnh Hồ Xung", parsedGet.SenderNameUtf8);
        Assert.Equal("Huynh đệ tương phùng!", parsedGet.MessageUtf8);

        var sysExtend = new SystemInfoExtendS2CPacket("Bạn chưa đạt cấp 10, không thể rời khỏi tân thủ thôn.");
        byte[] sysBytes = sysExtend.ToPayloadBytes();
        Assert.True(SystemInfoExtendS2CPacket.TryParse(sysBytes, out SystemInfoExtendS2CPacket parsedSys));
        Assert.Equal("Bạn chưa đạt cấp 10, không thể rời khỏi tân thủ thôn.", parsedSys.MessageUtf8);
    }

    [Fact]
    public void ChatSetChannelC2SPacket_ShouldRoundTrip_0x60()
    {
        var packet = new ChatSetChannelC2SPacket(0x05u);
        Span<byte> buf = stackalloc byte[ChatSetChannelC2SPacket.PayloadSize];
        packet.WritePayload(buf);

        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(buf));
        Assert.True(ChatSetChannelC2SPacket.TryParse(buf, out ChatSetChannelC2SPacket parsed));
        Assert.Equal(0x05u, parsed.SubscribedChannelMask);
    }
}
