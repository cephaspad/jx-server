using JXServer.Contracts.Characters;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.SpatialGrid;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JXServer.ZoneEngine.Tests;

public sealed class SpatialRegionGridTests
{
    [Fact]
    public void SpatialRegionGrid_ComputeEnteringRegions_ShouldCompute3x3SetDifference()
    {
        var grid = new SpatialRegionGrid();
        for (int ry = 95; ry <= 105; ry++)
        {
            for (int rx = 95; rx <= 105; rx++)
            {
                _ = grid.GetOrCreateRegion(rx, ry);
            }
        }

        Span<SpatialRegionSlot> buffer = new SpatialRegionSlot[9];

        // Surrounding 3x3 at (100, 99) -> all 9 regions in 3x3
        int initialCount = grid.GetSurroundingRegions(100, 99, buffer);
        Assert.Equal(9, initialCount);

        // Moving East from (100, 99) to (101, 99) -> 3 newly entered regions: (102, 98), (102, 99), (102, 100)
        int eastCount = grid.ComputeEnteringRegions(100, 99, 101, 99, buffer);
        Assert.Equal(3, eastCount);
        for (int i = 0; i < eastCount; i++)
        {
            Assert.Equal(102, buffer[i].RegionX);
            Assert.InRange(buffer[i].RegionY, 98, 100);
        }

        // Moving Diagonal from (100, 99) to (101, 100) -> 5 newly entered regions
        int diagCount = grid.ComputeEnteringRegions(100, 99, 101, 100, buffer);
        Assert.Equal(5, diagCount);
    }

    [Fact]
    public async Task MapZoneWorker_ShouldSyncStaticNpcs_AndNeighborPlayers_AndRespondTo0x48_0x49()
    {
        var fakeTime = new FakeTimeProvider();
        var staticNpc = new StaticNpcSpawnTemplate(
            TemplateId: 382,
            MapId: 53,
            SpawnMpsX: 52000, // Region 101
            SpawnMpsY: 101700, // Region 99
            Level: 10,
            Kind: EntityKind.DialogNpc,
            Camp: EntityCamp.Animal,
            Series: ElementSeries.Earth,
            IsSpecialNpc: false,
            DisplayName: "Chủ tiệm tạp hóa",
            ScriptPath: string.Empty);

        var mapDesc = new MapDescriptor(53, "Ba Lăng huyện", "balang", 96, 88, 16, 20)
        {
            RegionNpcSpawns = [staticNpc],
        };

        var worker = new MapZoneWorker(
            mapDesc,
            new SpySessionLeaseAppService(),
            new ZoneEngineOptions(),
            fakeTime,
            NullLogger.Instance);

        Assert.Equal(1, worker.ActiveNpcCount);

        var sinkA = new RecordingOutboundSink();
        var sinkB = new RecordingOutboundSink();

        CharacterDto playerA = CreateCharacter("Quách Tĩnh", 52032, 101696); // Region (101, 99)
        CharacterDto playerB = CreateCharacter("Hoàng Dung", 52064, 101696); // Region (101, 99)

        Assert.True(worker.TryEnqueueCommand(new AttachPlayerZoneCommand(playerA, 1, sinkA)));
        Assert.True(worker.TryEnqueueCommand(new AttachPlayerZoneCommand(playerB, 2, sinkB)));
        await worker.StepSingleTickAsync();

        // Complete sync for Player A -> receives static NPC full sync
        Assert.True(worker.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(playerA.Id, 1, 1u, 1u)));
        await worker.StepSingleTickAsync();

        Assert.Contains(sinkA.FullSyncs, s => s.Kind == EntityKind.DialogNpc && s.NameUtf8 == "Chủ tiệm tạp hóa");

        // Complete sync for Player B -> Player A and Player B receive each other's full sync (0x4C)
        Assert.True(worker.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(playerB.Id, 2, 2u, 2u)));
        await worker.StepSingleTickAsync();

        Assert.Contains(sinkA.FullSyncs, s => s.Kind == EntityKind.Player && s.NameUtf8 == "Hoàng Dung");
        Assert.Contains(sinkB.FullSyncs, s => s.Kind == EntityKind.Player && s.NameUtf8 == "Quách Tĩnh");

        uint playerBNpcId = sinkA.FullSyncs.First(s => s.NameUtf8 == "Hoàng Dung").NpcId;
        uint staticNpcId = sinkA.FullSyncs.First(s => s.NameUtf8 == "Chủ tiệm tạp hóa").NpcId;

        sinkA.FullSyncs.Clear();

        // Query Player B via 0x48 (IsPlayerRequest: true) and Static NPC via 0x49 (IsPlayerRequest: false)
        Assert.True(worker.TryEnqueueCommand(new RequestEntitySyncZoneCommand(playerA.Id, 1, playerBNpcId, IsPlayerRequest: true)));
        Assert.True(worker.TryEnqueueCommand(new RequestEntitySyncZoneCommand(playerA.Id, 1, staticNpcId, IsPlayerRequest: false)));
        await worker.StepSingleTickAsync();

        Assert.Equal(2, sinkA.FullSyncs.Count);
        Assert.Contains(sinkA.FullSyncs, s => s.NpcId == playerBNpcId && s.NameUtf8 == "Hoàng Dung");
        Assert.Contains(sinkA.FullSyncs, s => s.NpcId == staticNpcId && s.NameUtf8 == "Chủ tiệm tạp hóa");
    }

    private static CharacterDto CreateCharacter(string name, int mpsX, int mpsY) =>
        new(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            SlotIndex: 0,
            RoleNameUnicode: name,
            Gender: CharacterGender.Male,
            Series: ElementSeries.Metal,
            Level: 10,
            Experience: 0,
            LeadExperience: 0,
            MapId: 53,
            RevivalMapId: 53,
            RevivalPointId: 19,
            MapMpsX: mpsX,
            MapMpsY: mpsY,
            CurrentLife: 150,
            MaxLife: 200,
            CurrentMana: 50,
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
}
