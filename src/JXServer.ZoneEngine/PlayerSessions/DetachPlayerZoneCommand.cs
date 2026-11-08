using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Zone command dispatched to detach a player from a <c>MapZoneWorker</c> and persist their latest world snapshot.
/// </summary>
public sealed record DetachPlayerZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    PlayerDetachReason Reason,
    TaskCompletionSource? CompletionSignal = null) : IZoneCommand;
