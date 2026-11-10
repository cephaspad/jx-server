using System.Collections.Generic;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;

namespace JXServer.GameAssets.Items;

/// <summary>
/// Immutable result produced by <see cref="ItemGenerator"/> (`FR-010`).
/// </summary>
public sealed record GeneratedItemData(
    ItemTemplate Template,
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
    IReadOnlyList<ItemMagicAttributeSlot> MagicAttributes);
