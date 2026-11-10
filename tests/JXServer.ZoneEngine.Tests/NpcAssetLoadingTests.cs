using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.WorldZones;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace JXServer.ZoneEngine.Tests;

/// <summary>
/// Verifies <see cref="NpcTemplateCatalog"/>, <see cref="TrapScriptCatalog"/>, <see cref="RegionTrapGrid"/>,
/// and <see cref="MapCatalog"/> Section 1 (<c>TRAP_SECTION</c>) &amp; Section 2 (<c>NPC_SECTION</c>) parsing
/// against <c>d:\Projects\jx-linux-bin\server1</c> (`US3`, `US4`, `FR-004`, `FR-008`).
/// </summary>
public sealed class NpcAssetLoadingTests
{
    private const string Server1Root = @"d:\Projects\jx-linux-bin\server1";
    private const string GatewayRoot = @"d:\Projects\jx-linux-bin\gateway";

    [Fact]
    public void NpcTemplateCatalog_ShouldLoadVietnameseTemplates_AndAutoExecSpawns()
    {
        var options = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = Server1Root,
            GatewayRootPath = GatewayRoot,
        });

        var npcCatalog = new NpcTemplateCatalog(options, NullLogger<NpcTemplateCatalog>.Instance);

        Assert.True(npcCatalog.Count > 1000);

        // Template 382 in settings/npcs.txt is DialogNpc ("Nữ trung niên 3"), Template 43 is Monster ("Bò cạp")
        Assert.True(npcCatalog.TryGetTemplate(382, out NpcTemplate? dialogTemplate));
        Assert.NotNull(dialogTemplate);
        Assert.Equal(EntityKind.DialogNpc, dialogTemplate.Kind);
        Assert.Contains("Nữ trung", dialogTemplate.Name, StringComparison.OrdinalIgnoreCase);

        Assert.True(npcCatalog.TryGetTemplate(43, out NpcTemplate? scorpionTemplate));
        Assert.NotNull(scorpionTemplate);
        Assert.Equal(EntityKind.Monster, scorpionTemplate.Kind);
        Assert.Contains("Bò cạp", scorpionTemplate.Name, StringComparison.OrdinalIgnoreCase);

        // Autoexec spawns for Map 53 (Ba Lăng Huyện)
        IReadOnlyList<StaticNpcSpawnTemplate> autoSpawns53 = npcCatalog.GetAutoExecSpawnsForMap(53);
        Assert.NotEmpty(autoSpawns53);
        Assert.Contains(autoSpawns53, s => s.DisplayName.Contains("Thần Tiên Tỷ Tỷ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(autoSpawns53, s => s.DisplayName.Contains("bang hội", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MapCatalog_ShouldParseMap53NpcSection_AndTrapSection_WithVietnameseNames()
    {
        var options = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = Server1Root,
            GatewayRootPath = GatewayRoot,
            EagerObstacleLoadMapIds = [53],
        });

        var npcCatalog = new NpcTemplateCatalog(options, NullLogger<NpcTemplateCatalog>.Instance);
        var mapCatalog = new MapCatalog(options, NullLogger<MapCatalog>.Instance, npcCatalog);
        var trapCatalog = new TrapScriptCatalog(options, NullLogger<TrapScriptCatalog>.Instance);

        Assert.True(trapCatalog.Count > 50);

        IReadOnlyList<StaticNpcSpawnTemplate> spawns53 = mapCatalog.GetStaticNpcSpawnsForMap(53);
        Assert.True(spawns53.Count > 500);

        // Verify both Dialog NPCs and Monsters are spawned with Vietnamese names
        Assert.Contains(spawns53, s => s.Kind == EntityKind.DialogNpc && s.DisplayName.Contains("Thần Tiên Tỷ Tỷ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(spawns53, s => s.Kind == EntityKind.Monster && s.DisplayName.Contains("Bò cạp", StringComparison.OrdinalIgnoreCase) && s.Level >= 1);

        // Verify Map 53 has trap regions in Region_S.dat Section 1 whose TrapIds resolve in TrapScriptCatalog
        Assert.True(mapCatalog.TryGetMapDescriptor(53, out MapDescriptor? map53));
        Assert.NotNull(map53);

        uint foundTrapId = 0;
        for (int ry = map53.RegionBeginY; ry < map53.RegionBeginY + map53.RegionHeight && foundTrapId == 0; ry++)
        {
            for (int rx = map53.RegionBeginX; rx < map53.RegionBeginX + map53.RegionWidth && foundTrapId == 0; rx++)
            {
                if (!mapCatalog.TryGetRegionTrapGrid(53, rx, ry, out RegionTrapGrid? trapGrid) || trapGrid is null)
                {
                    continue;
                }

                for (int cx = 0; cx < WorldCoordinateConsts.RegionWidthCells && foundTrapId == 0; cx++)
                {
                    for (int cy = 0; cy < WorldCoordinateConsts.RegionHeightCells; cy++)
                    {
                        uint tid = trapGrid.GetTrapId(cx, cy);
                        if (tid != 0 && trapCatalog.TryGetTrapBinding(tid, out _))
                        {
                            foundTrapId = tid;
                            break;
                        }
                    }
                }
            }
        }

        Assert.NotEqual(0u, foundTrapId);
        Assert.True(trapCatalog.TryGetTrapBinding(foundTrapId, out PortalTrapBindingTemplate? binding));
        Assert.NotNull(binding);
    }
}
