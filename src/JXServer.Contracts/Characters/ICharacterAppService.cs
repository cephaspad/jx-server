namespace JXServer.Contracts.Characters;

/// <summary>
/// Control-plane application service port for character roster retrieval, creation (`0x45`), and deletion (`0x3A`).
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
}
