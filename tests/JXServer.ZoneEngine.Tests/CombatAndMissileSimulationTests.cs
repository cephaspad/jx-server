using JXServer.Contracts.Characters;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.NpcTemplates;
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

public sealed class CombatAndMissileSimulationTests
{

    [Fact]
    public void CombatDamageCalculator_ShouldEnforceRelationsFiveElementsAndWeightedPoisonBlending()
    {
        // 1. Peace zone player cannot attack monster
        EntityRelationKind peaceRel = CombatDamageCalculator.EvaluateRelation(
            10000u,
            EntityKind.Player,
            EntityCamp.Justice,
            attackerFightMode: 0,
            PlayerPkMode.Fight,
            1u,
            EntityKind.Monster,
            EntityCamp.Animal,
            defenderFightMode: 1,
            PlayerPkMode.Fight);
        Assert.Equal(EntityRelationKind.None, peaceRel);

        // 2. Wilderness player vs Animal monster is Enemy
        EntityRelationKind wildRel = CombatDamageCalculator.EvaluateRelation(
            10000u,
            EntityKind.Player,
            EntityCamp.Begin,
            attackerFightMode: 1,
            PlayerPkMode.Peace,
            1u,
            EntityKind.Monster,
            EntityCamp.Animal,
            defenderFightMode: 1,
            PlayerPkMode.Fight);
        Assert.Equal(EntityRelationKind.Enemy, wildRel);

        // 3. Five-Elements conquest (Metal -> Wood = 120%, Wood -> Metal = 80%) and resistance scaling
        var fx = new EntityStatusEffectState();
        var rng = new Random(42);
        var metalPayload = new SkillDamagePayload(
            SkillId: 53,
            SkillLevel: 1,
            ElementSeries: ElementSeries.Metal,
            IsPhysical: true,
            IsMelee: true,
            UseAttackRating: false,
            DoHurtAnimation: true,
            AttackRatingScore: 500,
            PhysicsMinDamage: 100,
            PhysicsMaxDamage: 100,
            ColdMinDamage: 0,
            ColdMaxDamage: 0,
            ColdFreezeDurationFrames: 0,
            FireMinDamage: 0,
            FireMaxDamage: 0,
            LightMinDamage: 0,
            LightMaxDamage: 0,
            LightStunDurationFrames: 0,
            PoisonDamagePerTick: 0,
            PoisonDurationFrames: 0,
            PoisonIntervalFrames: 18,
            MagicMinDamage: 0,
            MagicMaxDamage: 0);

        int dmgConquer = CombatDamageCalculator.ResolveHitAndApplyDamage(
            10000u,
            isPvp: false,
            in metalPayload,
            ElementSeries.Wood,
            defenderPhysicsResist: 0,
            defenderColdResist: 0,
            defenderFireResist: 0,
            defenderLightResist: 0,
            defenderPoisonResist: 0,
            fx,
            rng);
        Assert.Equal(120, dmgConquer);

        int dmgConqueredWith50Res = CombatDamageCalculator.ResolveHitAndApplyDamage(
            10000u,
            isPvp: false,
            in metalPayload,
            ElementSeries.Fire,
            defenderPhysicsResist: 50,
            defenderColdResist: 0,
            defenderFireResist: 0,
            defenderLightResist: 0,
            defenderPoisonResist: 0,
            fx,
            rng);
        Assert.Equal(40, dmgConqueredWith50Res);

        // 4. Weighted Poison DoT blending formula
        fx.BlendPoisonDamageOverTime(incomingDamagePerTick: 20, incomingDurationFrames: 90, incomingIntervalFrames: 18, attackerNpcId: 10000u);
        Assert.Equal(20, fx.PoisonDamagePerTick);
        Assert.Equal(90, fx.PoisonRemainingFrames);

        fx.BlendPoisonDamageOverTime(incomingDamagePerTick: 40, incomingDurationFrames: 90, incomingIntervalFrames: 18, attackerNpcId: 10000u);
        Assert.True(fx.PoisonDamagePerTick > 20 && fx.PoisonDamagePerTick <= 40);
        Assert.Equal(90, fx.PoisonRemainingFrames);
    }

    [Fact]
    public async Task MapZoneWorker_ShouldRejectPeaceZoneSkillCast_AndExecuteProjectileCollisionInWilderness()
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
                    SpawnMpsX: 52128,
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
            missileTemplateCatalog: missileCatalog);

        var sink = new RecordingOutboundSink();
        Guid charId = Guid.NewGuid();
        var dto = new CharacterDto(
            Id: charId,
            AccountId: Guid.NewGuid(),
            SlotIndex: 0,
            RoleNameUnicode: "Độc Cô Cầu Bại",
            Gender: CharacterGender.Male,
            Series: ElementSeries.Fire,
            Level: 5,
            Experience: 0,
            LeadExperience: 0,
            MapId: 53,
            RevivalMapId: 53,
            RevivalPointId: 19,
            MapMpsX: 52000,
            MapMpsY: 101696,
            CurrentLife: 500,
            MaxLife: 500,
            CurrentMana: 500,
            MaxMana: 500,
            CurrentStamina: 200,
            MaxStamina: 200,
            Strength: 50,
            Dexterity: 50,
            Vitality: 50,
            Energy: 50,
            Lucky: 0,
            UnallocatedAttributePoints: 0,
            UnallocatedSkillPoints: 0,
            InventoryMoney: 1000,
            RepositoryMoney: 0,
            CurrentFactionId: 255,
            FirstFactionId: 255,
            FactionJoinCount: 0,
            Skills:
            [
                new CharacterSkillDto(53, 1, 0),
                new CharacterSkillDto(88, 5, 0), // Bất Diệt Bất Tuyệt (projectile missileId = 3, collideVanish = true, doHurtAnimation = true)
            ]);

        worker.TryEnqueueCommand(new AttachPlayerZoneCommand(dto, 1L, sink));
        worker.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(charId, 1L, 0x1111u, 1u));
        await worker.StepSingleTickAsync();

        // 1. Attempt to cast offensive skill 88 while FightMode == 0 -> rejected
        worker.TryEnqueueCommand(new CastSkillZoneCommand(charId, 1L, 88, 52128, 101696));
        await worker.StepSingleTickAsync();
        Assert.Empty(sink.SkillCastBroadcasts);

        // 2. Switch to FightMode == 1 and cast projectile skill 88 toward monster at (52128, 101696)
        NpcZoneState monster = worker.StaticNpcs[1u];
        uint initialMonsterHp = monster.CurLife;

        // Access player via PK command and set FightMode = 1
        foreach (SpatialRegionSlot region in worker.RegionGrid.ActiveRegions)
        {
            foreach (PlayerZoneState p in region.Players)
            {
                p.FightMode = 1;
            }
        }

        worker.TryEnqueueCommand(new CastSkillZoneCommand(charId, 1L, 88, 52128, 101696));
        await worker.StepSingleTickAsync();

        Assert.Single(sink.SkillCastBroadcasts);
        Assert.Equal(88u, sink.SkillCastBroadcasts[0].SkillId);
        Assert.Equal(1, worker.ActiveMissileCount);

        // Step ticks until projectile collides with monster
        for (int t = 0; t < 6; t++)
        {
            await worker.StepSingleTickAsync();
        }

        Assert.Equal(0, worker.ActiveMissileCount);
        Assert.True(monster.CurLife < initialMonsterHp);
        Assert.NotEmpty(sink.HurtBroadcasts);
    }
}
