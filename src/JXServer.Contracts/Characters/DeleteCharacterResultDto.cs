namespace JXServer.Contracts.Characters;

/// <summary>
/// Result DTO returned from <see cref="ICharacterAppService.DeleteCharacterAsync"/>.
/// </summary>
public sealed record DeleteCharacterResultDto(
    bool Succeeded,
    string? ErrorCode,
    string RoleNameUnicode);
