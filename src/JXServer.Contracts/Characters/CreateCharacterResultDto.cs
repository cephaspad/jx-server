using JXServer.Contracts.SessionLeases;

namespace JXServer.Contracts.Characters;

/// <summary>
/// Result DTO returned from <see cref="ICharacterAppService.CreateCharacterAsync"/>.
/// </summary>
public sealed record CreateCharacterResultDto(
    bool Succeeded,
    string? ErrorCode,
    CharacterDto? Character,
    WorldHandoffTicketDto? HandoffTicket);
