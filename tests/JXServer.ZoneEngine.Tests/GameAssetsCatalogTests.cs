using JXServer.GameAssets.Archives;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.WorldZones;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace JXServer.ZoneEngine.Tests;

/// <summary>
/// Verifies <see cref="PakFilePathHasher"/>, <see cref="XPackArchiveReader"/>, <see cref="MapCatalog"/>,
/// and <see cref="PlayerTemplateCatalog"/> against <c>d:\Projects\jx-linux-bin</c> (`US4`, `FR-012`).
/// </summary>
public sealed class GameAssetsCatalogTests
{
    private const string Server1Root = @"d:\Projects\jx-linux-bin\server1";
    private const string GatewayRoot = @"d:\Projects\jx-linux-bin\gateway";

    [Fact]
    public void PakFilePathHasher_ShouldNormalizeCaseAndSlashesDeterministically()
    {
        uint h1 = PakFilePathHasher.ComputeHash(@"\maps\ÖÐÔ­ÄÏÇø\°ÍÁêÏØ\°ÍÁêÏØ.wor");
        uint h2 = PakFilePathHasher.ComputeHash(@"/MAPS/ÖÐÔ­ÄÏÇø/°ÍÁêÏØ/°ÍÁêÏØ.WOR");
        Assert.Equal(h1, h2);
        Assert.NotEqual(0u, h1);
    }

    [Fact]
    public void MapCatalog_ShouldLoadWorldSet_Map53BaLangHuyen_AndObstacleGrid()
    {
        var options = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = Server1Root,
            GatewayRootPath = GatewayRoot,
            DefaultFallbackStarterVillageMapId = 53,
            FallbackUnloadedStarterVillage = true,
            EagerObstacleLoadMapIds = [53]
        });

        var catalog = new MapCatalog(options, NullLogger<MapCatalog>.Instance);

        Assert.NotEmpty(catalog.ActiveMaps);
        Assert.True(catalog.IsMapLoaded(53));
        Assert.True(catalog.TryGetMapDescriptor(53, out MapDescriptor? map53));
        Assert.NotNull(map53);
        Assert.Contains("Ba Lăng", map53.MapNameUnicode, StringComparison.OrdinalIgnoreCase);

        // Verify Ba Lăng Huyện spawn coordinate (MpsX=52032, MpsY=101696 -> Region 101, 99)
        var spawnPos = new WorldPosition(53, 52032, 101696);
        Assert.Equal(101, spawnPos.RegionX);
        Assert.Equal(99, spawnPos.RegionY);
        Assert.Equal((99 << 16) | 101, spawnPos.PackedRegionId);

        Assert.True(catalog.TryGetRegionObstacleGrid(53, spawnPos.RegionX, spawnPos.RegionY, out RegionObstacleGrid? grid));
        Assert.NotNull(grid);
        Assert.Equal(0, grid.GetObstacle(spawnPos.CellX, spawnPos.CellY));
    }

    [Fact]
    public void PlayerTemplateCatalog_ShouldLoadAll10SeriesGenderTemplates_And8StarterVillages()
    {
        var options = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = Server1Root,
            GatewayRootPath = GatewayRoot
        });

        var catalog = new PlayerTemplateCatalog(options, NullLogger<PlayerTemplateCatalog>.Instance);

        Assert.Equal(10, catalog.CharacterTemplates.Count);
        Assert.Equal(8, catalog.StarterVillages.Count);

        // Verify Metal Male (00)
        Assert.True(catalog.TryGetCharacterTemplate(ElementSeries.Metal, CharacterGender.Male, out CharacterBaseTemplate? metalMale));
        Assert.NotNull(metalMale);
        Assert.Equal(35, metalMale.BaseStrength);
        Assert.Equal(25, metalMale.BaseDexterity);
        Assert.Equal(25, metalMale.BaseVitality);
        Assert.Equal(15, metalMale.BaseEnergy);
        Assert.Equal(204, metalMale.InitialLifeMax);
        Assert.Equal(16, metalMale.InitialManaMax);
        Assert.Equal(100, metalMale.InitialStaminaMax);
        Assert.Equal(7, metalMale.StarterSkills.Count);

        // Verify Water Male (04) vs Water Female (05)
        Assert.True(catalog.TryGetCharacterTemplate(ElementSeries.Water, CharacterGender.Male, out CharacterBaseTemplate? waterMale));
        Assert.True(catalog.TryGetCharacterTemplate(ElementSeries.Water, CharacterGender.Female, out CharacterBaseTemplate? waterFemale));
        Assert.Equal(100, waterMale!.InitialLifeMax);
        Assert.Equal(200, waterMale.InitialManaMax);
        Assert.Equal(153, waterFemale!.InitialLifeMax);
        Assert.Equal(77, waterFemale.InitialManaMax);

        // Verify Ba Lăng Huyện (Map 53) starter village
        Assert.True(catalog.TryGetStarterVillage(53, out StarterVillageTemplate? baLang));
        Assert.NotNull(baLang);
        Assert.Equal(19, baLang.RevivalId);
        Assert.Equal(52032, baLang.SpawnMpsX);
        Assert.Equal(101696, baLang.SpawnMpsY);

        // Verify prohibited name filter (goddess_rolename.flt)
        Assert.True(catalog.IsRoleNameProhibited("GM_Admin"));
        Assert.False(catalog.IsRoleNameProhibited("Độc Cô Cầu Bại"));
    }

    [Fact]
    public void MapCatalog_ShouldHaltOnMissingServerRootPath()
    {
        var badOptions = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = @"d:\NonExistentServerRoot_12345"
        });

        Assert.Throws<DirectoryNotFoundException>(() =>
            new MapCatalog(badOptions, NullLogger<MapCatalog>.Instance));
    }
}
