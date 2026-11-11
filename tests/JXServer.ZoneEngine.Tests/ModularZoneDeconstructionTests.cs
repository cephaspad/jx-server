using System.Reflection;
using JXServer.Contracts.Characters;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Chats;
using JXServer.ZoneEngine.Chats;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.Tests.SimulationHarnesses;
using JXServer.ZoneEngine.WorldZones;
using Xunit;

namespace JXServer.ZoneEngine.Tests;

public sealed class ModularZoneDeconstructionTests
{
    [Fact]
    public void CoreZoneModule_ShouldRegister21CommandHandlers_And6PhasedTickSystems()
    {
        ZoneExtensionRegistry registry = ZoneExtensionRegistry.Create([new CoreZoneModule()]);

        Assert.Equal(21, registry.RegisteredCommandCount);
        Assert.Equal(6, registry.RegisteredTickSystemFactoryCount);

        // Verify all 21 core command types have registered handlers
        Assert.True(registry.HasCommandHandler(typeof(AttachPlayerZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(AttachTransferredPlayerZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(CompletePlayerSyncZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(RequestEntitySyncZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(ProcessPlayerPingZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(DetachPlayerZoneCommand)));

        Assert.True(registry.HasCommandHandler(typeof(MovePlayerZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(JumpPlayerZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(TogglePlayerSitZoneCommand)));

        Assert.True(registry.HasCommandHandler(typeof(CastSkillZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(ChangeAuraSkillZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(ChangePkModeZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(AllocateBaseAttributeZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(AllocateSkillPointZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(RevivePlayerZoneCommand)));

        Assert.True(registry.HasCommandHandler(typeof(RequestGroundObjectSyncZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(PickupGroundObjectZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(MoveInventoryItemZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(UseInventoryItemZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(DropInventoryItemZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(DivideInventoryItemZoneCommand)));

        // Verify tick systems instantiation and phase order in harness
        var harness = new ZoneSimulationHarness();
        MapZoneWorker worker = harness.GetZone(53);
        var context = new ZoneExecutionContext(worker);
        IReadOnlyList<IZoneTickSystem> tickSystems = registry.CreateTickSystems(context);

        Assert.Equal(6, tickSystems.Count);
        Assert.Equal(ZoneTickPhase.StatusEffects, tickSystems[0].Phase);
        Assert.Equal("StatusEffects", tickSystems[0].Name);

        Assert.Equal(ZoneTickPhase.MonsterAi, tickSystems[1].Phase);
        Assert.Equal("MonsterAi", tickSystems[1].Name);

        Assert.Equal(ZoneTickPhase.Missiles, tickSystems[2].Phase);
        Assert.Equal("Missiles", tickSystems[2].Name);

        Assert.Equal(ZoneTickPhase.GroundObjects, tickSystems[3].Phase);
        Assert.Equal("GroundObjects", tickSystems[3].Name);

        Assert.Equal(ZoneTickPhase.MovementAndPosture, tickSystems[4].Phase);
        Assert.Equal("MovementAndPosture", tickSystems[4].Name);

        Assert.Equal(ZoneTickPhase.SessionHeartbeats, tickSystems[5].Phase);
        Assert.Equal("SessionHeartbeats", tickSystems[5].Name);
    }

    [Fact]
    public void ChatZoneModule_ShouldRegister4ChatCommands_AndMapZoneWorkerContainsNoChatHandlers()
    {
        ZoneExtensionRegistry registry = ZoneExtensionRegistry.Create([new ChatZoneModule()]);

        Assert.Equal(4, registry.RegisteredCommandCount);
        Assert.True(registry.HasCommandHandler(typeof(UpdateChatChannelMaskZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(SendNearbyTalkZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(SendStructuredChatZoneCommand)));
        Assert.True(registry.HasCommandHandler(typeof(DeliverClusterChatZoneCommand)));

        // Assert MapZoneWorker type has zero methods containing 'Chat'
        MethodInfo[] workerMethods = typeof(MapZoneWorker).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (MethodInfo method in workerMethods)
        {
            Assert.DoesNotContain("Chat", method.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ChatZoneModule_EndToEndInHarness_NearbyTalkAndStructuredChat()
    {
        var harness = new ZoneSimulationHarness();

        CharacterDto charA = CreateTestCharacter(Guid.NewGuid(), 52000, 101696);
        CharacterDto charB = CreateTestCharacter(Guid.NewGuid(), 52050, 101700);

        SimulatedPlayerHandle handleA = await harness.AttachSimulatedPlayerAsync(charA);
        SimulatedPlayerHandle handleB = await harness.AttachSimulatedPlayerAsync(charB);

        // Player A speaks nearby
        handleA.TalkNearby("Hello World from Module 016!");
        await harness.StepFramesAsync(1);

        // Player B should receive talk broadcast
        var talkEvent = Assert.Single(
            handleB.Recorder.Events,
            static e => e.Domain == OutboundNotificationDomain.Chat && e.MethodName == nameof(IChatOutboundSink.SendPlayerTalkBroadcast));
        Assert.Equal("Hello World from Module 016!", talkEvent.Arguments[1]);

        // Send structured chat on nearby channel
        harness.EnqueueCommand(
            handleA.MapId,
            new SendStructuredChatZoneCommand(
                handleA.CharacterId,
                handleA.LeaseEpoch,
                ChatChannelKind.Nearby,
                TargetNpcId: 0,
                TargetOrPrefixUtf8: string.Empty,
                MessageUtf8: "Structured Nearby Message"));

        await harness.StepFramesAsync(1);

        var chatEvent = Assert.Single(
            handleB.Recorder.Events,
            static e => e.Domain == OutboundNotificationDomain.Chat && e.MethodName == nameof(IChatOutboundSink.SendChatMessage));
        Assert.Equal("Structured Nearby Message", chatEvent.Arguments[2]);
    }

    [Fact]
    public void RuntimeGameAssets_DoesNotContainLegacyReaders()
    {
        Assembly gameAssetsAsm = typeof(GameAssets.WorldMaps.MapCatalog).Assembly;
        string[] legacyTypes =
        [
            "PakFilePathHasher",
            "UclNrv2bDecompressor",
            "XPackArchiveReader",
            "IniFileParser",
            "TabFileParser"
        ];

        foreach (string legacyType in legacyTypes)
        {
            Type? found = gameAssetsAsm.GetType($"JXServer.GameAssets.Archives.{legacyType}")
                ?? gameAssetsAsm.GetType($"JXServer.GameAssets.Configuration.{legacyType}")
                ?? gameAssetsAsm.GetType($"JXServer.GameAssets.{legacyType}");

            Assert.Null(found);
        }
    }

    private static CharacterDto CreateTestCharacter(
        Guid? id = null,
        int x = 52000,
        int y = 101696) =>
        new(
            Id: id ?? Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            SlotIndex: 0,
            RoleNameUnicode: "Tiêu Phong",
            Gender: CharacterGender.Male,
            Series: ElementSeries.Metal,
            Level: 10,
            Experience: 0,
            LeadExperience: 0,
            MapId: 53,
            RevivalMapId: 53,
            RevivalPointId: 19,
            MapMpsX: x,
            MapMpsY: y,
            CurrentLife: 1000,
            MaxLife: 1000,
            CurrentMana: 500,
            MaxMana: 500,
            CurrentStamina: 300,
            MaxStamina: 300,
            Strength: 120,
            Dexterity: 80,
            Vitality: 100,
            Energy: 50,
            Lucky: 10,
            UnallocatedAttributePoints: 0,
            UnallocatedSkillPoints: 0,
            InventoryMoney: 5000,
            RepositoryMoney: 10000,
            CurrentFactionId: 255,
            FirstFactionId: 255,
            FactionJoinCount: 0,
            Skills: [new CharacterSkillDto(1, 1, 0)]);
}
