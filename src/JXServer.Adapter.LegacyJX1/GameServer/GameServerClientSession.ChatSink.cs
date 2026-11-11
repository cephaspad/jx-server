using System.Buffers;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.Adapter.LegacyJX1.GameServer;

public sealed partial class GameServerClientSession : IChatOutboundSink
{
    public void SendPlayerTalkBroadcast(uint talkerNpcId, string messageUtf8)
    {
        var writer = new ArrayBufferWriter<byte>(320);
        var talk = new PlayerTalkS2CPacket(talkerNpcId, messageUtf8);
        _frameProcessor.WriteEncryptedS2CFrame(writer, talk.ToPayloadBytes());
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendChatMessage(uint senderNpcId, string senderNameUtf8, string messageUtf8)
    {
        var writer = new ArrayBufferWriter<byte>(352);
        var chat = new ChatGetSomeoneS2CPacket(senderNpcId, senderNameUtf8, messageUtf8);
        _frameProcessor.WriteEncryptedS2CFrame(writer, chat.ToPayloadBytes());
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendSystemMessage(string messageUtf8)
    {
        var writer = new ArrayBufferWriter<byte>(320);
        var sysMsg = new SystemInfoExtendS2CPacket(messageUtf8);
        _frameProcessor.WriteEncryptedS2CFrame(writer, sysMsg.ToPayloadBytes());
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }
}
