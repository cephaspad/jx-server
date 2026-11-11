using System.Diagnostics;
using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Items;
using JXServer.GameAssets.LootTables;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.Skills;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using JXServer.Primitives.Items;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.SpatialGrid;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JXServer.Integration.Tests;

[Collection(NonParallelStressTestCollection.Name)]
public sealed class HighDensityCombatAndLootStressTests : IPlayerOutboundSink, ISessionLeaseAppService
{
    private int _itemSyncCount;

    [Fact]
    public async Task HighDensity_50PlayersCastingProjectilesKillingMonstersAndRacingGroundLoot_ShouldMaintainP99TickUnder15MsAndZeroDuplication()
    {
        IOptions<GameAssetsOptions> assetOpts = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = @"D:\Projects\jx-linux-bin\server1",
            DataDirectory = "data",
            ArtifactsDirectory = "artifacts",
            PreferBinarySnapshots = true,
        });
        var skillCatalog = new SkillTemplateCatalog(assetOpts, NullLogger<SkillTemplateCatalog>.Instance);
        var missileCatalog = new MissileTemplateCatalog(assetOpts);
        var itemCatalog = new ItemTemplateCatalog(assetOpts, NullLogger<ItemTemplateCatalog>.Instance);
        var lootCatalog = new LootTableCatalog(assetOpts, NullLogger<LootTableCatalog>.Instance);
        var generator = new ItemGenerator(itemCatalog);

        var spawns = new List<StaticNpcSpawnTemplate>(25);
        for (int m = 0; m < 25; m++)
        {
            spawns.Add(new StaticNpcSpawnTemplate(
                TemplateId: 1,
                MapId: 53,
                SpawnMpsX: 52000 + ((m % 5) * 96),
                SpawnMpsY: 101600 + ((m / 5) * 96),
                Level: 5,
                Kind: EntityKind.Monster,
                Camp: EntityCamp.Animal,
                Series: (ElementSeries)(m % 5),
                IsSpecialNpc: false,
                DisplayName: $"Hươu Đốm #{m + 1}",
                ScriptPath: string.Empty));
        }

        var mapDesc = new MapDescriptor(53, "Ba Lăng Huyện", "balanghuyen", 96, 88, 16, 20)
        {
            RegionNpcSpawns = spawns,
        };

        var worker = new MapZoneWorker(
            mapDesc,
            this,
            new ZoneEngineOptions(),
            new FakeTimeProvider(),
            NullLogger.Instance,
            skillTemplateCatalog: skillCatalog,
            missileTemplateCatalog: missileCatalog,
            itemTemplateCatalog: itemCatalog,
            lootTableCatalog: lootCatalog,
            itemGenerator: generator);

        var players = new List<CharacterDto>(50);
        for (int i = 0; i < 50; i++)
        {
            var dto = new CharacterDto(
                Id: Guid.NewGuid(),
                AccountId: Guid.NewGuid(),
                SlotIndex: 0,
                RoleNameUnicode: $"HiệpKhách{i:D2}",
                Gender: (CharacterGender)(i % 2),
                Series: (ElementSeries)(i % 5),
                Level: 30,
                Experience: 0,
                LeadExperience: 0,
                MapId: 53,
                RevivalMapId: 53,
                RevivalPointId: 19,
                MapMpsX: 51968 + ((i % 10) * 48),
                MapMpsY: 101568 + ((i / 10) * 48),
                CurrentLife: 2000,
                MaxLife: 2000,
                CurrentMana: 2000,
                MaxMana: 2000,
                CurrentStamina: 1000,
                MaxStamina: 1000,
                Strength: 100,
                Dexterity: 100,
                Vitality: 100,
                Energy: 100,
                Lucky: 25,
                UnallocatedAttributePoints: 0,
                UnallocatedSkillPoints: 0,
                InventoryMoney: 0,
                RepositoryMoney: 0,
                CurrentFactionId: 0xFF,
                FirstFactionId: 0xFF,
                FactionJoinCount: 0,
                Skills:
                [
                    new CharacterSkillDto(302, 10, 0), // Ranged multi-projectile skill
                ],
                PkState: PlayerPkMode.Fight);

            players.Add(dto);
            Assert.True(worker.TryEnqueueCommand(new AttachPlayerZoneCommand(dto, i + 1, this)));
            Assert.True(worker.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(dto.Id, i + 1, (uint)(i + 1), 100u)));
        }

        await worker.StepSingleTickAsync();
        Assert.Equal(50, worker.ActivePlayerCount);
        _itemSyncCount = 0;

        foreach (SpatialRegionSlot slot in worker.RegionGrid.ActiveRegions)
        {
            foreach (PlayerZoneState p in slot.Players)
            {
                p.FightMode = 1;
            }
        }

        // 1. Concurrent Ground Pickup Race Verification (SC-004):
        // Spawn a single public (ownerless) 6-affix Blue item on the ground and have all 50 players race to pick it up in the same tick.
        GeneratedItemData contestedBlueGear = generator.GenerateEquipment(
            detailType: 0,
            particularType: 1,
            series: ElementSeries.Metal,
            level: 5,
            magicLevelThresholds: [10, 10, 10, 10, 10, 10],
            luck: 30);
        GroundObjectSlot contestedGroundObj = worker.GroundObjectSystem.SpawnItemGroundObject(
            53,
            null,
            worker.RegionGrid,
            PlayerItemSlot.FromGeneratedItem(888888u, contestedBlueGear),
            52064,
            101664,
            ownerCharacterId: null,
            currentTick: worker.CurrentFrameTick);

        for (int i = 0; i < players.Count; i++)
        {
            worker.TryEnqueueCommand(new PickupGroundObjectZoneCommand(
                players[i].Id,
                i + 1,
                contestedGroundObj.ObjId,
                ItemContainerPlace.EquipRoom,
                0,
                0));
        }

        await worker.StepSingleTickAsync();

        Assert.Equal(1, _itemSyncCount); // Zero item duplication across 50 concurrent pickup commands!

        // 2. Warmup JIT for projectile combat, monster AI, and loot drops before p99 latency measurement
        worker.TryEnqueueCommand(new CastSkillZoneCommand(players[0].Id, 1, 302, 52160, 101696));
        for (int w = 0; w < 5; w++)
        {
            await worker.StepSingleTickAsync();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 3. Measure 180 ticks (10 seconds @ 18 Hz) of 50 players casting multi-projectiles, monster AI/respawns, and loot drops
        const int totalTicks = 180;
        var tickDurationsMs = new List<double>(totalTicks);
        for (int tick = 0; tick < totalTicks; tick++)
        {
            if (tick % 18 == 0)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    worker.TryEnqueueCommand(new CastSkillZoneCommand(
                        players[i].Id,
                        i + 1,
                        302,
                        52000 + ((i % 5) * 96),
                        101600 + ((i / 10) * 96)));
                }
            }

            long start = Stopwatch.GetTimestamp();
            await worker.StepSingleTickAsync();
            double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            tickDurationsMs.Add(elapsedMs);
        }

        tickDurationsMs.Sort();
        int p99Index = (int)Math.Floor(tickDurationsMs.Count * 0.99) - 1;
        double p99Ms = tickDurationsMs[Math.Clamp(p99Index, 0, tickDurationsMs.Count - 1)];

        // SC-003: 99th-percentile tick duration under 15 milliseconds under 50-player combat & projectile load
        Assert.True(p99Ms < 15.0, $"Expected p99 combat tick duration < 15ms, actual was {p99Ms:F3}ms.");
    }

    void IWorldOutboundSink.SendInitialWorldSyncBurst(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick,
        IReadOnlyList<CharacterSkillDto> skills)
    {
    }

    void IWorldOutboundSink.SendActiveWorldConfirmation(PlayerZoneState playerState)
    {
    }

    void IInventoryOutboundSink.SendItemSync(in PlayerItemSyncSnapshot snapshot) => _itemSyncCount++;

    void IWorldOutboundSink.SendServerPing(uint serverTimestamp)
    {
    }

    void IWorldOutboundSink.SendServerPingReply(uint echoedClientTimestamp)
    {
    }

    void IWorldOutboundSink.Disconnect(PlayerDetachReason reason)
    {
    }

    Task<WorldHandoffTicketDto> ISessionLeaseAppService.SelectCharacterForWorldEntryAsync(SelectCharacterInput input, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    Task<RedeemHandoffTicketResultDto> ISessionLeaseAppService.RedeemWorldHandoffTicketAsync(RedeemHandoffTicketInput input, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    Task<bool> ISessionLeaseAppService.RecordHeartbeatAsync(Guid accountId, long leaseEpoch, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    Task ISessionLeaseAppService.CompleteLogoutOrEvictAsync(ReleaseSessionLeaseInput input, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
