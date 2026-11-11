using System.Collections.Concurrent;
using JXServer.Contracts.SessionLeases;

namespace JXServer.ZoneEngine.Tests.SimulationHarnesses;

/// <summary>
/// In-memory, zero-database stub of <see cref="ISessionLeaseAppService"/> for deterministic simulation harnesses.
/// Records all heartbeats, ticket redemptions, and session releases for offline assertions.
/// </summary>
public sealed class InMemorySessionLeaseAppServiceStub : ISessionLeaseAppService
{
    private readonly ConcurrentBag<(Guid AccountId, long LeaseEpoch)> _heartbeats = [];
    private readonly ConcurrentBag<ReleaseSessionLeaseInput> _releases = [];

    public IReadOnlyCollection<(Guid AccountId, long LeaseEpoch)> Heartbeats => _heartbeats;

    public IReadOnlyCollection<ReleaseSessionLeaseInput> Releases => _releases;

    public bool HeartbeatReturnValue { get; set; } = true;

    public Task<WorldHandoffTicketDto> SelectCharacterForWorldEntryAsync(
        SelectCharacterInput input,
        CancellationToken cancellationToken = default)
    {
        var ticket = new WorldHandoffTicketDto(
            Permitted: true,
            TicketGuid: Guid.NewGuid(),
            LeaseEpoch: input.LeaseEpoch,
            AccountId: input.AccountId,
            CharacterId: Guid.NewGuid(),
            RoleNameUnicode: input.RoleNameUnicode,
            TargetMapId: 53,
            GameServerHost: "127.0.0.1",
            GameServerPort: 6666);

        return Task.FromResult(ticket);
    }

    public Task<RedeemHandoffTicketResultDto> RedeemWorldHandoffTicketAsync(
        RedeemHandoffTicketInput input,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new RedeemHandoffTicketResultDto(
            Succeeded: true,
            LeaseEpoch: 1,
            Character: null));
    }

    public Task<bool> RecordHeartbeatAsync(
        Guid accountId,
        long leaseEpoch,
        CancellationToken cancellationToken = default)
    {
        _heartbeats.Add((accountId, leaseEpoch));
        return Task.FromResult(HeartbeatReturnValue);
    }

    public Task CompleteLogoutOrEvictAsync(
        ReleaseSessionLeaseInput input,
        CancellationToken cancellationToken = default)
    {
        _releases.Add(input);
        return Task.CompletedTask;
    }
}
