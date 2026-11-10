using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Zone command enqueued on the destination <c>MapZoneWorker</c> when a player transitions maps on the same server (`FR-009`).
/// </summary>
public sealed record AttachTransferredPlayerZoneCommand(
    PlayerZoneState Player,
    int ArrivalMpsX,
    int ArrivalMpsY,
    byte ArrivalFightMode) : IZoneCommand;
