using System.Collections.Generic;
using MemoryPack;

namespace JXServer.GameAssets.Shops;

/// <summary>
/// Immutable NPC shop definition (<c>settings/buysell.txt</c>, <c>Sale(shopId)</c> 1-based row) per <c>JXData Standard v1</c>.
/// </summary>
[MemoryPackable]
public sealed partial record ShopTemplate(
    int Id,
    string Code,
    string Name,
    string CurrencyType,
    int BuyPriceScalePercent,
    int SellBackDivisor,
    IReadOnlyList<ShopGoodTemplate> Goods);
