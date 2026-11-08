using JXServer.Domain.Accounts;
using JXServer.Domain.Characters;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Accounts;

namespace JXServer.Domain.SessionLeases;

/// <summary>
/// Domain service coordinating <see cref="SessionLease"/> epoch fencing, active session takeover rules (`FR-008`),
/// and world handoff ticket resolution with unloaded-map fallback (`FR-007`).
/// Purely enforces cross-aggregate domain rules without calling database save operations or external notifications directly (`Constitution VI`).
/// </summary>
public sealed class SessionLeaseManager
{
    private readonly ISessionLeaseRepository _sessionLeaseRepository;
    private readonly IMapCatalog _mapCatalog;
    private readonly IPlayerTemplateCatalog _playerTemplateCatalog;
    private readonly TimeProvider _timeProvider;

    public SessionLeaseManager(
        ISessionLeaseRepository sessionLeaseRepository,
        IMapCatalog mapCatalog,
        IPlayerTemplateCatalog playerTemplateCatalog,
        TimeProvider timeProvider)
    {
        _sessionLeaseRepository = sessionLeaseRepository;
        _mapCatalog = mapCatalog;
        _playerTemplateCatalog = playerTemplateCatalog;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Evaluates whether <paramref name="account"/> can acquire or supersede a gateway session lease according to <paramref name="kickOnline"/> (`FR-008`),
    /// advancing the <paramref name="account"/> epoch and transitioning the <see cref="SessionLease"/> in memory.
    /// </summary>
    public async Task<(
        LoginResultCode ResultCode,
        SessionLease? Lease,
        bool HasEvictedActiveLease,
        long EvictedEpoch,
        string? OldBishopConnectionId,
        string? OldGameServerConnectionId,
        Guid? OldCharacterId,
        int? OldMapId)> TryAcquireGatewayLeaseAsync(
        Account account,
        bool kickOnline,
        string? bishopConnectionId,
        TimeSpan heartbeatTimeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        DateTimeOffset nowUtc = _timeProvider.GetUtcNow();
        SessionLease? existingLease = await _sessionLeaseRepository.FindByAccountIdAsync(account.Id, cancellationToken);

        bool hasActiveLease = existingLease is not null && existingLease.IsActive(nowUtc, heartbeatTimeout);
        if (hasActiveLease && !kickOnline)
        {
            return (LoginResultCode.AccountAlreadyOnline, null, false, 0, null, null, null, null);
        }

        long evictedEpoch = existingLease?.LeaseEpoch ?? 0;
        string? oldBishopConnId = existingLease?.BishopConnectionId;
        string? oldGsConnId = existingLease?.GameServerConnectionId;
        Guid? oldCharacterId = existingLease?.ActiveCharacterId;
        int? oldMapId = existingLease?.AssignedMapId;

        long newEpoch = account.AdvanceLeaseEpoch(nowUtc);
        while (existingLease is not null && newEpoch <= existingLease.LeaseEpoch)
        {
            newEpoch = account.AdvanceLeaseEpoch(nowUtc);
        }

        if (existingLease is null)
        {
            existingLease = new SessionLease(account.Id, account.UserName, newEpoch, nowUtc, bishopConnectionId);
        }
        else
        {
            existingLease.BeginGatewaySession(newEpoch, nowUtc, bishopConnectionId);
        }

        return (
            LoginResultCode.Success,
            existingLease,
            hasActiveLease,
            evictedEpoch,
            oldBishopConnId,
            oldGsConnId,
            oldCharacterId,
            oldMapId);
    }

    /// <summary>
    /// Validates that <paramref name="character"/>'s map is loaded (or redirects to the fallback starter village per `FR-007`)
    /// and issues a single-use 16-byte handoff ticket on <paramref name="lease"/> in memory.
    /// </summary>
    public (bool Permitted, Guid TicketGuid, ushort ResolvedMapId, bool WasRedirected) TryIssueHandoffTicket(
        SessionLease lease,
        Character character,
        TimeSpan ticketTtl)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(character);

        if (!_mapCatalog.TryResolveActiveOrFallbackMapId(character.MapId, out ushort resolvedMapId, out bool wasRedirected))
        {
            return (false, Guid.Empty, 0, false);
        }

        DateTimeOffset nowUtc = _timeProvider.GetUtcNow();
        if (wasRedirected)
        {
            int spawnX = 52032;
            int spawnY = 101696;
            if (_playerTemplateCatalog.TryGetStarterVillage(resolvedMapId, out StarterVillageTemplate? fallbackVillage) &&
                fallbackVillage is not null)
            {
                spawnX = fallbackVillage.SpawnMpsX;
                spawnY = fallbackVillage.SpawnMpsY;
            }

            character.UpdateWorldSnapshot(
                resolvedMapId,
                spawnX,
                spawnY,
                character.CurrentLife,
                character.CurrentMana,
                character.CurrentStamina,
                nowUtc);
        }

        Guid ticketGuid = Guid.NewGuid();
        lease.IssueWorldHandoffTicket(character.Id, character.RoleName, resolvedMapId, ticketGuid, nowUtc, ticketTtl);

        return (true, ticketGuid, resolvedMapId, wasRedirected);
    }
}
