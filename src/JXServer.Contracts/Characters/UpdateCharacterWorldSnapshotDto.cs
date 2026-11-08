namespace JXServer.Contracts.Characters;

/// <summary>
/// DTO carrying updated character coordinates and vitals when detaching from a map zone.
/// </summary>
public sealed record UpdateCharacterWorldSnapshotDto(
    Guid CharacterId,
    ushort MapId,
    int MapMpsX,
    int MapMpsY,
    int CurrentLife,
    int CurrentMana,
    ushort CurrentStamina);
