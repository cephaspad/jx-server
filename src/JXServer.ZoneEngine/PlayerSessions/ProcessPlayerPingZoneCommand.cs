using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Zone command dispatched upon receiving the 9-byte <c>0x70</c> (<c>c2s_ping</c>) heartbeat reply from the client.
/// </summary>
public sealed record ProcessPlayerPingZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    uint EchoedServerTimestamp,
    uint ClientTimestamp) : IZoneCommand;
