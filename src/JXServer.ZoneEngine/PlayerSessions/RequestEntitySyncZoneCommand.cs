using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Zone command enqueued when a client sends <c>c2s_requestplayer</c> (<c>0x48</c>) or <c>c2s_requestnpc</c> (<c>0x49</c>) (`FR-003`).
/// </summary>
public sealed record RequestEntitySyncZoneCommand(
    Guid RequesterCharacterId,
    long LeaseEpoch,
    uint TargetNpcId,
    bool IsPlayerRequest) : IZoneCommand;
