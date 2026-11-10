using System.IO;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Items;
using JXServer.GameAssets.LootTables;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.Skills;
using JXServer.GameAssets.Snapshots;
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
        Assert.NotEmpty(bundle.Skills);
        Assert.NotEmpty(bundle.Missiles);
        Assert.NotEmpty(bundle.Items);
        Assert.NotEmpty(bundle.MagicAffixes);
        Assert.NotEmpty(bundle.LootTables);
        Assert.Equal(10, bundle.CharacterTemplates.Count);
        Assert.NotEmpty(bundle.DefaultStarterItems);

        byte[] roundTripBytes = MemoryPackSerializer.Serialize(bundle);
        GameAssetsSnapshotBundle? roundTrip = MemoryPackSerializer.Deserialize<GameAssetsSnapshotBundle>(roundTripBytes);
        Assert.NotNull(roundTrip);
        Assert.Equal(bundle.SourceJsonSha256Hex, roundTrip.SourceJsonSha256Hex);
        Assert.Equal(bundle.Skills.Count, roundTrip.Skills.Count);
        Assert.Equal(bundle.Items.Count, roundTrip.Items.Count);
    }

    [Fact]
    public void SkillParameterCurveTemplate_EvaluatesLinearPiecewiseAndConicCurvesDeterministically()
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
    }

    [Fact]
    public void Catalogs_LoadExpectedVietnameseNames_AndStarterItems()
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

        Assert.True(skillCatalog.TryGetSkill(53, out SkillTemplate? unarmed) && unarmed is not null);
        Assert.Equal("Quyền Pháp Cơ Bản", unarmed.Name);

        Assert.True(skillCatalog.TryGetSkill(100, out SkillTemplate? fireSkill) && fireSkill is not null);
        Assert.True(missileCatalog.TryGetMissile(fireSkill.MissileId, out MissileTemplate? fireMissile) && fireMissile is not null);
        Assert.Equal("Hỏa Cầu", fireMissile.Name);

        Assert.True(itemCatalog.TryGetItemByCode("kim-sang-duoc", out ItemTemplate? healPotion) && healPotion is not null);
        Assert.Equal(ItemGenreKind.Medicine, healPotion.Genre);
        Assert.Equal("Kim Sáng Dược (Tiểu)", healPotion.Name);

        Assert.True(itemCatalog.TryGetItemByCode("tho-dia-phu", out ItemTemplate? portalScroll) && portalScroll is not null);
        Assert.Equal(ItemGenreKind.TownPortal, portalScroll.Genre);

        Assert.True(lootCatalog.TryGetLootTable("default-monster-loot", out LootTableTemplate? lootTable) && lootTable is not null);
        Assert.NotEmpty(lootTable.WeightedPools);

        Assert.Equal(3, playerCatalog.DefaultStarterItems.Count);
        PlayerSeriesGrowthRuleTemplate metalGrowth = playerCatalog.GetSeriesGrowthRule(ElementSeries.Metal);
        Assert.True(metalGrowth.LifePerVitality > 0);
        Assert.True(playerCatalog.GetRequiredExpForNextLevel(1) > 0);
    }
}
