namespace JXServer.Contracts.SessionLeases;

/// <summary>
/// Control-plane application service port for session lease fencing, world handoff tickets (`0x34` / `0x42`), and logout/eviction (`FR-007`, `FR-008`).
/// </summary>
public interface ISessionLeaseAppService
{
    /// <summary>
    /// Validates character selection (`0x64`), resolves the target map (or fallback village per `FR-007`),
    /// and issues a single-use 16-byte <see cref="WorldHandoffTicketDto"/> (`0x34`).
    /// </summary>
    Task<WorldHandoffTicketDto> SelectCharacterForWorldEntryAsync(
        SelectCharacterInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically verifies and consumes a single-use 16-byte handoff ticket (`0x42` <c>c2s_logiclogin</c>).
    /// </summary>
    Task<RedeemHandoffTicketResultDto> RedeemWorldHandoffTicketAsync(
        RedeemHandoffTicketInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the heartbeat timestamp for an active session lease if the epoch matches.
    /// </summary>
    Task<bool> RecordHeartbeatAsync(
        Guid accountId,
        long leaseEpoch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the character's world snapshot (if provided) and releases the session lease if the epoch matches.
    /// </summary>
    Task CompleteLogoutOrEvictAsync(
        ReleaseSessionLeaseInput input,
        CancellationToken cancellationToken = default);
}
