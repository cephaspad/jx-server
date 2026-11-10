using System.Collections.Generic;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using MemoryPack;

namespace JXServer.GameAssets.Items;

/// <summary>
/// Immutable item template loaded from <c>JXData Standard v1</c> (<c>data/items/*.items.json</c> or <c>artifacts/items.pack</c>).
/// </summary>
[MemoryPackable]
public sealed partial record ItemTemplate(
    int Id,
    string Code,
    string Name,
    ItemGenreKind Genre,
    ItemQualityKind Quality,
    int DetailType,
    int ParticularType,
    byte Level,
    ElementSeries ElementSeries,
    byte GridWidthCells,
    byte GridHeightCells,
    int ObjDataId,
    int AppearanceResId,
    int PriceGold,
    int MaxStackCount,
    IReadOnlyList<ItemBaseAttributeTemplate> BaseAttributes,
    IReadOnlyList<ItemRequirementTemplate> Requirements);
