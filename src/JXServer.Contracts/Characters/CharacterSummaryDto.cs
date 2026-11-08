using JXServer.Primitives.Characters;

namespace JXServer.Contracts.Characters;

/// <summary>
/// Summary DTO for a character slot in <c>s2c_roleserver_getrolelist_result</c> (<c>0x37</c>).
/// </summary>
public sealed record CharacterSummaryDto(
    byte SlotIndex,
    Guid CharacterId,
    string RoleNameUnicode,
    CharacterGender Gender,
    ElementSeries Series,
    byte Level);
