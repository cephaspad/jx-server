namespace JXServer.Contracts.SessionLeases;

/// <summary>
/// Input DTO for selecting an existing character to enter the world (<c>0x64</c> <c>c2s_dbplayerselect</c>).
/// </summary>
public sealed record SelectCharacterInput(
    Guid AccountId,
    long LeaseEpoch,
    string RoleNameUnicode);
