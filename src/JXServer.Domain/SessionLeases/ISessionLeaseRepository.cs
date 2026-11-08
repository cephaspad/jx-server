namespace JXServer.Domain.SessionLeases;

/// <summary>
/// Persistence and atomic coordination port for <see cref="SessionLease"/> records.
/// </summary>
public interface ISessionLeaseRepository
{
    /// <summary>
    /// Finds the lease associated with an account, or returns <see langword="null"/> if none exists.
    /// </summary>
    Task<SessionLease?> FindByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the lease currently holding the specified single-use handoff ticket, or returns <see langword="null"/>.
    /// </summary>
    Task<SessionLease?> FindByHandoffTicketAsync(Guid handoffTicket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a session lease atomically.
    /// </summary>
    Task<SessionLease> UpsertAsync(SessionLease lease, CancellationToken cancellationToken = default);
}
