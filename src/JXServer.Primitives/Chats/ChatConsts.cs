namespace JXServer.Primitives.Chats;

/// <summary>
/// Validation and framing constants for legacy JX1 player speech (<c>0x52</c>/<c>0x5B</c>),
/// multi-channel chat (<c>0x56</c>/<c>0x7E</c>), and system broadcasts (<c>0xFB</c>).
/// </summary>
public static class ChatConsts
{
    public const int MaxTalkMessageBytes = 256;
    public const int MaxChatSentenceBytes = 256;
    public const int SenderNameBufferLength = 32;
    public const int SentencePrefixBufferLength = 32;
    public const uint DefaultSubscribedChannelMask = 0xFFFFFFFFu;
    public const uint SystemExtendHeader = 0x00010032u;
    public const string DefaultSystemPrefixUtf8 = "Hệ thống";
}
