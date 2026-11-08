namespace JXServer.Contracts.Accounts;

/// <summary>
/// Input DTO for authenticating an account via Bishop Gateway (<c>0x41</c>).
/// </summary>
public sealed record AuthenticateAccountInput(
    string UserName,
    string PasswordHashUpperMd5,
    uint ProtocolVersion,
    bool KickOnline,
    string? BishopConnectionId = null);
