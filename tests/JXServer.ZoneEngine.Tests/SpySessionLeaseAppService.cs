using JXServer.Contracts.SessionLeases;

namespace JXServer.ZoneEngine.Tests;

internal sealed class SpySessionLeaseAppService : ISessionLeaseAppService
{
    public int HeartbeatCount { get; private set; }

    public int ReleaseCount { get; private set; }

    public Task<WorldHandoffTicketDto> SelectCharacterForWorldEntryAsync(
        SelectCharacterInput input,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<RedeemHandoffTicketResultDto> RedeemWorldHandoffTicketAsync(
        RedeemHandoffTicketInput input,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> RecordHeartbeatAsync(
        Guid accountId,
        long leaseEpoch,
        CancellationToken cancellationToken = default)
    {
        HeartbeatCount++;
        return Task.FromResult(true);
    }

    public Task CompleteLogoutOrEvictAsync(
        ReleaseSessionLeaseInput input,
        CancellationToken cancellationToken = default)
    {
        ReleaseCount++;
        return Task.CompletedTask;
    }
}
