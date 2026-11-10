using JXServer.Primitives.Chats;
using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Chats;

/// <summary>
/// Zone command enqueued when a client sends <c>c2s_playersendchat</c> (<c>0x56</c>) (`FR-011`).
/// </summary>
public sealed record SendStructuredChatZoneCommand(
    Guid SenderCharacterId,
    long LeaseEpoch,
    ChatChannelKind Channel,
    uint TargetNpcId,
    string TargetOrPrefixUtf8,
    string MessageUtf8) : IZoneCommand;
