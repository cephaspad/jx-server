using JXServer.Primitives.Characters;

namespace JXServer.Contracts.Characters;

/// <summary>
/// Complete DTO snapshot of a character used during world entry and zone attachment.
/// </summary>
public sealed record CharacterDto(
    Guid Id,
    Guid AccountId,
    byte SlotIndex,
    string RoleNameUnicode,
    CharacterGender Gender,
    ElementSeries Series,
    byte Level,
    long Experience,
    int LeadExperience,
    ushort MapId,
    int RevivalMapId,
    int RevivalPointId,
    int MapMpsX,
    int MapMpsY,
    int CurrentLife,
    ushort MaxLife,
    int CurrentMana,
    ushort MaxMana,
    ushort CurrentStamina,
    ushort MaxStamina,
    ushort Strength,
    ushort Dexterity,
    ushort Vitality,
    ushort Energy,
    ushort Lucky,
    ushort UnallocatedAttributePoints,
    ushort UnallocatedSkillPoints,
    int InventoryMoney,
    int RepositoryMoney,
    byte CurrentFactionId,
    byte FirstFactionId,
    ushort FactionJoinCount,
    IReadOnlyList<CharacterSkillDto> Skills);
