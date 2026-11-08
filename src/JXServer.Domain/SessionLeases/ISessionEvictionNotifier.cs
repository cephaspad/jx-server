namespace JXServer.Domain.SessionLeases;

/// <summary>
/// Port notified when an active session lease is superseded by a new login (`bKickOnline != 0`) or administrative kick,
/// allowing the transport/zone layers to close stale sockets and detach any in-world character (`FR-008`).
/// </summary>
public interface ISessionEvictionNotifier
{
    /// <summary>
    /// Evicts any active Bishop and/or GameServer session matching the superseded lease snapshot.
    /// </summary>
    Task EvictSessionAsync(
        Guid accountId,
        long evictedLeaseEpoch,
        string? bishopConnectionId,
        string? gameServerConnectionId,
        Guid? activeCharacterId,
        int? activeMapId,
        CancellationToken cancellationToken = default);
}
