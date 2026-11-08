namespace JXServer.Contracts.Characters;

/// <summary>
/// Input DTO for retrieving an account's character roster (<c>0x37</c>).
/// </summary>
public sealed record GetCharacterRosterInput(Guid AccountId);
