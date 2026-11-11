using System.Collections.Generic;

namespace JXServer.GameAssets.Shops;

/// <summary>
/// Read-only catalog of multi-currency NPC shops (<c>data/shops/*.shops.json</c>).
/// </summary>
public interface IShopCatalog
{
    int Count { get; }

    IReadOnlyCollection<ShopTemplate> AllShops { get; }

    bool TryGetShop(int shopId, out ShopTemplate? shop);

    bool TryGetShopByCode(string code, out ShopTemplate? shop);
}
