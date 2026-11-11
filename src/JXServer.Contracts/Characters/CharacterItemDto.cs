using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;

namespace JXServer.Contracts.Characters;

/// <summary>
/// Data transfer object representing a persisted character item.
/// </summary>
public sealed record class CharacterItemDto(
    Guid Id,
    ItemGenreKind Genre,
    short DetailType,
    short ParticularType,
    byte Level,
    ElementSeries Series,
    ItemQualityKind Quality,
    uint RandomSeed,
    ushort GeneratorVersion,
    int Luck,
    ushort CurrentDurability,
    ushort MaxDurability,
    ushort StackCount,
    ItemContainerPlace ContainerPlace,
    byte GridX,
    byte GridY,
    IReadOnlyList<CharacterItemMagicAttributeDto> MagicAttributes,
    ItemBindState BindState = ItemBindState.Unbound,
    uint ExpireTimeUnixSeconds = 0,
    string? CreatorName = null,
    bool IsBroken = false,
    byte EnchaseLevel = 0);
