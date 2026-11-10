using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Zone command enqueued when a client sends a valid <c>c2s_npcwalk</c> (<c>0x4B</c>) or <c>c2s_npcrun</c> (<c>0x4C</c>) packet (`FR-006`).
/// </summary>
public sealed record MovePlayerZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    int TargetMpsX,
    int TargetMpsY,
    bool IsRun) : IZoneCommand;
