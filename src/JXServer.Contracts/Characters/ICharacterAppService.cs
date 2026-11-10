namespace JXServer.Contracts.Characters;

/// <summary>
/// Control-plane application service port for character roster retrieval, creation (`0x45`), deletion (`0x3A`),
/// and durable coordinate/vital persistence (`FR-012`).
/// </summary>
public interface ICharacterAppService
{
    /// <summary>
    /// Retrieves up to 3 active characters for the specified account.
    /// </summary>
    Task<IReadOnlyList<CharacterSummaryDto>> GetCharacterRosterAsync(
        GetCharacterRosterInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new character initialized from starter templates and issues a world handoff ticket (`FR-005`).
    /// </summary>
    Task<CreateCharacterResultDto> CreateCharacterAsync(
        CreateCharacterDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes an existing character and immediately frees its slot and role name (`FR-006`).
    /// </summary>
    Task<DeleteCharacterResultDto> DeleteCharacterAsync(
        DeleteCharacterDto input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists an active character's updated map ID, pixel coordinates, and vitals (`FR-012`).
    /// </summary>
    Task<bool> UpdateCharacterWorldSnapshotAsync(
        UpdateCharacterWorldSnapshotDto input,
        CancellationToken cancellationToken = default);
}
