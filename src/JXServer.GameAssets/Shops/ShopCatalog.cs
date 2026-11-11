using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Logging;
using JXServer.GameAssets.Snapshots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.Shops;

/// <summary>
/// Singleton read-only catalog of multi-currency NPC shops loaded from <c>JXData Standard v1</c> snapshots or JSON.
/// </summary>
public sealed class ShopCatalog : IShopCatalog
{
    private readonly FrozenDictionary<int, ShopTemplate> _shopsById;
    private readonly FrozenDictionary<string, ShopTemplate> _shopsByCode;

    public int Count => _shopsById.Count;

    public IReadOnlyCollection<ShopTemplate> AllShops => _shopsById.Values;

    public ShopCatalog(IOptions<GameAssetsOptions> options, ILogger<ShopCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        IReadOnlyList<ShopTemplate> shops = [];
        string sourceTier = "EmptyDefaults";
        if (GameAssetsSnapshotLoader.TryLoadBundle(options.Value, out GameAssetsSnapshotBundle? bundle, out string loadedTier) &&
            bundle?.Shops is { Count: > 0 })
        {
            shops = bundle.Shops;
            sourceTier = loadedTier;
        }

        var byId = new Dictionary<int, ShopTemplate>();
        var byCode = new Dictionary<string, ShopTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (ShopTemplate shop in shops)
        {
            byId[shop.Id] = shop;
            byCode[shop.Code] = shop;
        }

        _shopsById = byId.ToFrozenDictionary();
        _shopsByCode = byCode.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        GameAssetsLogMessages.LogShopCatalogLoaded(logger, _shopsById.Count, sourceTier);
    }

    public bool TryGetShop(int shopId, out ShopTemplate? shop) =>
        _shopsById.TryGetValue(shopId, out shop);

    public bool TryGetShopByCode(string code, out ShopTemplate? shop) =>
        _shopsByCode.TryGetValue(code, out shop);
}
