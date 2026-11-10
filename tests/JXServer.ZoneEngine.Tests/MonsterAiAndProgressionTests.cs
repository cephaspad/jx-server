using JXServer.Contracts.Characters;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.Skills;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.NpcEntities;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.SpatialGrid;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace JXServer.ZoneEngine.Tests;

public sealed class MonsterAiAndProgressionTests
{
    [Fact]
    public async Task MonsterAiTickSystem_ShouldAggroPursueAttackLeashDieRespawn_AndAwardLevelUpAndStatAllocation()
    {
        IOptions<GameAssetsOptions> opts = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = @"D:\Projects\jx-linux-bin\server1",
            DataDirectory = "data",
            ArtifactsDirectory = "artifacts",
            PreferBinarySnapshots = true,
        });
        var skillCatalog = new SkillTemplateCatalog(opts, NullLogger<SkillTemplateCatalog>.Instance);
        var missileCatalog = new MissileTemplateCatalog(opts);
        var playerCatalog = new PlayerTemplateCatalog(opts, NullLogger<PlayerTemplateCatalog>.Instance);

        var mapDesc = new MapDescriptor(
            MapId: 53,
            MapNameUnicode: "Ba Lăng Huyện",
            RawSubPath: "balanghuyen",
            RegionBeginX: 0,
            RegionBeginY: 0,
            RegionWidth: 200,
            RegionHeight: 200)
        {
            RegionNpcSpawns =
            [
                new StaticNpcSpawnTemplate(
                    TemplateId: 1,
                    MapId: 53,
                    SpawnMpsX: 52064,
                    SpawnMpsY: 101696,
                    Level: 1,
                    Kind: EntityKind.Monster,
                    Camp: EntityCamp.Animal,
                    Series: ElementSeries.Wood,
                    IsSpecialNpc: false,
                    DisplayName: "Nhím",
                    ScriptPath: string.Empty),
            ],
        };

        var worker = new MapZoneWorker(
            mapDesc,
            new SpySessionLeaseAppService(),
            new ZoneEngineOptions(),
            TimeProvider.System,
            NullLogger.Instance,
            skillTemplateCatalog: skillCatalog,
            missileTemplateCatalog: missileCatalog,
            playerTemplateCatalog: playerCatalog);

        var sink = new RecordingOutboundSink();
        Guid charId = Guid.NewGuid();
        var dto = new CharacterDto(
            Id: charId,
            AccountId: Guid.NewGuid(),
            SlotIndex: 0,
            RoleNameUnicode: "Dương Quá",
            Gender: CharacterGender.Male,
            Series: ElementSeries.Metal,
            Level: 1,
            Experience: 95,
            LeadExperience: 0,
            MapId: 53,
            RevivalMapId: 53,
            RevivalPointId: 19,
            MapMpsX: 52000,
            MapMpsY: 101696,
            CurrentLife: 300,
            MaxLife: 300,
            CurrentMana: 200,
            MaxMana: 200,
            CurrentStamina: 200,
            MaxStamina: 200,
            Strength: 35,
            Dexterity: 25,
            Vitality: 25,
            Energy: 15,
            Lucky: 0,
            UnallocatedAttributePoints: 0,
            UnallocatedSkillPoints: 0,
            InventoryMoney: 500,
            RepositoryMoney: 0,
            CurrentFactionId: 255,
            FirstFactionId: 255,
            FactionJoinCount: 0,
            Skills:
            [
                new CharacterSkillDto(53, 1, 0),
            ]);

        worker.TryEnqueueCommand(new AttachPlayerZoneCommand(dto, 1L, sink));
        worker.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(charId, 1L, 0x1111u, 1u));
        await worker.StepSingleTickAsync();

        NpcZoneState monster = worker.StaticNpcs[1u];
        monster.VisionRadiusMps = 400;
        monster.PursuitRadiusMps = 500;
        monster.AttackRadiusMps = 96;
        monster.RespawnFrames = 18;
        monster.ExpReward = 50;
        monster.CurLife = 40;

        PlayerZoneState? player = null;
        foreach (SpatialRegionSlot region in worker.RegionGrid.ActiveRegions)
        {
            foreach (PlayerZoneState p in region.Players)
            {
                p.FightMode = 1;
                player = p;
            }
        }

        Assert.NotNull(player);

        // 1. Step ticks so monster scans aggro, pursues player, and attacks
        for (int i = 0; i < 6; i++)
        {
            await worker.StepSingleTickAsync();
        }

        Assert.Equal(player!.RuntimeNpcId, monster.AggroTargetNpcId);
        Assert.True(monster.AiState is MonsterAiStateKind.Pursuit or MonsterAiStateKind.Attack);

        // 2. Move player far outside PursuitRadiusMps -> monster leashes and restores HP
        monster.CurLife = 20;
        player.CurrentMpsX = 53000;
        for (int i = 0; i < 20; i++)
        {
            await worker.StepSingleTickAsync();
        }

        Assert.Equal(MonsterAiStateKind.IdlePatrol, monster.AiState);
        Assert.Equal(0u, monster.AggroTargetNpcId);
        Assert.Equal(monster.MaxLife, monster.CurLife);

        // 3. Move player back next to monster and cast melee skill 53 until monster dies
        player.CurrentMpsX = monster.MpsX - 32;
        player.CurrentMpsY = monster.MpsY;
        player.WeaponMinDamage = 200;
        player.WeaponMaxDamage = 250;
        monster.CurLife = 10;

        worker.TryEnqueueCommand(new CastSkillZoneCommand(charId, 1L, 53, -1, (int)monster.NpcId));
        await worker.StepSingleTickAsync();

        Assert.Equal(0u, monster.CurLife);
        Assert.Equal(MonsterAiStateKind.DeadRespawnWait, monster.AiState);
        Assert.Contains(monster.NpcId, sink.NpcDeathBroadcasts);
        Assert.NotEmpty(sink.PlayerExpSyncs);
        Assert.NotEmpty(sink.PlayerLevelUpSyncs);
        Assert.Equal((byte)2, player.Level);
        Assert.Equal((ushort)5, player.UnallocatedAttributePoints);
        Assert.Equal((ushort)1, player.UnallocatedSkillPoints);

        // 4. Allocate +3 Vitality and +1 Skill Point into skill 53
        ushort vitBefore = player.Vitality;
        ushort maxLifeBefore = player.MaxLife;
        worker.TryEnqueueCommand(new AllocateBaseAttributeZoneCommand(charId, 1L, BaseAttributeKind.Vitality, 3));
        worker.TryEnqueueCommand(new AllocateSkillPointZoneCommand(charId, 1L, 53, 1));
        await worker.StepSingleTickAsync();

        Assert.Equal((ushort)(vitBefore + 3), player.Vitality);
        Assert.True(player.MaxLife > maxLifeBefore);
        Assert.Equal((ushort)2, player.UnallocatedAttributePoints);
        Assert.Equal((ushort)0, player.UnallocatedSkillPoints);
        Assert.True(player.TryGetLearnedSkillLevel(53, out ushort skillLvl) && skillLvl == 2);
        Assert.Single(sink.PlayerAttributeSyncs);
        Assert.Single(sink.PlayerSkillLevelSyncs);

        // 5. Advance ticks past RespawnFrames (18 ticks) -> monster respawns at SpawnMpsX/Y with full HP
        player.FightMode = 0; // enter peace mode so newly respawned monster stays in IdlePatrol
        int fullSyncCountBeforeRespawn = sink.FullSyncs.Count;
        for (int i = 0; i < 20; i++)
        {
            await worker.StepSingleTickAsync();
        }

        Assert.Equal(MonsterAiStateKind.IdlePatrol, monster.AiState);
        Assert.Equal(monster.MaxLife, monster.CurLife);
        Assert.True(sink.FullSyncs.Count > fullSyncCountBeforeRespawn);

        // 6. Simulate player death and town revival (0x74 -> 0x89)
        player.CurrentLife = 0;
        player.Doing = EntityDoingPosture.Death;
        worker.TryEnqueueCommand(new RevivePlayerZoneCommand(charId, 1L, 0));
        await worker.StepSingleTickAsync();

        Assert.Equal(player.MaxLife, player.CurrentLife);
        Assert.Equal(EntityDoingPosture.Stand, player.Doing);
        Assert.Equal((byte)0, player.FightMode);
        Assert.Single(sink.NpcReviveBroadcasts);
        Assert.Equal(player.RuntimeNpcId, sink.NpcReviveBroadcasts[0].NpcId);
    }
}
