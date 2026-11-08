using System.Collections.Concurrent;
using JXServer.Domain.SessionLeases;

namespace JXServer.Infrastructure.SessionLeases;

/// <summary>
/// Thread-safe in-memory repository for <see cref="SessionLease"/> coordination across Bishop and GameServer listeners.
/// </summary>
public sealed class InMemorySessionLeaseRepository : ISessionLeaseRepository
{
    private readonly ConcurrentDictionary<Guid, SessionLease> _leasesByAccountId = new();

    /// <inheritdoc />
    public Task<SessionLease?> FindByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        _leasesByAccountId.TryGetValue(accountId, out SessionLease? lease);
        return Task.FromResult(lease);
    }

    /// <inheritdoc />
    public Task<SessionLease?> FindByHandoffTicketAsync(Guid handoffTicket, CancellationToken cancellationToken = default)
    {
        if (handoffTicket == Guid.Empty)
        {
            return Task.FromResult<SessionLease?>(null);
        }

        foreach (SessionLease lease in _leasesByAccountId.Values)
        {
            if (lease.HandoffTicketGuid == handoffTicket)
            {
                return Task.FromResult<SessionLease?>(lease);
            }
        }

        return Task.FromResult<SessionLease?>(null);
    }

    /// <inheritdoc />
    public Task<SessionLease> UpsertAsync(SessionLease lease, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lease);
        _leasesByAccountId[lease.AccountId] = lease;
        return Task.FromResult(lease);
    }
}
