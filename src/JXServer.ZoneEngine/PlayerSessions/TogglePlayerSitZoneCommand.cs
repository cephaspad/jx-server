using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Zone command enqueued when a client sends a <c>c2s_npcsit</c> (<c>0x71</c>) posture toggle packet (`FR-007`).
/// </summary>
public sealed record TogglePlayerSitZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    bool IsSitting) : IZoneCommand;
