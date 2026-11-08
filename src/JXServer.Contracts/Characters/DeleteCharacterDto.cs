namespace JXServer.Contracts.Characters;

/// <summary>
/// Input DTO for deleting an existing character (<c>0x3A</c> <c>c2s_roleserver_deleteplayer</c>).
/// </summary>
public sealed record DeleteCharacterDto(
    Guid AccountId,
    long LeaseEpoch,
    string RoleNameUnicode,
    string DeletionPasswordOrMd5Hex);
