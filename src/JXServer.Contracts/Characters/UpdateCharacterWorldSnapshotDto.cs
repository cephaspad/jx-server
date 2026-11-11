using JXServer.Primitives.Combat;

namespace JXServer.Contracts.Characters;

/// <summary>
/// DTO carrying updated character coordinates, vitals, progression, and inventory when detaching from a map zone.
/// </summary>
public sealed record UpdateCharacterWorldSnapshotDto(
    Guid CharacterId,
    ushort MapId,
    int MapMpsX,
    int MapMpsY,
    int CurrentLife,
    int CurrentMana,
    ushort CurrentStamina,
    long LeaseEpoch = 0,
    byte FightMode = 0,
    byte? Level = null,
    long? Experience = null,
    int? LeadExperience = null,
    ushort? Strength = null,
    ushort? Dexterity = null,
    ushort? Vitality = null,
    ushort? Energy = null,
    ushort? UnallocatedAttributePoints = null,
    ushort? UnallocatedSkillPoints = null,
    ushort? MaxLife = null,
    ushort? MaxMana = null,
    ushort? MaxStamina = null,
    int? InventoryMoney = null,
    int? RepositoryMoney = null,
    PlayerPkMode? PkState = null,
    sbyte? PkValue = null,
    ushort? ActiveAuraSkillId = null,
    IReadOnlyList<CharacterSkillDto>? Skills = null,
    IReadOnlyList<CharacterItemDto>? Items = null,
    IReadOnlyDictionary<ushort, int>? TaskValues = null,
    string? RepositoryPasswordHash = null,
    bool UpdateRepositoryPasswordHash = false);
