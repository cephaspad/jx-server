using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Chats;

/// <summary>
/// Zone command enqueued when a client sends <c>c2s_chatsetchannel</c> (<c>0x60</c>) (`FR-011`).
/// </summary>
public sealed record UpdateChatChannelMaskZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    uint SubscribedChannelMask) : IZoneCommand;
