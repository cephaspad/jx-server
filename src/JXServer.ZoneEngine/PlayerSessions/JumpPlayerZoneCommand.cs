using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Zone command enqueued when a client sends a <c>c2s_npcjump</c> (<c>0x4E</c>) packet (`FR-007`).
/// </summary>
public sealed record JumpPlayerZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    int TargetMpsX,
    int TargetMpsY) : IZoneCommand;
