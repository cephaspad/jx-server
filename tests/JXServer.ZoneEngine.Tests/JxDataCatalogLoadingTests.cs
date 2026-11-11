using System;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Items;
using JXServer.GameAssets.LootTables;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.Shops;
using JXServer.GameAssets.Skills;
using JXServer.GameAssets.Snapshots;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using MemoryPack;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace JXServer.ZoneEngine.Tests;

public sealed class JxDataCatalogLoadingTests
{
    [Fact]
    public void SnapshotLoader_LoadsTier2MemoryPack_AndMatchesCanonicalJson()
    {
        var opts = new GameAssetsOptions
        {
            ServerRootPath = @"D:\Projects\jx-linux-bin\server1",
            DataDirectory = "data",
            ArtifactsDirectory = "artifacts",
            PreferBinarySnapshots = true,
        };

        bool loaded = GameAssetsSnapshotLoader.TryLoadBundle(opts, out GameAssetsSnapshotBundle? bundle, out string sourceTier);
        Assert.True(loaded);
        Assert.NotNull(bundle);
        Assert.StartsWith("Tier2:MemoryPack", sourceTier);
        Assert.NotEmpty(bundle.SourceJsonSha256Hex);
        Assert.True(bundle.Skills.Count > 1000);
        Assert.True(bundle.Missiles.Count > 500);
        Assert.True(bundle.Items.Count > 20000);
        Assert.True(bundle.MagicAffixes.Count > 7000);
        Assert.True(bundle.LootTables.Count >= 150);
        Assert.Equal(2540, bundle.NpcTemplates.Count);
        Assert.Equal(10, bundle.CharacterTemplates.Count);
        Assert.NotEmpty(bundle.DefaultStarterItems);
        Assert.NotNull(bundle.Shops);
        Assert.Equal(179, bundle.Shops.Count);

        byte[] roundTripBytes = MemoryPackSerializer.Serialize(bundle);
        GameAssetsSnapshotBundle? roundTrip = MemoryPackSerializer.Deserialize<GameAssetsSnapshotBundle>(roundTripBytes);
        Assert.NotNull(roundTrip);
        Assert.Equal(bundle.SourceJsonSha256Hex, roundTrip.SourceJsonSha256Hex);
        Assert.Equal(bundle.Skills.Count, roundTrip.Skills.Count);
        Assert.Equal(bundle.Items.Count, roundTrip.Items.Count);
        Assert.Equal(179, roundTrip.Shops?.Count);
    }

    [Fact]
    public void SkillParameterCurveTemplate_EvaluatesLinearPiecewiseConicAndOverCapTableDeterministically()
    {
        var linear = new SkillParameterCurveTemplate("linear", BaseValue: 10, PerLevelValue: 5);
        Assert.Equal(10, linear.Evaluate(1));
        Assert.Equal(30, linear.Evaluate(5));

        var piecewise = new SkillParameterCurveTemplate(
            "piecewise",
            BaseValue: 0,
            Points:
            [
                new SkillCurveControlPoint(1, 10),
                new SkillCurveControlPoint(10, 100),
                new SkillCurveControlPoint(20, 300),
            ]);
        Assert.Equal(10, piecewise.Evaluate(1));
        Assert.Equal(50, piecewise.Evaluate(5));
        Assert.Equal(100, piecewise.Evaluate(10));
        Assert.Equal(200, piecewise.Evaluate(15));
        Assert.Equal(300, piecewise.Evaluate(20));

        var tableCurve = new SkillParameterCurveTemplate(
            "table",
            BaseValue: 0,
            ValuesByLevel: [10, 20, 35, 55, 80]);
        Assert.Equal(10, tableCurve.Evaluate(1));
        Assert.Equal(80, tableCurve.Evaluate(5));
        Assert.Equal(80, tableCurve.Evaluate(30));

        int[] cells = new int[ObstacleRleCodec.RegionCellCount];
        cells[0] = 1;
        cells[1] = 1;
        cells[255] = 3;
        cells[511] = 2;
        string? encoded = ObstacleRleCodec.EncodeCellsToBase64(cells);
        Assert.NotNull(encoded);
        int[] decoded = ObstacleRleCodec.DecodeBase64ToCells(encoded);
        Assert.Equal(cells, decoded);
    }

    [Fact]
    public void Catalogs_LoadExpectedVietnameseNames_Shops_GoldenPlatina_AndStarterItems()
    {
        IOptions<GameAssetsOptions> options = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = @"D:\Projects\jx-linux-bin\server1",
            DataDirectory = "data",
            ArtifactsDirectory = "artifacts",
            PreferBinarySnapshots = true,
        });

        var skillCatalog = new SkillTemplateCatalog(options, NullLogger<SkillTemplateCatalog>.Instance);
        var missileCatalog = new MissileTemplateCatalog(options);
        var itemCatalog = new ItemTemplateCatalog(options, NullLogger<ItemTemplateCatalog>.Instance);
        var lootCatalog = new LootTableCatalog(options, NullLogger<LootTableCatalog>.Instance);
        var playerCatalog = new PlayerTemplateCatalog(options, NullLogger<PlayerTemplateCatalog>.Instance);
        var shopCatalog = new ShopCatalog(options, NullLogger<ShopCatalog>.Instance);

        Assert.True(skillCatalog.TryGetSkill(53, out SkillTemplate? unarmed) && unarmed is not null);
        Assert.Contains("kích vật lý", unarmed.Name, StringComparison.OrdinalIgnoreCase);

        Assert.True(skillCatalog.TryGetSkill(102, out SkillTemplate? windSkill) && windSkill is not null);
        Assert.Equal("Phong Quyển Tàn Tuyết", windSkill.Name);
        Assert.True(missileCatalog.TryGetMissile(windSkill.MissileId, out MissileTemplate? windMissile) && windMissile is not null);
        Assert.Equal("Phong Quyển Tàn Tuyết", windMissile.Name);

        Assert.True(itemCatalog.TryGetItemByCode("kim-sang-duoc", out ItemTemplate? healPotion) && healPotion is not null);
        Assert.Equal(ItemGenreKind.Medicine, healPotion.Genre);
        Assert.Contains("Kim Sáng", healPotion.Name, StringComparison.OrdinalIgnoreCase);

        Assert.True(itemCatalog.TryGetItemByRowId(ItemQualityKind.Golden, 1, out ItemTemplate? goldEquip) && goldEquip is not null);
        Assert.Equal(ItemQualityKind.Golden, goldEquip.Quality);
        Assert.NotEmpty(goldEquip.Code);

        Assert.True(itemCatalog.TryGetItemByRowId(ItemQualityKind.Platina, 1, out ItemTemplate? platinaEquip) && platinaEquip is not null);
        Assert.Equal(ItemQualityKind.Platina, platinaEquip.Quality);

        Assert.True(itemCatalog.TryGetItemByCode("tho-dia-phu", out ItemTemplate? portalScroll) && portalScroll is not null);
        Assert.Equal(ItemGenreKind.TownPortal, portalScroll.Genre);

        Assert.True(lootCatalog.TryGetLootTable("default-monster-loot", out LootTableTemplate? lootTable) && lootTable is not null);
        Assert.NotEmpty(lootTable.WeightedPools);

        Assert.Equal(179, shopCatalog.Count);
        Assert.True(shopCatalog.TryGetShop(1, out ShopTemplate? shop1) && shop1 is not null);
        Assert.NotEmpty(shop1.Goods);

        Assert.Equal(3, playerCatalog.DefaultStarterItems.Count);
        PlayerSeriesGrowthRuleTemplate metalGrowth = playerCatalog.GetSeriesGrowthRule(ElementSeries.Metal);
        Assert.True(metalGrowth.LifePerVitality > 0);
        Assert.True(playerCatalog.GetRequiredExpForNextLevel(1) > 0);
    }
}
