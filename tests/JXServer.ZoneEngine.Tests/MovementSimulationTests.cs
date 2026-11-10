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

public sealed class MovementSimulationTests
{
    private const string Server1Root = @"d:\Projects\jx-linux-bin\server1";
    private const string GatewayRoot = @"d:\Projects\jx-linux-bin\gateway";

    [Fact]
    public async Task MapZoneWorker_ShouldStepWalkAndRun_BroadcastTo3x3_AndRegenerateWhenSitting()
    {
        var fakeTime = new FakeTimeProvider();
        var mapDesc = new MapDescriptor(53, "Ba Lăng huyện", "balang", 96, 88, 16, 20);
        var worker = new MapZoneWorker(
            mapDesc,
            new SpySessionLeaseAppService(),
            new ZoneEngineOptions(),
            fakeTime,
            NullLogger.Instance);

        var sink = new RecordingOutboundSink();
        var charDto = new CharacterDto(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            SlotIndex: 0,
            RoleNameUnicode: "Trương Vô Kỵ",
            Gender: CharacterGender.Male,
            Series: ElementSeries.Fire,
            Level: 10,
            Experience: 0,
            LeadExperience: 0,
            MapId: 53,
            RevivalMapId: 53,
            RevivalPointId: 19,
            MapMpsX: 52000,
            MapMpsY: 101696,
            CurrentLife: 100,
            MaxLife: 200,
            CurrentMana: 40,
            MaxMana: 100,
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

        Assert.True(worker.TryEnqueueCommand(new AttachPlayerZoneCommand(charDto, 1, sink)));
        Assert.True(worker.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(charDto.Id, 1, 10u, 20u)));
        await worker.StepSingleTickAsync();

        // 1. Toggle sit (0x71) -> emits 0x53 broadcast and regenerates HP/MP after 18 ticks (1 second)
        Assert.True(worker.TryEnqueueCommand(new TogglePlayerSitZoneCommand(charDto.Id, 1, IsSitting: true)));
        for (int i = 0; i < 19; i++)
        {
            await worker.StepSingleTickAsync();
        }

        Assert.Single(sink.SitBroadcasts);
        Assert.NotEmpty(sink.NormalVitalSyncs);
        Assert.True(sink.NormalVitalSyncs[^1].CurrentLife > 100);
        Assert.True(sink.NormalVitalSyncs[^1].CurrentMana > 40);

        // 2. Run command (0x4C) -> interrupts sit, broadcasts 0x51, and advances at RunSpeed (20 Mps/tick)
        Assert.True(worker.TryEnqueueCommand(new MovePlayerZoneCommand(charDto.Id, 1, 52100, 101696, IsRun: true)));
        for (int i = 0; i < 6; i++)
        {
            await worker.StepSingleTickAsync();
        }

        Assert.Single(sink.RunBroadcasts);
        Assert.Equal((52100, 101696), (sink.RunBroadcasts[0].DestMpsX, sink.RunBroadcasts[0].DestMpsY));
        Assert.Contains(sink.SelfPositionSyncs, p => p.MpsX == 52100 && p.MpsY == 101696);

        // 3. Jump command (0x4E) -> broadcasts 0x54 and arrives at target
        Assert.True(worker.TryEnqueueCommand(new JumpPlayerZoneCommand(charDto.Id, 1, 52164, 101696)));
        await worker.StepSingleTickAsync();

        Assert.Single(sink.JumpBroadcasts);
        Assert.Equal(52164, sink.JumpBroadcasts[0].DestMpsX);
    }

    [Fact]
    public async Task MapZoneWorker_ShouldStopMovementAtImpassableObstacleCell()
    {
        var assetOptions = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = Server1Root,
            GatewayRootPath = GatewayRoot,
            EagerObstacleLoadMapIds = [53],
        });
        var mapCatalog = new MapCatalog(assetOptions, NullLogger<MapCatalog>.Instance);
        Assert.True(mapCatalog.TryGetMapDescriptor(53, out MapDescriptor? map53));

        // Find a passable cell adjacent to an Obstacle_Full (1) cell in Map 53 Region (101, 99)
        Assert.True(mapCatalog.TryGetRegionObstacleGrid(53, 101, 99, out RegionObstacleGrid? grid));
        Assert.NotNull(grid);

        int startMpsX = 52032;
        int startMpsY = 101696;
        int blockedMpsX = -1;
        int blockedMpsY = -1;

        for (int cx = 1; cx < 15 && blockedMpsX < 0; cx++)
        {
            for (int cy = 1; cy < 31; cy++)
            {
                if ((grid.GetObstacle(cx, cy) & 0x0F) == 0 && (grid.GetObstacle(cx + 1, cy) & 0x0F) == 1)
                {
                    startMpsX = (101 * WorldCoordinateConsts.RegionWidthMps) + (cx * 32) + 16;
                    startMpsY = (99 * WorldCoordinateConsts.RegionHeightMps) + (cy * 32) + 16;
                    blockedMpsX = (101 * WorldCoordinateConsts.RegionWidthMps) + ((cx + 2) * 32) + 16;
                    blockedMpsY = startMpsY;
                    break;
                }
            }
        }

        Assert.True(blockedMpsX > startMpsX);

        var worker = new MapZoneWorker(
            map53!,
            new SpySessionLeaseAppService(),
            new ZoneEngineOptions(),
            new FakeTimeProvider(),
            NullLogger.Instance,
            mapCatalog);

        var sink = new RecordingOutboundSink();
        var charDto = new CharacterDto(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            SlotIndex: 0,
            RoleNameUnicode: "Lệnh Hồ Xung",
            Gender: CharacterGender.Male,
            Series: ElementSeries.Metal,
            Level: 10,
            Experience: 0,
            LeadExperience: 0,
            MapId: 53,
            RevivalMapId: 53,
            RevivalPointId: 19,
            MapMpsX: startMpsX,
            MapMpsY: startMpsY,
            CurrentLife: 200,
            MaxLife: 200,
            CurrentMana: 100,
            MaxMana: 100,
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

        Assert.True(worker.TryEnqueueCommand(new AttachPlayerZoneCommand(charDto, 1, sink)));
        Assert.True(worker.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(charDto.Id, 1, 1u, 1u)));
        await worker.StepSingleTickAsync();

        // Attempt to walk through the blocked cell -> stops before reaching blockedMpsX
        Assert.True(worker.TryEnqueueCommand(new MovePlayerZoneCommand(charDto.Id, 1, blockedMpsX, blockedMpsY, IsRun: false)));
        for (int i = 0; i < 10; i++)
        {
            await worker.StepSingleTickAsync();
        }

        Assert.NotEmpty(sink.SelfPositionSyncs);
        Assert.True(sink.SelfPositionSyncs[^1].MpsX < blockedMpsX);
    }
}
