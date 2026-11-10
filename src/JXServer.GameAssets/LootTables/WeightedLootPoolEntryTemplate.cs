using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using MemoryPack;

namespace JXServer.GameAssets.LootTables;

/// <summary>
/// Represents a single weighted entry in a monster loot drop table.
/// </summary>
[MemoryPackable]
public sealed partial record WeightedLootPoolEntryTemplate(
    int Weight,
    string? ItemCode,
    ItemGenreKind? Genre,
    ItemQualityKind Quality,
    int? DetailType,
    int? ParticularType,
    ElementSeries ElementSeries,
    byte MinLevel,
    byte MaxLevel,
    int? GoldEquipId = null);
