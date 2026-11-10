using JXServer.Contracts.Characters;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JXServer.ZoneEngine.Tests;

public sealed class TrapAndMapTransitionTests
{
    private const string Server1Root = @"d:\Projects\jx-linux-bin\server1";
    private const string GatewayRoot = @"d:\Projects\jx-linux-bin\gateway";

    [Fact]
    public void TrapScriptCatalog_ShouldParseTownGates_AndCrossMapPortals_ForMap53()
    {
        var assetOptions = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = Server1Root,
            GatewayRootPath = GatewayRoot,
        });
        var trapCatalog = new TrapScriptCatalog(assetOptions, NullLogger<TrapScriptCatalog>.Instance);

        Assert.True(trapCatalog.Count > 50);

        bool foundTownGate = false;
        bool foundCrossMap = false;

        var mapCatalog = new MapCatalog(
            Options.Create(new GameAssetsOptions
            {
                ServerRootPath = Server1Root,
                GatewayRootPath = GatewayRoot,
                EagerObstacleLoadMapIds = [53],
            }),
            NullLogger<MapCatalog>.Instance);

        Assert.True(mapCatalog.TryGetMapDescriptor(53, out MapDescriptor? map53));
        for (int ry = map53!.RegionBeginY; ry < map53.RegionBeginY + map53.RegionHeight; ry++)
        {
            for (int rx = map53.RegionBeginX; rx < map53.RegionBeginX + map53.RegionWidth; rx++)
            {
                if (!mapCatalog.TryGetRegionTrapGrid(53, rx, ry, out RegionTrapGrid? trapGrid) || trapGrid is null)
                {
                    continue;
                }

                for (int cx = 0; cx < WorldCoordinateConsts.RegionWidthCells; cx++)
                {
                    for (int cy = 0; cy < WorldCoordinateConsts.RegionHeightCells; cy++)
                    {
                        uint tid = trapGrid.GetTrapId(cx, cy);
                        if (tid != 0 && trapCatalog.TryGetTrapBinding(tid, out PortalTrapBindingTemplate? b) && b is not null)
                        {
                            if (b.ActionKind == PortalTrapActionKind.ToggleFightStateAndSetPos)
                            {
                                foundTownGate = true;
                            }
                            else if (b.ActionKind == PortalTrapActionKind.ChangeMapPortal)
                            {
                                foundCrossMap = true;
                            }
                        }
                    }
                }
            }
        }

        Assert.True(foundTownGate);
        Assert.True(foundCrossMap);
    }

    [Fact]
    public async Task ZoneClusterRouter_ShouldTransferPlayerAcrossMaps_WhenSteppingOnCrossMapTrap()
    {
        var assetOptions = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = Server1Root,
            GatewayRootPath = GatewayRoot,
            EagerObstacleLoadMapIds = [53, 54],
        });
        var mapCatalog = new MapCatalog(assetOptions, NullLogger<MapCatalog>.Instance);
        var trapCatalog = new TrapScriptCatalog(assetOptions, NullLogger<TrapScriptCatalog>.Instance);

        Assert.True(mapCatalog.TryGetMapDescriptor(53, out MapDescriptor? map53));
        int trapMpsX = -1;
        int trapMpsY = -1;
        PortalTrapBindingTemplate? portalBinding = null;

        for (int ry = map53!.RegionBeginY; ry < map53.RegionBeginY + map53.RegionHeight && portalBinding is null; ry++)
        {
            for (int rx = map53.RegionBeginX; rx < map53.RegionBeginX + map53.RegionWidth && portalBinding is null; rx++)
            {
                if (!mapCatalog.TryGetRegionTrapGrid(53, rx, ry, out RegionTrapGrid? trapGrid) || trapGrid is null)
                {
                    continue;
                }

                for (int cx = 0; cx < WorldCoordinateConsts.RegionWidthCells && portalBinding is null; cx++)
                {
                    for (int cy = 0; cy < WorldCoordinateConsts.RegionHeightCells; cy++)
                    {
                        uint tid = trapGrid.GetTrapId(cx, cy);
                        if (tid != 0 &&
                            trapCatalog.TryGetTrapBinding(tid, out PortalTrapBindingTemplate? b) &&
                            b is { ActionKind: PortalTrapActionKind.ChangeMapPortal } &&
                            b.TargetMapId != 53 &&
                            mapCatalog.IsMapLoaded(b.TargetMapId))
                        {
                            portalBinding = b;
                            trapMpsX = (rx * WorldCoordinateConsts.RegionWidthMps) + (cx * 32) + 16;
                            trapMpsY = (ry * WorldCoordinateConsts.RegionHeightMps) + (cy * 32) + 16;
                            break;
                        }
                    }
                }
            }
        }

        Assert.NotNull(portalBinding);

        var router = new ZoneClusterRouter(
            mapCatalog,
            new SpySessionLeaseAppService(),
            Options.Create(new ZoneEngineOptions()),
            new FakeTimeProvider(),
            NullLogger<ZoneClusterRouter>.Instance,
            npcTemplateCatalog: null,
            trapScriptCatalog: trapCatalog);

        Assert.True(router.TryGetWorker(53, out MapZoneWorker? worker53));
        Assert.True(router.TryGetWorker(portalBinding.TargetMapId, out MapZoneWorker? targetWorker));
        Assert.NotNull(worker53);
        Assert.NotNull(targetWorker);

        var sink = new RecordingOutboundSink();
        var charDto = new CharacterDto(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            SlotIndex: 0,
            RoleNameUnicode: "Đoàn Dự",
            Gender: CharacterGender.Male,
            Series: ElementSeries.Water,
            Level: 20, // Meets MinRequiredLevel
            Experience: 0,
            LeadExperience: 0,
            MapId: 53,
            RevivalMapId: 53,
            RevivalPointId: 19,
            MapMpsX: 52032,
            MapMpsY: 101696,
            CurrentLife: 200,
            MaxLife: 200,
            CurrentMana: 200,
            MaxMana: 200,
            CurrentStamina: 100,
            MaxStamina: 100,
            Strength: 35,
            Dexterity: 25,
            Vitality: 25,
            Energy: 15,
            Lucky: 0,
            UnallocatedAttributePoints: 0,
            UnallocatedSkillPoints: 0,
            InventoryMoney: 0,
            RepositoryMoney: 0,
            CurrentFactionId: 0xFF,
            FirstFactionId: 0xFF,
            FactionJoinCount: 0,
            Skills: []);

        Assert.True(worker53.TryEnqueueCommand(new AttachPlayerZoneCommand(charDto, 1, sink)));
        Assert.True(worker53.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(charDto.Id, 1, 1u, 1u)));
        await worker53.StepSingleTickAsync();

        // Jump onto the portal trap cell -> triggers ChangeMapPortal transfer to TargetMapId
        Assert.True(worker53.TryEnqueueCommand(new JumpPlayerZoneCommand(charDto.Id, 1, trapMpsX, trapMpsY)));
        await worker53.StepSingleTickAsync();
        await targetWorker.StepSingleTickAsync();

        Assert.Equal(0, worker53.ActivePlayerCount);
        Assert.Equal(1, targetWorker.ActivePlayerCount);
        Assert.Single(sink.MapTransitionSyncs);
        Assert.Equal(portalBinding.TargetMapId, sink.MapTransitionSyncs[0].MapId);
    }
}
