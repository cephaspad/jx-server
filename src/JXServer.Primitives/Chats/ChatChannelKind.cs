namespace JXServer.Primitives.Chats;

/// <summary>
/// Canonical chat channels (<c>btCurrChannel</c>) in <c>c2s_playersendchat</c> (<c>0x56</c>).
/// </summary>
public enum ChatChannelKind : byte
{
    All = 0,
    World = 1,
    Nearby = 2,
    PrivateWhisper = 3,
    Team = 4,
    Faction = 5,
    Tong = 6,
    Screen = 7,
    System = 8,
}
