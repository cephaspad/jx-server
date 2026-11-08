using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Zone command dispatched upon receiving the 9-byte <c>0x43</c> (<c>c2s_syncend</c>) reply from the client.
/// </summary>
public sealed record CompletePlayerSyncZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    uint ClientToken,
    uint ClientTick) : IZoneCommand;
