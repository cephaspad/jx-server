namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Allocation-free singleton fallback when <see cref="IChatOutboundSink"/> is unsubscribed (<c>FR-011</c>).
/// </summary>
public sealed class NoOpChatOutboundSink : IChatOutboundSink
{
    public static NoOpChatOutboundSink Instance { get; } = new();

    private NoOpChatOutboundSink()
    {
    }

    public void SendPlayerTalkBroadcast(uint talkerNpcId, string messageUtf8)
    {
    }

    public void SendChatMessage(uint senderNpcId, string senderNameUtf8, string messageUtf8)
    {
    }

    public void SendSystemMessage(string messageUtf8)
    {
    }
}
