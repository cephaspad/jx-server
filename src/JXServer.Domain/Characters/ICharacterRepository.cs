namespace JXServer.Domain.Characters;

/// <summary>
/// Persistence port for the <see cref="Character"/> aggregate root following ABP repository naming conventions.
/// </summary>
public interface ICharacterRepository
{
    /// <summary>
    /// Finds a character by its unique identifier, or returns <see langword="null"/> if not found.
    /// </summary>
    Task<Character?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a character by its normalized uppercase role name, or returns <see langword="null"/> if not found.
    /// </summary>
    Task<Character?> FindByNormalizedRoleNameAsync(string normalizedRoleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of characters belonging to an account ordered by <see cref="Character.SlotIndex"/>.
    /// </summary>
    Task<List<Character>> GetListByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of characters belonging to an account.
    /// </summary>
    Task<int> GetCountByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a newly created character into the persistence store.
    /// </summary>
    Task<Character> InsertAsync(Character character, bool autoSave = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing character in the persistence store.
    /// </summary>
    Task<Character> UpdateAsync(Character character, bool autoSave = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a character from the persistence store, freeing its slot and role name (`FR-006`).
    /// </summary>
    Task DeleteAsync(Character character, bool autoSave = false, CancellationToken cancellationToken = default);
}
