using System.Collections.Concurrent;
using JXServer.Adapter.LegacyJX1.GameServer;
using JXServer.Domain.SessionLeases;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.DependencyInjection;

namespace JXServer.Adapter.LegacyJX1.Bishop;

/// <summary>
/// Tracks active <see cref="BishopClientSession"/> and <see cref="GameServerClientSession"/> instances
/// and implements <see cref="ISessionEvictionNotifier"/> to immediately close superseded sockets and flush zone snapshots (`FR-008`).
/// </summary>
public sealed class LegacySessionEvictionNotifier : ISessionEvictionNotifier
{
    private readonly ConcurrentDictionary<string, BishopClientSession> _bishopSessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, GameServerClientSession> _gameServerSessions = new(StringComparer.Ordinal);
    private readonly IServiceProvider _serviceProvider;

    public LegacySessionEvictionNotifier(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    public void RegisterBishopSession(BishopClientSession session) =>
        _bishopSessions[session.ConnectionId] = session;

    public void UnregisterBishopSession(string connectionId) =>
        _bishopSessions.TryRemove(connectionId, out _);

    public void RegisterGameServerSession(GameServerClientSession session) =>
        _gameServerSessions[session.ConnectionId] = session;

    public void UnregisterGameServerSession(string connectionId) =>
        _gameServerSessions.TryRemove(connectionId, out _);

    public async Task EvictSessionAsync(
        Guid accountId,
        long evictedLeaseEpoch,
        string? bishopConnectionId,
        string? gameServerConnectionId,
        Guid? activeCharacterId,
        int? activeMapId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(bishopConnectionId) &&
            _bishopSessions.TryRemove(bishopConnectionId, out BishopClientSession? oldBishop))
        {
            oldBishop.CloseSession();
        }

        if (activeCharacterId.HasValue && activeCharacterId.Value != Guid.Empty)
        {
            IZoneClusterRouter zoneClusterRouter = _serviceProvider.GetRequiredService<IZoneClusterRouter>();
            await zoneClusterRouter.EvictAccountFromAllZonesAsync(
                accountId,
                evictedLeaseEpoch,
                activeCharacterId,
                activeMapId.HasValue ? (ushort)activeMapId.Value : null,
                cancellationToken);
        }

        if (!string.IsNullOrEmpty(gameServerConnectionId) &&
            _gameServerSessions.TryRemove(gameServerConnectionId, out GameServerClientSession? oldGs))
        {
            oldGs.Disconnect(PlayerDetachReason.LeaseSuperseded);
        }
    }
}
