using System.Collections.Generic;
using MemoryPack;

namespace JXServer.GameAssets.LootTables;

/// <summary>
/// Immutable loot drop table loaded from <c>JXData Standard v1</c> (<c>data/loot/*.loot.json</c> or <c>artifacts/loot.pack</c>).
/// </summary>
[MemoryPackable]
public sealed partial record LootTableTemplate(
    int Id,
    string Code,
    int RandRange,
    int MagicRate,
    double MoneyDropChance,
    int MinMoneyGold,
    int MaxMoneyGold,
    byte MinItemLevel,
    byte MaxItemLevel,
    int MaxDropsPerKill,
    double BlueAffixLuckFactor,
    IReadOnlyList<WeightedLootPoolEntryTemplate> WeightedPools);
