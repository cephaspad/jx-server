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

    public async Task<bool> UpdateCharacterWorldSnapshotAsync(
        UpdateCharacterWorldSnapshotDto input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        Character? character = await _characterRepository.FindAsync(input.CharacterId, cancellationToken);
        if (character is null)
        {
            return false;
        }

        SemaphoreSlim accountLock = _lockProvider.GetLock(character.AccountId);
        await accountLock.WaitAsync(cancellationToken);
        try
        {
            if (input.LeaseEpoch > 0)
            {
                SessionLease? lease = await _sessionLeaseRepository.FindByAccountIdAsync(character.AccountId, cancellationToken);
                if (lease is null || lease.LeaseEpoch != input.LeaseEpoch)
                {
                    return false;
                }
            }

            ApplyWorldSnapshot(character, input, DateTimeOffset.UtcNow);
            await _characterRepository.UpdateAsync(character, autoSave: true, cancellationToken: cancellationToken);
            return true;
        }
        finally
        {
            accountLock.Release();
        }
    }

    internal static void ApplyWorldSnapshot(
        Character character,
        UpdateCharacterWorldSnapshotDto snap,
        DateTimeOffset nowUtc)
    {
        if (snap.Level is null &&
            snap.Experience is null &&
            snap.Skills is null &&
            snap.Items is null &&
            snap.InventoryMoney is null &&
            snap.PkState is null &&
            snap.TaskValues is null &&
            !snap.UpdateRepositoryPasswordHash)
        {
            character.UpdateWorldSnapshot(
                snap.MapId,
                snap.MapMpsX,
                snap.MapMpsY,
                snap.CurrentLife,
                snap.CurrentMana,
                snap.CurrentStamina,
                nowUtc);
            return;
        }

        var mappedSkills = snap.Skills?.Select(s => new CharacterSkillValue(
            s.SkillId,
            s.BaseLevel,
            s.BonusLevel,
            s.ExpPercent)).ToList();

        var mappedItems = snap.Items?.Select(i => new CharacterItem(
            i.Id == Guid.Empty ? Guid.NewGuid() : i.Id,
            character.Id,
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
            i.BindState,
            i.ExpireTimeUnixSeconds,
            i.CreatorName,
            i.MagicAttributes.Select(m => new ItemMagicAttributeValue(
                m.SlotIndex,
                m.AttributeId,
                m.Value0,
                m.Value1,
                m.Value2,
                m.EnchaseLevel)),
            i.IsBroken,
            i.EnchaseLevel)).ToList();

        character.UpdateWorldSnapshot(
            snap.MapId,
            snap.MapMpsX,
            snap.MapMpsY,
            snap.CurrentLife,
            snap.CurrentMana,
            snap.CurrentStamina,
            snap.Level ?? character.Level,
            snap.Experience ?? character.Experience,
            snap.LeadExperience ?? character.LeadExperience,
            snap.Strength ?? character.Strength,
            snap.Dexterity ?? character.Dexterity,
            snap.Vitality ?? character.Vitality,
            snap.Energy ?? character.Energy,
            snap.UnallocatedAttributePoints ?? character.UnallocatedAttributePoints,
            snap.UnallocatedSkillPoints ?? character.UnallocatedSkillPoints,
            snap.MaxLife ?? character.MaxLife,
            snap.MaxMana ?? character.MaxMana,
            snap.MaxStamina ?? character.MaxStamina,
            snap.InventoryMoney ?? character.InventoryMoney,
            snap.RepositoryMoney ?? character.RepositoryMoney,
            snap.PkState ?? character.PkState,
            snap.PkValue ?? character.PkValue,
            snap.ActiveAuraSkillId ?? character.ActiveAuraSkillId,
            mappedSkills,
            mappedItems,
            nowUtc,
            snap.TaskValues,
            snap.RepositoryPasswordHash,
            snap.UpdateRepositoryPasswordHash);
    }
}
