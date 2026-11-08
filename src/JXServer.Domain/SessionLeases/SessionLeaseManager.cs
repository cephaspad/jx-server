using JXServer.Domain.Accounts;
using JXServer.Domain.Characters;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Accounts;
using JXServer.Primitives.SessionLeases;

namespace JXServer.Domain.SessionLeases;

/// <summary>
/// Domain service coordinating <see cref="SessionLease"/> epoch fencing, active session takeover (`FR-008`),
/// and world handoff ticket resolution with unloaded-map fallback (`FR-007`).
/// </summary>
public sealed class SessionLeaseManager
{
    private readonly ISessionLeaseRepository _sessionLeaseRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly IMapCatalog _mapCatalog;
    private readonly IPlayerTemplateCatalog _playerTemplateCatalog;
    private readonly ISessionEvictionNotifier _evictionNotifier;
    private readonly TimeProvider _timeProvider;

    public SessionLeaseManager(
        ISessionLeaseRepository sessionLeaseRepository,
        IAccountRepository accountRepository,
        ICharacterRepository characterRepository,
        IMapCatalog mapCatalog,
        IPlayerTemplateCatalog playerTemplateCatalog,
        ISessionEvictionNotifier evictionNotifier,
        TimeProvider timeProvider)
    {
        _sessionLeaseRepository = sessionLeaseRepository;
        _accountRepository = accountRepository;
        _characterRepository = characterRepository;
        _mapCatalog = mapCatalog;
        _playerTemplateCatalog = playerTemplateCatalog;
        _evictionNotifier = evictionNotifier;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Acquires or supersedes a gateway session lease for <paramref name="account"/> according to <paramref name="kickOnline"/> (`FR-008`).
    /// </summary>
    public async Task<(LoginResultCode ResultCode, SessionLease? Lease)> TryAcquireGatewayLeaseAsync(
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
            return (LoginResultCode.AccountAlreadyOnline, null);
        }

        long evictedEpoch = existingLease?.LeaseEpoch ?? 0;
        string? oldBishopConnId = existingLease?.BishopConnectionId;
        string? oldGsConnId = existingLease?.GameServerConnectionId;
        Guid? oldCharacterId = existingLease?.ActiveCharacterId;
        int? oldMapId = existingLease?.AssignedMapId;

        long newEpoch = account.AdvanceLeaseEpoch(nowUtc);
        if (existingLease is not null && newEpoch <= existingLease.LeaseEpoch)
        {
            newEpoch = existingLease.LeaseEpoch + 1;
        }

        await _accountRepository.UpdateAsync(account, autoSave: true, cancellationToken: cancellationToken);

        if (existingLease is null)
        {
            existingLease = new SessionLease(account.Id, account.UserName, newEpoch, nowUtc, bishopConnectionId);
        }
        else
        {
            existingLease.BeginGatewaySession(newEpoch, nowUtc, bishopConnectionId);
        }

        await _sessionLeaseRepository.UpsertAsync(existingLease, cancellationToken);

        if (hasActiveLease)
        {
            await _evictionNotifier.EvictSessionAsync(
                account.Id,
                evictedEpoch,
                oldBishopConnId,
                oldGsConnId,
                oldCharacterId,
                oldMapId,
                cancellationToken);
        }

        return (LoginResultCode.Success, existingLease);
    }

    /// <summary>
    /// Validates that <paramref name="character"/>'s map is loaded (or redirects to the fallback starter village per `FR-007`)
    /// and issues a single-use 16-byte handoff ticket on <paramref name="lease"/>.
    /// </summary>
    public async Task<(bool Permitted, Guid TicketGuid, ushort ResolvedMapId)> TryIssueHandoffTicketAsync(
        SessionLease lease,
        Character character,
        TimeSpan ticketTtl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(character);

        if (!_mapCatalog.TryResolveActiveOrFallbackMapId(character.MapId, out ushort resolvedMapId, out bool wasRedirected))
        {
            return (false, Guid.Empty, 0);
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

            await _characterRepository.UpdateAsync(character, autoSave: true, cancellationToken: cancellationToken);
        }

        Guid ticketGuid = Guid.NewGuid();
        lease.IssueWorldHandoffTicket(character.Id, character.RoleName, resolvedMapId, ticketGuid, nowUtc, ticketTtl);
        await _sessionLeaseRepository.UpsertAsync(lease, cancellationToken);

        return (true, ticketGuid, resolvedMapId);
    }
}
