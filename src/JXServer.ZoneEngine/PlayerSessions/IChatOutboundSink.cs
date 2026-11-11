namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Outbound wire port for spatial player talk, multi-channel chat messages, and server announcements (<c>FR-010</c>).
/// </summary>
public interface IChatOutboundSink
{
    void SendPlayerTalkBroadcast(uint talkerNpcId, string messageUtf8) { }

    void SendChatMessage(uint senderNpcId, string senderNameUtf8, string messageUtf8) { }

    void SendSystemMessage(string messageUtf8) { }
}
