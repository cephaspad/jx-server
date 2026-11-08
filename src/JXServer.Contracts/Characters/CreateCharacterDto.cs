using JXServer.Contracts.SessionLeases;
using JXServer.Primitives.Characters;

namespace JXServer.Contracts.Characters;

/// <summary>
/// Input DTO for creating a new character (<c>0x45</c> <c>c2s_newplayer</c>).
/// </summary>
public sealed record CreateCharacterDto(
    Guid AccountId,
    long LeaseEpoch,
    string RoleNameUnicode,
    CharacterGender Gender,
    ElementSeries Series,
    ushort StarterVillageMapId);
