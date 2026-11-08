using JXServer.Primitives.Accounts;

namespace JXServer.Contracts.Accounts;

/// <summary>
/// Result DTO returned from <see cref="IAccountAppService.AuthenticateAsync"/>.
/// </summary>
public sealed record AuthenticateAccountResultDto(
    LoginResultCode ResultCode,
    Guid? AccountId,
    long LeaseEpoch,
    uint RemainingTimeSeconds);
