using JXServer.Application.Characters;
using JXServer.Application.Configuration;
using JXServer.Application.Logging;
using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.Domain.Characters;
using JXServer.Domain.SessionLeases;
using JXServer.Primitives.SessionLeases;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.Application.SessionLeases;

/// <summary>
/// Application service orchestrating character selection (`0x64`), single-use handoff ticket issuance (`0x34`) and redemption (`0x42`),
/// heartbeat updates, and session lease release (`FR-007`, `FR-008`, `Constitution VI`).
/// </summary>
public sealed class SessionLeaseAppService : ISessionLeaseAppService
{
    private readonly ISessionLeaseRepository _sessionLeaseRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly SessionLeaseManager _sessionLeaseManager;
    private readonly AccountOperationLockProvider _lockProvider;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<ApplicationOptions> _options;
    private readonly ILogger<SessionLeaseAppService> _logger;

    public SessionLeaseAppService(
        ISessionLeaseRepository sessionLeaseRepository,
        ICharacterRepository characterRepository,
        SessionLeaseManager sessionLeaseManager,
        AccountOperationLockProvider lockProvider,
        TimeProvider timeProvider,
        IOptions<ApplicationOptions> options,
        ILogger<SessionLeaseAppService> logger)
    {
        _sessionLeaseRepository = sessionLeaseRepository;
        _characterRepository = characterRepository;
        _sessionLeaseManager = sessionLeaseManager;
        _lockProvider = lockProvider;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    public async Task<WorldHandoffTicketDto> SelectCharacterForWorldEntryAsync(
        SelectCharacterInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        ApplicationOptions cfg = _options.Value;
        SemaphoreSlim accountLock = _lockProvider.GetLock(input.AccountId);
        await accountLock.WaitAsync(cancellationToken);
        try
        {
            SessionLease? lease = await _sessionLeaseRepository.FindByAccountIdAsync(input.AccountId, cancellationToken);
            if (lease is null ||
                lease.LeaseEpoch != input.LeaseEpoch ||
                lease.State == SessionLeaseState.Offline)
            {
                return CreateDeniedTicket(input.AccountId, input.LeaseEpoch, input.RoleNameUnicode, cfg);
            }

            string normalizedRoleName = Character.NormalizeRoleName(input.RoleNameUnicode);
            Character? character = await _characterRepository.FindByNormalizedRoleNameAsync(normalizedRoleName, cancellationToken);
            if (character is null || character.AccountId != input.AccountId)
            {
                return CreateDeniedTicket(input.AccountId, input.LeaseEpoch, input.RoleNameUnicode, cfg);
            }

            TimeSpan ticketTtl = TimeSpan.FromSeconds(Math.Max(5, cfg.HandoffTicketTtlSeconds));
            (bool permitted, Guid ticketGuid, ushort resolvedMapId, bool wasRedirected) = _sessionLeaseManager.TryIssueHandoffTicket(
                lease,
                character,
                ticketTtl);

            if (!permitted)
            {
                return CreateDeniedTicket(input.AccountId, input.LeaseEpoch, character.RoleName, cfg);
            }

            if (wasRedirected)
            {
                await _characterRepository.UpdateAsync(character, autoSave: true, cancellationToken: cancellationToken);
            }

            await _sessionLeaseRepository.UpsertAsync(lease, cancellationToken);

            ApplicationLogMessages.LogHandoffTicketIssued(
                _logger,
                ticketGuid,
                input.AccountId,
                character.RoleName,
                resolvedMapId,
                lease.LeaseEpoch);

            return new WorldHandoffTicketDto(
                Permitted: true,
                TicketGuid: ticketGuid,
                LeaseEpoch: lease.LeaseEpoch,
                AccountId: input.AccountId,
                CharacterId: character.Id,
                RoleNameUnicode: character.RoleName,
                TargetMapId: resolvedMapId,
                GameServerHost: cfg.AdvertisedGameServerHost,
                GameServerPort: cfg.AdvertisedGameServerPort);
        }
        finally
        {
            accountLock.Release();
        }
    }

    public async Task<RedeemHandoffTicketResultDto> RedeemWorldHandoffTicketAsync(
        RedeemHandoffTicketInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.TicketGuid == Guid.Empty)
        {
            return new RedeemHandoffTicketResultDto(false, 0, null);
        }

        SessionLease? candidateLease = await _sessionLeaseRepository.FindByHandoffTicketAsync(input.TicketGuid, cancellationToken);
        if (candidateLease is null)
        {
            return new RedeemHandoffTicketResultDto(false, 0, null);
        }

        SemaphoreSlim accountLock = _lockProvider.GetLock(candidateLease.AccountId);
        await accountLock.WaitAsync(cancellationToken);
        try
        {
            SessionLease? lease = await _sessionLeaseRepository.FindByAccountIdAsync(candidateLease.AccountId, cancellationToken);
            if (lease is null || lease.ActiveCharacterId is null)
            {
                return new RedeemHandoffTicketResultDto(false, 0, null);
            }

            Guid characterId = lease.ActiveCharacterId.Value;
            DateTimeOffset nowUtc = _timeProvider.GetUtcNow();
            if (!lease.ConsumeWorldHandoffTicket(input.TicketGuid, nowUtc, input.GameServerConnectionId))
            {
                return new RedeemHandoffTicketResultDto(false, 0, null);
            }

            await _sessionLeaseRepository.UpsertAsync(lease, cancellationToken);

            Character? character = await _characterRepository.FindAsync(characterId, cancellationToken);
            if (character is null)
            {
                return new RedeemHandoffTicketResultDto(false, 0, null);
            }

            ApplicationLogMessages.LogHandoffTicketRedeemed(
                _logger,
                input.TicketGuid,
                lease.AccountId,
                character.RoleName,
                lease.LeaseEpoch);

            return new RedeemHandoffTicketResultDto(true, lease.LeaseEpoch, MapToDto(character));
        }
        finally
        {
            accountLock.Release();
        }
    }

    public async Task<bool> RecordHeartbeatAsync(
        Guid accountId,
        long leaseEpoch,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty)
        {
            return false;
        }

        SemaphoreSlim accountLock = _lockProvider.GetLock(accountId);
        await accountLock.WaitAsync(cancellationToken);
        try
        {
            SessionLease? lease = await _sessionLeaseRepository.FindByAccountIdAsync(accountId, cancellationToken);
            if (lease is null || lease.LeaseEpoch != leaseEpoch || lease.State == SessionLeaseState.Offline)
            {
                return false;
            }

            lease.RecordHeartbeat(_timeProvider.GetUtcNow());
            await _sessionLeaseRepository.UpsertAsync(lease, cancellationToken);
            return true;
        }
        finally
        {
            accountLock.Release();
        }
    }

    public async Task CompleteLogoutOrEvictAsync(
        ReleaseSessionLeaseInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.AccountId == Guid.Empty)
        {
            return;
        }

        SemaphoreSlim accountLock = _lockProvider.GetLock(input.AccountId);
        await accountLock.WaitAsync(cancellationToken);
        try
        {
            DateTimeOffset nowUtc = _timeProvider.GetUtcNow();
            SessionLease? lease = await _sessionLeaseRepository.FindByAccountIdAsync(input.AccountId, cancellationToken);

            bool persistSnapshot = input.CharacterSnapshot is not null && (
                input.LeaseEpoch == 0 ||
                (lease is not null && lease.LeaseEpoch == input.LeaseEpoch) ||
                (lease is not null && lease.LeaseEpoch == input.LeaseEpoch + 1 && lease.State == SessionLeaseState.GatewayAuthenticated && lease.ActiveCharacterId is null)
            );

            if (persistSnapshot && input.CharacterSnapshot is { } snap)
            {
                Character? character = await _characterRepository.FindAsync(snap.CharacterId, cancellationToken);
                if (character is not null && character.AccountId == input.AccountId)
                {
                    CharacterAppService.ApplyWorldSnapshot(character, snap, nowUtc);
                    await _characterRepository.UpdateAsync(character, autoSave: true, cancellationToken: cancellationToken);
                }
            }

            if (lease is not null && lease.LeaseEpoch == input.LeaseEpoch)
            {
                if (input.OnlyIfGatewayAuthenticated && lease.State != SessionLeaseState.GatewayAuthenticated)
                {
                    return;
                }

                lease.Release(nowUtc);
                await _sessionLeaseRepository.UpsertAsync(lease, cancellationToken);
            }
        }
        finally
        {
            accountLock.Release();
        }
    }

    internal static CharacterDto MapToDto(Character character) =>
        new(
            character.Id,
            character.AccountId,
            character.SlotIndex,
            character.RoleName,
            character.Gender,
            character.Series,
            character.Level,
            character.Experience,
            character.LeadExperience,
            character.MapId,
            character.RevivalMapId,
            character.RevivalPointId,
            character.MapMpsX,
            character.MapMpsY,
            character.CurrentLife,
            character.MaxLife,
            character.CurrentMana,
            character.MaxMana,
            character.CurrentStamina,
            character.MaxStamina,
            character.Strength,
            character.Dexterity,
            character.Vitality,
            character.Energy,
            character.Lucky,
            character.UnallocatedAttributePoints,
            character.UnallocatedSkillPoints,
            character.InventoryMoney,
            character.RepositoryMoney,
            character.CurrentFactionId,
            character.FirstFactionId,
            character.FactionJoinCount,
            character.Skills
                .Select(s => new CharacterSkillDto(s.SkillId, s.BaseLevel, s.BonusLevel, s.ExpPercent))
                .ToArray(),
            character.PkState,
            character.PkValue,
            character.ActiveAuraSkillId,
            character.Items
                .Select(i => new CharacterItemDto(
                    i.Id,
                    i.Genre,
                    i.DetailType,
                    i.ParticularType,
                    i.Level,
                    i.Series,
                    i.Quality,
                    i.RandomSeed,
                    i.GeneratorVersion,
                    i.Luck,
                    i.CurrentDurability,
                    i.MaxDurability,
                    i.StackCount,
                    i.ContainerPlace,
                    i.GridX,
                    i.GridY,
                    i.MagicAttributes
                        .Select(m => new CharacterItemMagicAttributeDto(
                            m.SlotIndex,
                            m.AttributeId,
                            m.Value0,
                            m.Value1,
                            m.Value2,
                            m.EnchaseLevel))
                        .ToArray(),
                    i.BindState,
                    i.ExpireTimeUnixSeconds,
                    i.CreatorName,
                    i.IsBroken,
                    i.EnchaseLevel))
                .ToArray(),
            character.TaskValues,
            character.RepositoryPasswordHash);

    private static WorldHandoffTicketDto CreateDeniedTicket(
        Guid accountId,
        long leaseEpoch,
        string roleNameUnicode,
        ApplicationOptions cfg) =>
        new(
            Permitted: false,
            TicketGuid: Guid.Empty,
            LeaseEpoch: leaseEpoch,
            AccountId: accountId,
            CharacterId: Guid.Empty,
            RoleNameUnicode: roleNameUnicode ?? string.Empty,
            TargetMapId: 0,
            GameServerHost: cfg.AdvertisedGameServerHost,
            GameServerPort: cfg.AdvertisedGameServerPort);
}
