using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.Chats;

/// <summary>
/// Zone command enqueued when a client sends <c>c2s_playertalk</c> (<c>0x52</c>) for <c>3x3</c> AOI speech broadcast (`FR-010`).
/// </summary>
public sealed record SendNearbyTalkZoneCommand(
    Guid SenderCharacterId,
    long LeaseEpoch,
    string MessageUtf8) : IZoneCommand
{
    public Guid? FaultIsolationCharacterId => SenderCharacterId;
}
