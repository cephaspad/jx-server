using JXServer.Contracts.Characters;
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
using JXServer.ZoneEngine.NpcEntities;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.SpatialGrid;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace JXServer.ZoneEngine.Tests;

public sealed class BlueItemGeneratorAndLootTests
{
    [Fact]
    public void ItemGenerator_ShouldRollUpTo6BlueMagicAffixes_WithSlotParityAndZeroDuplicateAttributeIds()
    {
        IOptions<GameAssetsOptions> opts = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = @"D:\Projects\jx-linux-bin\server1",
            DataDirectory = "data",
            ArtifactsDirectory = "artifacts",
            PreferBinarySnapshots = true,
        });
        var itemCatalog = new ItemTemplateCatalog(opts, NullLogger<ItemTemplateCatalog>.Instance);
        var generator = new ItemGenerator(itemCatalog);

        int[] thresholds = [10, 10, 10, 10, 10, 10];
        GeneratedItemData blueSword = generator.GenerateEquipment(
            detailType: 0,
            particularType: 1,
            series: ElementSeries.Metal,
            level: 5,
            magicLevelThresholds: thresholds,
            luck: 30,
            randomSeedOverride: 0xABCDEF01u);

        Assert.Equal(ItemConsts.GeneratorVersion, blueSword.GeneratorVersion);
        Assert.Equal(ItemGenreKind.Equipment, blueSword.Genre);
        Assert.Equal(6, blueSword.MagicAttributes.Count);

        var seenAttrIds = new HashSet<short>();
        int populatedCount = 0;

        for (int i = 0; i < blueSword.MagicAttributes.Count; i++)
        {
            ItemMagicAttributeSlot slot = blueSword.MagicAttributes[i];
            if (!slot.IsPopulated)
            {
                continue;
            }

            populatedCount++;
            Assert.True(seenAttrIds.Add(slot.AttributeId), $"Duplicate AttributeId {slot.AttributeId} rolled in slot {i}");
        }

        Assert.True(populatedCount >= 2, "Expected Blue sword to roll at least 2 magic affixes.");
    }

    [Fact]
    public async Task GroundObjectTickSystem_ShouldSpawnMonsterDropsBroadcastObjAdd_SyncOnAoiEntry_AndExpireAtExpireTick()
    {
        IOptions<GameAssetsOptions> opts = Options.Create(new GameAssetsOptions
        {
            ServerRootPath = @"D:\Projects\jx-linux-bin\server1",
            DataDirectory = "data",
            ArtifactsDirectory = "artifacts",
            PreferBinarySnapshots = true,
        });
        var itemCatalog = new ItemTemplateCatalog(opts, NullLogger<ItemTemplateCatalog>.Instance);
        var lootCatalog = new LootTableCatalog(opts, NullLogger<LootTableCatalog>.Instance);
        var skillCatalog = new SkillTemplateCatalog(opts, NullLogger<SkillTemplateCatalog>.Instance);
        var generator = new ItemGenerator(itemCatalog);

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
                    SpawnMpsX: 52032,
                    SpawnMpsY: 101696,
                    Level: 3,
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
            itemTemplateCatalog: itemCatalog,
            lootTableCatalog: lootCatalog,
            itemGenerator: generator);

        var sink1 = new RecordingOutboundSink();
        Guid char1Id = Guid.NewGuid();
        var dto1 = new CharacterDto(
            Id: char1Id,
            AccountId: Guid.NewGuid(),
            SlotIndex: 0,
            RoleNameUnicode: "Quách Tĩnh",
            Gender: CharacterGender.Male,
            Series: ElementSeries.Earth,
            Level: 10,
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
            Strength: 100,
            Dexterity: 100,
            Vitality: 100,
            Energy: 50,
            Lucky: 50,
            UnallocatedAttributePoints: 0,
            UnallocatedSkillPoints: 0,
            InventoryMoney: 1000,
            RepositoryMoney: 0,
            CurrentFactionId: 255,
            FirstFactionId: 255,
            FactionJoinCount: 0,
            Skills:
            [
                new CharacterSkillDto(53, 5, 0),
            ]);

        worker.TryEnqueueCommand(new AttachPlayerZoneCommand(dto1, 1L, sink1));
        worker.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(char1Id, 1L, 0x1111u, 1u));
        await worker.StepSingleTickAsync();

        foreach (SpatialRegionSlot slot in worker.RegionGrid.ActiveRegions)
        {
            foreach (PlayerZoneState p in slot.Players)
            {
                p.FightMode = 1;
                p.WeaponMinDamage = 500;
                p.WeaponMaxDamage = 600;
            }
        }

        NpcZoneState monster = worker.StaticNpcs[1u];
        monster.CurLife = 10;
        monster.TreasureDropCount = 3;
        monster.LootTableCode = "npcdroprate-changgemenareaboss";

        // 1. Slay monster -> verify GroundObjectSlot instances spawn and broadcast 0x64 (SendGroundObjectAdd)
        worker.TryEnqueueCommand(new CastSkillZoneCommand(char1Id, 1L, 53, -1, (int)monster.NpcId));
        await worker.StepSingleTickAsync();

        Assert.True(worker.ActiveGroundObjectCount > 0);
        Assert.NotEmpty(sink1.GroundObjectAdds);

        // 2. Attach second player in same 3x3 region -> verify entering player receives 0x64 for existing ground objects
        var sink2 = new RecordingOutboundSink();
        Guid char2Id = Guid.NewGuid();
        CharacterDto dto2 = dto1 with { Id = char2Id, AccountId = Guid.NewGuid(), RoleNameUnicode = "Hoàng Dung" };
        worker.TryEnqueueCommand(new AttachPlayerZoneCommand(dto2, 2L, sink2));
        worker.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(char2Id, 2L, 0x2222u, 1u));
        await worker.StepSingleTickAsync();

        Assert.Equal(worker.ActiveGroundObjectCount, sink2.GroundObjectAdds.Count);

        // 3. Test 0x4A RequestGroundObjectSyncZoneCommand
        int firstObjId = sink1.GroundObjectAdds[0].ObjId;
        int addsBeforeReq = sink1.GroundObjectAdds.Count;
        worker.TryEnqueueCommand(new RequestGroundObjectSyncZoneCommand(char1Id, 1L, firstObjId));
        await worker.StepSingleTickAsync();
        Assert.Equal(addsBeforeReq + 1, sink1.GroundObjectAdds.Count);

        // 4. Advance ticks past DefaultGroundObjectLifeFrames (1080) or spawn a short-lived object and verify 0x67 expiry
        var shortObj = new GroundObjectSlot(
            objId: 9999,
            objDataId: 1,
            kind: GroundObjectKind.Money,
            displayNameUtf8: "100 Lượng",
            mapId: 53,
            mpsX: 52000,
            mpsY: 101696,
            dirIndex: 0,
            moneyAmount: 100,
            itemPayload: null,
            colorId: 0,
            ownerCharacterId: null,
            ownerProtectionExpiryTick: 0,
            expireTick: worker.CurrentFrameTick + 1);
        worker.GroundObjectSystem.SpawnMoneyGroundObject(
            53,
            null,
            worker.RegionGrid,
            100,
            52000,
            101696,
            null,
            worker.CurrentFrameTick - ItemConsts.DefaultGroundObjectLifeFrames);

        await worker.StepSingleTickAsync();
        Assert.NotEmpty(sink1.GroundObjectRemoves);
        Assert.Contains(sink1.GroundObjectRemoves, static r => r.Reason == 0);
        _ = shortObj;
    }
}
