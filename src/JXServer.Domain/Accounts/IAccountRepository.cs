namespace JXServer.Domain.Accounts;

/// <summary>
/// Persistence port for the <see cref="Account"/> aggregate root following ABP repository naming conventions.
/// </summary>
public interface IAccountRepository
{
    /// <summary>
    /// Finds an account by its unique identifier, or returns <see langword="null"/> if not found.
    /// </summary>
    Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an account by its normalized uppercase username, or returns <see langword="null"/> if not found.
    /// </summary>
    Task<Account?> FindByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a newly created account into the persistence store.
    /// </summary>
    Task<Account> InsertAsync(Account account, bool autoSave = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing account in the persistence store.
    /// </summary>
    Task<Account> UpdateAsync(Account account, bool autoSave = false, CancellationToken cancellationToken = default);
}
