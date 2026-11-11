using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JXServer.DataExporter.Legacy;

namespace JXServer.DataExporter.Exporters;

/// <summary>
/// Exports legacy multi-currency shop merchandise (<c>settings/goods.txt</c> and <c>settings/buysell.txt</c>)
/// into <c>data/shops/npc-shops.shops.json</c> per <c>JXData Standard v1</c>, preserving 1-based physical line indices
/// (including blank rows <c>173..174</c> in <c>buysell.txt</c>).
/// </summary>
public sealed class ShopDataExporter
{
    public (int ShopCount, int GoodsCount) ExportShops(
        string serverRootPath,
        string outputDataDirectory,
        ExportCatalogRegistry? registry = null)
    {
        registry ??= new ExportCatalogRegistry();

        string shopsOutDir = Path.Combine(outputDataDirectory, "shops");
        Directory.CreateDirectory(shopsOutDir);

        var goodsById = new Dictionary<int, (
            int Genre,
            int DetailType,
            int ParticularType,
            int Series,
            int Level,
            string CurrencyType,
            int Price,
            int DiscountPrice,
            int ExpireWallClockMinutes,
            int ExpireOnlineMinutes,
            int ConsumeType,
            string? ItemCode)>();

        string? goodsPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\goods.txt");
        int legacyGoodsRows = 0;
        if (goodsPath is not null && File.Exists(goodsPath))
        {
            var goodsTable = KTabFileTable.Load(goodsPath);
            legacyGoodsRows = goodsTable.DataRowCount;

            for (int r = 1; r <= goodsTable.DataRowCount; r++)
            {
                int genre = Math.Max(0, goodsTable.GetInt(r, 0, 0));
                int detail = Math.Max(0, goodsTable.GetInt(r, 1, 0));
                int part = Math.Max(0, goodsTable.GetInt(r, 2, 0));
                int series = Math.Clamp(goodsTable.GetInt(r, 3, -1), -1, 6);
                int level = Math.Max(0, goodsTable.GetInt(r, 4, 1));

                int gold = Math.Max(0, goodsTable.GetInt(r, 5, 0));
                int fuYuan = Math.Max(0, goodsTable.GetInt(r, 6, 0));
                int coin = Math.Max(0, goodsTable.GetInt(r, 7, 0));
                int battleScore = Math.Max(0, goodsTable.GetInt(r, 8, 0));
                int goldCoin = Math.Max(0, goodsTable.GetInt(r, 9, 0));
                int tongContribution = Math.Max(0, goodsTable.GetInt(r, 10, 0));
                int honor = Math.Max(0, goodsTable.GetInt(r, 11, 0));
                int goldCoinScore = Math.Max(0, goodsTable.GetInt(r, 12, 0));
                int unifiedCurrency = goodsTable.GetInt(r, 14, 0);
                int unifiedPrice = Math.Max(0, goodsTable.GetInt(r, 15, 0));
                int discountPrice = Math.Max(0, goodsTable.GetInt(r, 17, 0));
                int expireWall = Math.Max(0, goodsTable.GetInt(r, 22, 0));
                int expireOnline = Math.Max(0, goodsTable.GetInt(r, 23, 0));
                int consumeRaw = goodsTable.GetInt(r, 24, 0);
                int consumeType = consumeRaw == 1 ? 1 : 0;

                (string currencyType, int price) = ResolveCurrencyAndPrice(
                    gold,
                    fuYuan,
                    coin,
                    battleScore,
                    goldCoin,
                    tongContribution,
                    honor,
                    goldCoinScore,
                    unifiedCurrency,
                    unifiedPrice);

                string? itemCode = null;
                int lookupLevel = Math.Max(1, level);
                if (registry.ItemCodeByTupleAndLevel.TryGetValue((genre, detail, part, lookupLevel), out string? exactCode))
                {
                    itemCode = exactCode;
                }
                else if (registry.PrimaryItemCodeByTuple.TryGetValue((genre, detail, part), out string? primaryCode))
                {
                    itemCode = primaryCode;
                }

                goodsById[r] = (
                    genre,
                    detail,
                    part,
                    series,
                    level,
                    currencyType,
                    price,
                    discountPrice,
                    expireWall,
                    expireOnline,
                    consumeType,
                    itemCode);
            }
        }

        var shops = new List<Dictionary<string, object?>>();
        string? buySellPath = LegacyPathResolver.TryResolveStringPath(serverRootPath, @"settings\buysell.txt");
        int legacyBuySellRows = 0;

        if (buySellPath is not null && File.Exists(buySellPath))
        {
            var buySellTable = KTabFileTable.Load(buySellPath);
            legacyBuySellRows = buySellTable.DataRowCount;

            for (int r = 1; r <= buySellTable.DataRowCount; r++)
            {
                int shopId = r;
                string code = $"shop-{shopId}";
                string name = $"Cửa hàng #{shopId}";

                var referencedGoodsIds = new List<int>();
                int rowCols = buySellTable.GetColumnCount(r);
                for (int c = 0; c < rowCols; c++)
                {
                    int gid = buySellTable.GetInt(r, c, 0);
                    if (gid >= 1 && goodsById.ContainsKey(gid))
                    {
                        referencedGoodsIds.Add(gid);
                    }
                }

                string shopCurrency = "gold";
                if (referencedGoodsIds.Count > 0)
                {
                    shopCurrency = referencedGoodsIds
                        .Select(gid => goodsById[gid].CurrencyType)
                        .GroupBy(ct => ct, StringComparer.Ordinal)
                        .OrderByDescending(g => g.Count())
                        .First()
                        .Key;
                }

                var goodsEntries = new List<Dictionary<string, object?>>(referencedGoodsIds.Count);
                foreach (int gid in referencedGoodsIds)
                {
                    var g = goodsById[gid];
                    var entry = new Dictionary<string, object?>
                    {
                        ["goodsId"] = gid,
                    };

                    if (!string.IsNullOrEmpty(g.ItemCode))
                    {
                        entry["itemCode"] = g.ItemCode;
                    }

                    entry["genre"] = g.Genre;
                    entry["detailType"] = g.DetailType;
                    entry["particularType"] = g.ParticularType;
                    entry["level"] = g.Level;
                    entry["elementSeries"] = g.Series;
                    entry["price"] = g.Price;

                    if (g.DiscountPrice > 0)
                    {
                        entry["discountPrice"] = g.DiscountPrice;
                    }

                    if (!string.Equals(g.CurrencyType, shopCurrency, StringComparison.Ordinal))
                    {
                        entry["currencyTypeOverride"] = g.CurrencyType;
                    }

                    if (g.ConsumeType != 0)
                    {
                        entry["consumeType"] = g.ConsumeType;
                    }

                    if (g.ExpireWallClockMinutes > 0)
                    {
                        entry["expireWallClockMinutes"] = g.ExpireWallClockMinutes;
                    }

                    if (g.ExpireOnlineMinutes > 0)
                    {
                        entry["expireOnlineMinutes"] = g.ExpireOnlineMinutes;
                    }

                    goodsEntries.Add(entry);
                }

                shops.Add(new Dictionary<string, object?>
                {
                    ["id"] = shopId,
                    ["code"] = code,
                    ["name"] = name,
                    ["currencyType"] = shopCurrency,
                    ["buyPriceScalePercent"] = 100,
                    ["sellBackDivisor"] = 4,
                    ["goods"] = goodsEntries,
                });

                registry.ShopCodes.Add(code);
            }
        }

        registry.RecordCount("shops.goodsCatalog", legacyGoodsRows, goodsById.Count);
        registry.RecordCount("shops.buysell", legacyBuySellRows, shops.Count);

        var rootDoc = new Dictionary<string, object?>
        {
            ["$schema"] = "../../schemas/shop-catalog.schema.json",
            ["schemaVersion"] = "1.0.0",
            ["shops"] = shops,
        };

        LegacyPathResolver.WriteCanonicalJson(Path.Combine(shopsOutDir, "npc-shops.shops.json"), rootDoc);
        return (shops.Count, goodsById.Count);
    }

    private static (string CurrencyType, int Price) ResolveCurrencyAndPrice(
        int gold,
        int fuYuan,
        int coin,
        int battleScore,
        int goldCoin,
        int tongContribution,
        int honor,
        int goldCoinScore,
        int unifiedCurrency,
        int unifiedPrice)
    {
        if (goldCoin > 0)
        {
            return ("goldCoin", goldCoin);
        }

        if (coin > 0)
        {
            return ("coin", coin);
        }

        if (fuYuan > 0)
        {
            return ("fuYuan", fuYuan);
        }

        if (battleScore > 0)
        {
            return ("battleScore", battleScore);
        }

        if (tongContribution > 0)
        {
            return ("tongContribution", tongContribution);
        }

        if (honor > 0)
        {
            return ("honor", honor);
        }

        if (goldCoinScore > 0)
        {
            return ("goldCoinScore", goldCoinScore);
        }

        if (unifiedPrice > 0 && unifiedCurrency > 0)
        {
            string mapped = unifiedCurrency switch
            {
                1 => "fuYuan",
                2 => "coin",
                3 => "battleScore",
                4 => "goldCoin",
                5 => "tongContribution",
                6 => "honor",
                7 => "goldCoinScore",
                _ => "gold",
            };
            return (mapped, unifiedPrice);
        }

        return ("gold", gold);
    }
}
