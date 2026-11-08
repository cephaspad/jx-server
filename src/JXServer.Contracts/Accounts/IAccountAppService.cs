namespace JXServer.Contracts.Accounts;

/// <summary>
/// Control-plane application service port for account authentication and auto-provisioning (`FR-003`).
/// </summary>
public interface IAccountAppService
{
    /// <summary>
    /// Authenticates an account login request (`0x41`) and coordinates session lease acquisition (`FR-003`, `FR-008`).
    /// </summary>
    Task<AuthenticateAccountResultDto> AuthenticateAsync(
        AuthenticateAccountInput input,
        CancellationToken cancellationToken = default);
}
