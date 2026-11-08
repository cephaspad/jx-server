using JXServer.Primitives.Accounts;
using JXServer.Primitives.Encodings;

namespace JXServer.Domain.Accounts;

/// <summary>
/// Domain service managing account credential verification and optional first-login auto-provisioning (`FR-003`).
/// Does not perform persistence directly; the calling <c>AccountAppService</c> persists newly provisioned or updated aggregates (`Constitution VI`).
/// </summary>
public sealed class AccountManager
{
    private readonly IAccountRepository _accountRepository;
    private readonly TimeProvider _timeProvider;

    public AccountManager(IAccountRepository accountRepository, TimeProvider timeProvider)
    {
        _accountRepository = accountRepository;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Verifies credentials (and optionally constructs a missing account per `FR-003`), returning the status code, account, and whether it is newly provisioned.
    /// </summary>
    public async Task<(LoginResultCode ResultCode, Account? Account, bool IsNewlyProvisioned)> VerifyOrProvisionAsync(
        string userName,
        string passwordOrMd5Hex,
        uint protocolVersion,
        bool autoProvisionAccounts,
        CancellationToken cancellationToken = default)
    {
        if (protocolVersion != AccountConsts.RequiredProtocolVersion)
        {
            return (LoginResultCode.InvalidProtocolVersion, null, false);
        }

        string trimmedUserName = (userName ?? string.Empty).TrimEnd('\0').Trim();
        if (trimmedUserName.Length < AccountConsts.MinUserNameByteLength ||
            trimmedUserName.Length > AccountConsts.MaxUserNameByteLength ||
            string.IsNullOrWhiteSpace(passwordOrMd5Hex))
        {
            return (LoginResultCode.AccountOrPasswordError, null, false);
        }

        string normalizedUserName = Account.NormalizeUserName(trimmedUserName);
        Account? account = await _accountRepository.FindByNormalizedUserNameAsync(normalizedUserName, cancellationToken);

        if (account is null)
        {
            if (!autoProvisionAccounts)
            {
                return (LoginResultCode.AccountOrPasswordError, null, false);
            }

            string normalizedMd5 = KsgPasswordHasher.NormalizeClientPasswordOrHash(passwordOrMd5Hex);
            if (!KsgPasswordHasher.IsHexDigest32(normalizedMd5))
            {
                return (LoginResultCode.AccountOrPasswordError, null, false);
            }

            account = new Account(
                Guid.NewGuid(),
                trimmedUserName,
                normalizedMd5,
                secondaryPasswordOrMd5Hex: null,
                status: AccountStatus.Active,
                remainingTimeSeconds: 86_400u,
                createdAtUtc: _timeProvider.GetUtcNow());

            return (LoginResultCode.Success, account, true);
        }

        if (!account.VerifyPassword(passwordOrMd5Hex))
        {
            return (LoginResultCode.AccountOrPasswordError, null, false);
        }

        if (account.Status != AccountStatus.Active)
        {
            return (LoginResultCode.AccountFrozen, null, false);
        }

        return (LoginResultCode.Success, account, false);
    }
}
