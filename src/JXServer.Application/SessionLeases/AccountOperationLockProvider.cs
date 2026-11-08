using System.Collections.Concurrent;

namespace JXServer.Application.SessionLeases;

/// <summary>
/// Provides per-account keyed <see cref="SemaphoreSlim"/> locks to serialize concurrent login, character mutation,
/// and lease state transitions for the same account (`FR-008`).
/// </summary>
public sealed class AccountOperationLockProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locksByUserName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the <see cref="SemaphoreSlim"/> associated with the specified normalized username or account ID key.
    /// </summary>
    public SemaphoreSlim GetLock(string accountKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);
        return _locksByUserName.GetOrAdd(accountKey, static _ => new SemaphoreSlim(1, 1));
    }

    /// <summary>
    /// Gets the <see cref="SemaphoreSlim"/> associated with the specified account ID.
    /// </summary>
    public SemaphoreSlim GetLock(Guid accountId) => GetLock(accountId.ToString("N"));
}
