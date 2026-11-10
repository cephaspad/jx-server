using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Logging;
using JXServer.GameAssets.Snapshots;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.LootTables;

/// <summary>
/// Singleton read-only catalog of loot drop tables loaded from <c>JXData Standard v1</c> (`FR-001`, `FR-008`).
/// </summary>
public sealed class LootTableCatalog : ILootTableCatalog
{
    private readonly FrozenDictionary<string, LootTableTemplate> _tablesByCode;

    public int Count => _tablesByCode.Count;

    public IReadOnlyCollection<LootTableTemplate> AllLootTables => _tablesByCode.Values;

    public LootTableCatalog(IOptions<GameAssetsOptions> options, ILogger<LootTableCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        IReadOnlyList<LootTableTemplate> tables;
        string sourceTier;
        if (GameAssetsSnapshotLoader.TryLoadBundle(options.Value, out GameAssetsSnapshotBundle? bundle, out sourceTier) &&
            bundle is not null &&
            bundle.LootTables.Count > 0)
        {
            tables = bundle.LootTables;
        }
        else
        {
            tables = CreateDefaultLootTables();
            sourceTier = "BuiltInDefaults";
        }

        var byCode = new Dictionary<string, LootTableTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (LootTableTemplate lt in tables)
        {
            byCode[lt.Code] = lt;
        }

        foreach (LootTableTemplate fallback in CreateDefaultLootTables())
        {
            byCode.TryAdd(fallback.Code, fallback);
        }

        _tablesByCode = byCode.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        GameAssetsLogMessages.LogLootTablesLoaded(logger, _tablesByCode.Count, sourceTier);
    }

    public bool TryGetLootTable(string code, out LootTableTemplate? lootTable) =>
        _tablesByCode.TryGetValue(code, out lootTable);

    public static IReadOnlyList<LootTableTemplate> CreateDefaultLootTables() =>
    [
        new LootTableTemplate(
            Id: 1,
            Code: "default-monster-loot",
            RandRange: 1000000,
            MagicRate: 100,
            MoneyDropChance: 0.75,
            MinMoneyGold: 25,
            MaxMoneyGold: 120,
            MinItemLevel: 1,
            MaxItemLevel: 5,
            MaxDropsPerKill: 2,
            BlueAffixLuckFactor: 1.0,
            WeightedPools:
            [
                new WeightedLootPoolEntryTemplate(300000, "equip-melee-sword-lvl1", ItemGenreKind.Equipment, ItemQualityKind.Normal, 0, 0, ElementSeries.Metal, 1, 3),
                new WeightedLootPoolEntryTemplate(250000, "equip-armor-cloth-lvl1", ItemGenreKind.Equipment, ItemQualityKind.Normal, 2, 0, ElementSeries.Wood, 1, 3),
                new WeightedLootPoolEntryTemplate(250000, "kim-sang-duoc", ItemGenreKind.Medicine, ItemQualityKind.Normal, 0, 0, ElementSeries.Metal, 1, 1),
                new WeightedLootPoolEntryTemplate(200000, "ngung-than-dan", ItemGenreKind.Medicine, ItemQualityKind.Normal, 1, 0, ElementSeries.Water, 1, 1),
            ]),
    ];
}
