using System.Collections.Generic;

namespace JXServer.GameAssets.LootTables;

/// <summary>
/// Read-only catalog of loot drop tables loaded from <c>JXData Standard v1</c>.
/// </summary>
public interface ILootTableCatalog
{
    int Count { get; }

    IReadOnlyCollection<LootTableTemplate> AllLootTables { get; }

    bool TryGetLootTable(string code, out LootTableTemplate? lootTable);
}
