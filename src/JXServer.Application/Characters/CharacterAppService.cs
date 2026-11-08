using JXServer.Application.Configuration;
using JXServer.Application.SessionLeases;
using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.Domain.Accounts;
using JXServer.Domain.Characters;
using JXServer.Domain.SessionLeases;
using JXServer.Primitives.ErrorCodes;
using JXServer.Primitives.SessionLeases;
using Microsoft.Extensions.Options;

namespace JXServer.Application.Characters;

/// <summary>
/// Application service orchestrating character roster queries (`0x37`), character creation (`0x45`),
/// character deletion (`0x3A`), and unit-of-work persistence (`FR-004`, `FR-005`, `FR-006`, `Constitution VI`).
/// </summary>
public sealed class CharacterAppService : ICharacterAppService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly ISessionLeaseRepository _sessionLeaseRepository;
    private readonly CharacterManager _characterManager;
    private readonly SessionLeaseManager _sessionLeaseManager;
    private readonly AccountOperationLockProvider _lockProvider;
    private readonly IOptions<ApplicationOptions> _options;

    public CharacterAppService(
        IAccountRepository accountRepository,
        ICharacterRepository characterRepository,
        ISessionLeaseRepository sessionLeaseRepository,
        CharacterManager characterManager,
        SessionLeaseManager sessionLeaseManager,
        AccountOperationLockProvider lockProvider,
        IOptions<ApplicationOptions> options)
    {
        _accountRepository = accountRepository;
        _characterRepository = characterRepository;
        _sessionLeaseRepository = sessionLeaseRepository;
        _characterManager = characterManager;
        _sessionLeaseManager = sessionLeaseManager;
        _lockProvider = lockProvider;
        _options = options;
    }

    public async Task<IReadOnlyList<CharacterSummaryDto>> GetCharacterRosterAsync(
        GetCharacterRosterInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        List<Character> characters = await _characterRepository.GetListByAccountIdAsync(input.AccountId, cancellationToken);
        return characters
            .Select(c => new CharacterSummaryDto(
                c.SlotIndex,
                c.Id,
                c.RoleName,
                c.Gender,
                c.Series,
                c.Level))
            .ToArray();
    }

    public async Task<CreateCharacterResultDto> CreateCharacterAsync(
        CreateCharacterDto input,
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
                return new CreateCharacterResultDto(false, JXServerErrorCodes.SessionEpochSuperseded, null, null);
            }

            Account? account = await _accountRepository.FindAsync(input.AccountId, cancellationToken);
            if (account is null)
            {
                return new CreateCharacterResultDto(false, JXServerErrorCodes.AccountInvalidCredentials, null, null);
            }

            try
            {
                Character created = await _characterManager.CreateAsync(
                    account,
                    input.RoleNameUnicode,
                    input.Gender,
                    input.Series,
                    input.StarterVillageMapId,
                    cancellationToken);

                created = await _characterRepository.InsertAsync(created, autoSave: true, cancellationToken: cancellationToken);

                TimeSpan ticketTtl = TimeSpan.FromSeconds(Math.Max(5, cfg.HandoffTicketTtlSeconds));
                (bool permitted, Guid ticketGuid, ushort resolvedMapId, bool wasRedirected) = _sessionLeaseManager.TryIssueHandoffTicket(
                    lease,
                    created,
                    ticketTtl);

                if (wasRedirected)
                {
                    await _characterRepository.UpdateAsync(created, autoSave: true, cancellationToken: cancellationToken);
                }

                if (permitted)
                {
                    await _sessionLeaseRepository.UpsertAsync(lease, cancellationToken);
                }

                var handoff = new WorldHandoffTicketDto(
                    Permitted: permitted,
                    TicketGuid: ticketGuid,
                    LeaseEpoch: lease.LeaseEpoch,
                    AccountId: account.Id,
                    CharacterId: created.Id,
                    RoleNameUnicode: created.RoleName,
                    TargetMapId: resolvedMapId,
                    GameServerHost: cfg.AdvertisedGameServerHost,
                    GameServerPort: cfg.AdvertisedGameServerPort);

                return new CreateCharacterResultDto(
                    true,
                    null,
                    SessionLeaseAppService.MapToDto(created),
                    handoff);
            }
            catch (BusinessException ex)
            {
                return new CreateCharacterResultDto(false, ex.Code, null, null);
            }
        }
        finally
        {
            accountLock.Release();
        }
    }

    public async Task<DeleteCharacterResultDto> DeleteCharacterAsync(
        DeleteCharacterDto input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        SemaphoreSlim accountLock = _lockProvider.GetLock(input.AccountId);
        await accountLock.WaitAsync(cancellationToken);
        try
        {
            SessionLease? lease = await _sessionLeaseRepository.FindByAccountIdAsync(input.AccountId, cancellationToken);
            if (lease is null ||
                lease.LeaseEpoch != input.LeaseEpoch ||
                lease.State == SessionLeaseState.Offline)
            {
                return new DeleteCharacterResultDto(false, JXServerErrorCodes.SessionEpochSuperseded, input.RoleNameUnicode);
            }

            Account? account = await _accountRepository.FindAsync(input.AccountId, cancellationToken);
            if (account is null || !account.VerifyDeletionPassword(input.DeletionPasswordOrMd5Hex))
            {
                return new DeleteCharacterResultDto(false, JXServerErrorCodes.AccountInvalidCredentials, input.RoleNameUnicode);
            }

            string normalizedRoleName = Character.NormalizeRoleName(input.RoleNameUnicode);
            Character? character = await _characterRepository.FindByNormalizedRoleNameAsync(normalizedRoleName, cancellationToken);
            if (character is null || character.AccountId != input.AccountId)
            {
                return new DeleteCharacterResultDto(false, JXServerErrorCodes.CharacterNotFound, input.RoleNameUnicode);
            }

            string deletedName = character.RoleName;
            await _characterRepository.DeleteAsync(character, autoSave: true, cancellationToken: cancellationToken);
            return new DeleteCharacterResultDto(true, null, deletedName);
        }
        finally
        {
            accountLock.Release();
        }
    }
}
