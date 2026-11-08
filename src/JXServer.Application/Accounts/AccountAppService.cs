using JXServer.Application.Configuration;
using JXServer.Application.Logging;
using JXServer.Application.SessionLeases;
using JXServer.Contracts.Accounts;
using JXServer.Domain.Accounts;
using JXServer.Domain.SessionLeases;
using JXServer.Primitives.Accounts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.Application.Accounts;

/// <summary>
/// Application service orchestrating account authentication (`0x41`), optional first-login auto-provisioning (`FR-003`),
/// and serialized per-account session lease acquisition (`FR-008`).
/// </summary>
public sealed class AccountAppService : IAccountAppService
{
    private readonly AccountManager _accountManager;
    private readonly SessionLeaseManager _sessionLeaseManager;
    private readonly AccountOperationLockProvider _lockProvider;
    private readonly IOptions<ApplicationOptions> _options;
    private readonly ILogger<AccountAppService> _logger;

    public AccountAppService(
        AccountManager accountManager,
        SessionLeaseManager sessionLeaseManager,
        AccountOperationLockProvider lockProvider,
        IOptions<ApplicationOptions> options,
        ILogger<AccountAppService> logger)
    {
        _accountManager = accountManager;
        _sessionLeaseManager = sessionLeaseManager;
        _lockProvider = lockProvider;
        _options = options;
        _logger = logger;
    }

    public async Task<AuthenticateAccountResultDto> AuthenticateAsync(
        AuthenticateAccountInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        string normalizedKey = Account.NormalizeUserName(input.UserName);
        if (string.IsNullOrEmpty(normalizedKey))
        {
            return new AuthenticateAccountResultDto(LoginResultCode.AccountOrPasswordError, null, 0, 0);
        }

        SemaphoreSlim userLock = _lockProvider.GetLock($"USER:{normalizedKey}");
        await userLock.WaitAsync(cancellationToken);
        try
        {
            ApplicationOptions cfg = _options.Value;
            (LoginResultCode verifyResult, Account? account) = await _accountManager.VerifyOrProvisionAsync(
                input.UserName,
                input.PasswordHashUpperMd5,
                input.ProtocolVersion,
                cfg.AutoProvisionAccounts,
                cancellationToken);

            if (verifyResult != LoginResultCode.Success || account is null)
            {
                ApplicationLogMessages.LogAccountAuthenticated(_logger, input.UserName, verifyResult, null, 0);
                return new AuthenticateAccountResultDto(verifyResult, null, 0, 0);
            }

            SemaphoreSlim accountLock = _lockProvider.GetLock(account.Id);
            await accountLock.WaitAsync(cancellationToken);
            try
            {
                TimeSpan heartbeatTimeout = TimeSpan.FromSeconds(Math.Max(5, cfg.HeartbeatTimeoutSeconds));
                (LoginResultCode leaseResult, SessionLease? lease) = await _sessionLeaseManager.TryAcquireGatewayLeaseAsync(
                    account,
                    input.KickOnline,
                    input.BishopConnectionId,
                    heartbeatTimeout,
                    cancellationToken);

                if (leaseResult != LoginResultCode.Success || lease is null)
                {
                    ApplicationLogMessages.LogAccountAuthenticated(_logger, account.UserName, leaseResult, account.Id, 0);
                    return new AuthenticateAccountResultDto(leaseResult, account.Id, 0, 0);
                }

                ApplicationLogMessages.LogAccountAuthenticated(
                    _logger,
                    account.UserName,
                    LoginResultCode.Success,
                    account.Id,
                    lease.LeaseEpoch);

                return new AuthenticateAccountResultDto(
                    LoginResultCode.Success,
                    account.Id,
                    lease.LeaseEpoch,
                    account.RemainingTimeSeconds);
            }
            finally
            {
                accountLock.Release();
            }
        }
        finally
        {
            userLock.Release();
        }
    }
}
