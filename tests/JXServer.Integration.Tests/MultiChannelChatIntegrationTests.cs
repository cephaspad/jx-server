using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Chats;
using Xunit;

namespace JXServer.Integration.Tests;

public sealed class MultiChannelChatIntegrationTests
{
    [Fact]
    public async Task NearbyTalk_WorldChat_PrivateWhisper_AndChannelMask_ShouldRouteWithVietnameseFidelity()
    {
        await using var harness = await IntegrationTestServerHarness.StartAsync();

        await using SimulatedJxClientConnection gsA = await LoginAndEnterWorldAsync(harness, "chatu1", "Lệnh Hồ Xung");
        await using SimulatedJxClientConnection gsB = await LoginAndEnterWorldAsync(harness, "chatu2", "Nhậm Doanh Doanh");
        await using SimulatedJxClientConnection gsC = await LoginAndEnterWorldAsync(harness, "chatu3", "Điền Bá Quang");

        // 1. Nearby speech bubble (0x52 c2s_playertalk -> 0x5B s2c_playertalk)
        var talkC2S = new PlayerTalkC2SPacket(0, "Xin chào các vị bằng hữu tại Ba Lăng huyện!");
        await gsA.SendEncryptedPayloadAsync(talkC2S.ToPayloadBytes());

        byte[] talkS2CBytes = await gsB.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_playertalk);
        Assert.True(PlayerTalkS2CPacket.TryParse(talkS2CBytes, out PlayerTalkS2CPacket talkS2C));
        Assert.Equal("Xin chào các vị bằng hữu tại Ba Lăng huyện!", talkS2C.MessageUtf8);

        // 2. Player C unsubscribes from World chat (bit 1 = 0x02) via 0x60 c2s_chatsetchannel
        byte[] maskBuf = new byte[ChatSetChannelC2SPacket.PayloadSize];
        new ChatSetChannelC2SPacket(SubscribedChannelMask: 1u << (int)ChatChannelKind.PrivateWhisper).WritePayload(maskBuf);
        await gsC.SendEncryptedPayloadAsync(maskBuf);

        // 3. Player A sends World Chat (0x56 Channel 1) -> Player B receives 0x7E s2c_chatgetsomeone
        var worldChat = new PlayerSendChatC2SPacket(
            Channel: ChatChannelKind.World,
            ChatType: 0,
            TargetNpcId: 0,
            TargetIdx: 0,
            SentencePrefixUtf8: string.Empty,
            SentenceUtf8: "Anh hùng thiên hạ hội tụ!");
        await gsA.SendEncryptedPayloadAsync(worldChat.ToPayloadBytes());

        byte[] worldRecvB = await gsB.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_chatgetsomeone);
        Assert.True(ChatGetSomeoneS2CPacket.TryParse(worldRecvB, out ChatGetSomeoneS2CPacket parsedWorldB));
        Assert.Equal("Lệnh Hồ Xung", parsedWorldB.SenderNameUtf8);
        Assert.Equal("Anh hùng thiên hạ hội tụ!", parsedWorldB.MessageUtf8);

        // 4. Player A sends Private Whisper (0x56 Channel 3) to "Điền Bá Quang" -> Player C receives 0x7E
        var whisperToC = new PlayerSendChatC2SPacket(
            Channel: ChatChannelKind.PrivateWhisper,
            ChatType: 0,
            TargetNpcId: 0,
            TargetIdx: 0,
            SentencePrefixUtf8: "Điền Bá Quang",
            SentenceUtf8: "Mật đàm riêng cho Điền huynh!");
        await gsA.SendEncryptedPayloadAsync(whisperToC.ToPayloadBytes());

        byte[] whisperRecvC = await gsC.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_chatgetsomeone);
        Assert.True(ChatGetSomeoneS2CPacket.TryParse(whisperRecvC, out ChatGetSomeoneS2CPacket parsedWhisperC));
        Assert.Equal("Lệnh Hồ Xung", parsedWhisperC.SenderNameUtf8);
        Assert.Equal("Mật đàm riêng cho Điền huynh!", parsedWhisperC.MessageUtf8);
    }

    private static async Task<SimulatedJxClientConnection> LoginAndEnterWorldAsync(
        IntegrationTestServerHarness harness,
        string username,
        string roleName)
    {
        Guid ticket;
        await using (var bishop = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.BishopPort))
        {
            await bishop.SendEncryptedStructAsync(AccountLoginC2SPacket.Create(username, "pass123", kickOnline: true));
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x41
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x37

            await bishop.SendEncryptedPayloadAsync(
                new CreateCharacterC2SPacket(0, CharacterGender.Male, ElementSeries.Metal, 53, roleName).ToPayloadBytes());
            _ = await bishop.ReadNextDecryptedPayloadAsync(); // 0x8C
            var notify = MemoryMarshal.Read<NotifyPlayerLoginS2CPacket>(await bishop.ReadNextDecryptedPayloadAsync());
            ticket = notify.GetTicketGuid();
        }

        var gs = await SimulatedJxClientConnection.ConnectAndCompleteHandshakeAsync(harness.GameServerPort);
        await gs.SendEncryptedStructAsync(LogicLoginC2SPacket.Create(ticket));
        _ = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_syncend);
        await gs.SendEncryptedStructAsync(SyncEndC2SPacket.Create(0x11112222u, 0x33334444u));
        _ = await gs.ReadUntilOpcodeAsync(ProtocolOpcodes.s2c_syncnpcminplayer);
        return gs;
    }
}
