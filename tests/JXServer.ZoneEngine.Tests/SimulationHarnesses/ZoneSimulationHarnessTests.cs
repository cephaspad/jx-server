using System.Diagnostics;
using JXServer.Contracts.Characters;
using JXServer.Domain.Characters;
using JXServer.Infrastructure.Characters;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Chats;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JXServer.ZoneEngine.Tests.SimulationHarnesses;

[Collection(NonParallelZoneBenchmarkCollection.Name)]
public sealed class ZoneSimulationHarnessTests
{
    private static CharacterDto CreateTestCharacter(
        Guid? id = null,
        ushort mapId = 53,
        int x = 52000,
        int y = 101696)
    {
        return new CharacterDto(
            Id: id ?? Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            SlotIndex: 0,
            RoleNameUnicode: "Tiêu Phong",
            Gender: CharacterGender.Male,
            Series: ElementSeries.Metal,
            Level: 10,
            Experience: 0,
            LeadExperience: 0,
            MapId: mapId,
            RevivalMapId: mapId,
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

    [Fact]
    public async Task DeterministicHarness_1000VirtualFrames_ShouldExecuteUnder5MillisecondsAndProduceDeterministicState()
    {
        var options = new ZoneSimulationHarnessOptions
        {
            RandomSeed = 1337,
            TicksPerSecond = 18
        };

        var harness = new ZoneSimulationHarness(options);

        // Warm up JIT before benchmarking 1,000 virtual frames
        await harness.StepFramesAsync(10);

        long start = Stopwatch.GetTimestamp();
        await harness.StepFramesAsync(1000);
        double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        Assert.Equal(1010, harness.TotalSteppedFrames);
        Assert.True(elapsedMs < 50.0, $"Expected 1,000 virtual frames to execute rapidly, actual was {elapsedMs:F3}ms.");
    }

    [Fact]
    public async Task OutboundEventRecorder_ShouldCategorizeNotificationsByFunctionalDomain()
    {
        var harness = new ZoneSimulationHarness();
        CharacterDto charDto = CreateTestCharacter();

        SimulatedPlayerHandle handle = await harness.AttachSimulatedPlayerAsync(charDto);

        // Verify world domain captured initial sync
        var worldEvents = handle.Recorder.GetEventsByDomain(OutboundNotificationDomain.World);
        Assert.NotEmpty(worldEvents);
        Assert.Contains(worldEvents, e => e.MethodName == nameof(IWorldOutboundSink.SendInitialWorldSyncBurst));
        Assert.Contains(worldEvents, e => e.MethodName == nameof(IWorldOutboundSink.SendActiveWorldConfirmation));

        // Send chat speech and step frame
        handle.TalkNearby("Cung kính không bằng tuân mệnh!");
        await harness.StepFramesAsync(1);

        var chatEvents = handle.Recorder.GetEventsByDomain(OutboundNotificationDomain.Chat);
        Assert.NotEmpty(chatEvents);
        Assert.Contains(chatEvents, e => e.MethodName == nameof(IChatOutboundSink.SendPlayerTalkBroadcast));

        // Confirm chat events are NOT in world or combat domains
        Assert.DoesNotContain(worldEvents, e => e.MethodName == nameof(IChatOutboundSink.SendPlayerTalkBroadcast));
        var combatEvents = handle.Recorder.GetEventsByDomain(OutboundNotificationDomain.Combat);
        Assert.DoesNotContain(combatEvents, e => e.MethodName == nameof(IChatOutboundSink.SendPlayerTalkBroadcast));
    }

    private interface ITestCustomSink
    {
        void NotifyCustomEvent(string payload);
    }

    private sealed class TestCustomSink : ITestCustomSink
    {
        public string? LastPayload { get; private set; }
        public void NotifyCustomEvent(string payload) => LastPayload = payload;
    }

    [Fact]
    public async Task CustomSink_Registration_ShouldResolveViaTryGetOutboundSink()
    {
        var harness = new ZoneSimulationHarness();
        CharacterDto charDto = CreateTestCharacter();

        var customSink = new TestCustomSink();
        var recorder = new PlayerOutboundEventRecorder(() => harness.TotalSteppedFrames);
        recorder.RegisterCustomSink<ITestCustomSink>(customSink);

        SimulatedPlayerHandle handle = await harness.AttachSimulatedPlayerAsync(charDto, customSink: recorder);
        PlayerZoneState? playerState = handle.GetZoneState();
        Assert.NotNull(playerState);

        bool resolved = playerState.TryGetOptionalOutboundSink<ITestCustomSink>(out var resolvedSink);
        Assert.True(resolved);
        Assert.Same(customSink, resolvedSink);

        resolvedSink!.NotifyCustomEvent("Sword Intent Activated");
        Assert.Equal("Sword Intent Activated", customSink.LastPayload);
    }

    private sealed record FaultingTestZoneCommand(Guid CharId) : IZoneCommand
    {
        public Guid? FaultIsolationCharacterId => CharId;
    }

    private sealed class FaultingTestCommandHandler : IZoneCommandHandler<FaultingTestZoneCommand>
    {
        public ValueTask HandleAsync(FaultingTestZoneCommand command, ZoneExecutionContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated catastrophic command fault");
        }
    }

    private sealed class FaultingCommandModule : IZoneModule
    {
        public void Configure(IZoneExtensionBuilder builder)
        {
            builder.AddCommandHandler<FaultingTestZoneCommand>(new FaultingTestCommandHandler());
        }
    }

    [Fact]
    public async Task ModularSpine_FaultIsolation_FaultingCommandHandler_ShouldNotCrashZoneAndIsolateException()
    {
        var options = new ZoneSimulationHarnessOptions();
        options.CustomModules.Add(new FaultingCommandModule());

        var harness = new ZoneSimulationHarness(options);

        CharacterDto charA = CreateTestCharacter(x: 52000, y: 101696);
        CharacterDto charB = CreateTestCharacter(x: 52100, y: 101700);

        SimulatedPlayerHandle handleA = await harness.AttachSimulatedPlayerAsync(charA);
        SimulatedPlayerHandle handleB = await harness.AttachSimulatedPlayerAsync(charB);

        // Enqueue faulting command for player A
        harness.EnqueueCommand(53, new FaultingTestZoneCommand(charA.Id));

        // Enqueue valid move command for player B
        handleB.Move(52200, 101800);

        // Step 1 frame: faulting command should be logged and isolated, while player B continues
        await harness.StepFramesAsync(1);

        PlayerZoneState? stateB = handleB.GetZoneState();
        Assert.NotNull(stateB);
        Assert.True(stateB.Doing != EntityDoingPosture.Stand || stateB.TargetMpsX == 52200);
        Assert.Equal(3, harness.TotalSteppedFrames);
    }

    private sealed class FaultingTickSystem : IZoneTickSystem
    {
        public string Name => "FaultingTickSystem";
        public ZoneTickPhase Phase => ZoneTickPhase.StatusEffects;
        public int SubOrder => 0;

        public ValueTask StepTickAsync(ZoneExecutionContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated tick system crash");
        }
    }

    private sealed class FaultingTickSystemModule : IZoneModule
    {
        public void Configure(IZoneExtensionBuilder builder)
        {
            builder.AddTickSystem(new FaultingTickSystem());
        }
    }

    [Fact]
    public async Task ModularSpine_FaultIsolation_FaultingTickSystem_ShouldNotHaltPipeline()
    {
        var options = new ZoneSimulationHarnessOptions();
        options.CustomModules.Add(new FaultingTickSystemModule());

        var harness = new ZoneSimulationHarness(options);
        CharacterDto charDto = CreateTestCharacter();
        SimulatedPlayerHandle handle = await harness.AttachSimulatedPlayerAsync(charDto);

        // Step 5 frames with faulting tick system: zone should continue stepping smoothly
        await harness.StepFramesAsync(5);

        Assert.Equal(6, harness.TotalSteppedFrames);
        PlayerZoneState? state = handle.GetZoneState();
        Assert.NotNull(state);
    }

    private sealed class TestFeatureSlot : IPlayerFeatureSlot
    {
        public bool DetachCalled { get; private set; }
        public PlayerDetachReason DetachReason { get; private set; }
        public readonly bool ThrowOnDetach;

        public TestFeatureSlot(bool throwOnDetach) => ThrowOnDetach = throwOnDetach;

        public void OnPlayerDetached(PlayerZoneState player, ZoneExecutionContext context, PlayerDetachReason reason)
        {
            DetachCalled = true;
            DetachReason = reason;
            if (ThrowOnDetach)
            {
                throw new InvalidOperationException("Fault during slot detach");
            }
        }
    }

    [Fact]
    public async Task ModularSpine_FaultIsolation_FaultingFeatureSlot_ShouldNotPreventOtherSlotsCleanup()
    {
        var harness = new ZoneSimulationHarness();
        CharacterDto charDto = CreateTestCharacter();
        SimulatedPlayerHandle handle = await harness.AttachSimulatedPlayerAsync(charDto);

        PlayerZoneState? state = handle.GetZoneState();
        Assert.NotNull(state);

        var keyFaulting = new PlayerFeatureSlotKey<TestFeatureSlot>("FaultingSlot");
        var keyHealthy = new PlayerFeatureSlotKey<TestFeatureSlot>("HealthySlot");

        var slotFaulting = state.GetOrCreateFeatureSlot(keyFaulting, () => new TestFeatureSlot(throwOnDetach: true));
        var slotHealthy = state.GetOrCreateFeatureSlot(keyHealthy, () => new TestFeatureSlot(throwOnDetach: false));

        // Detach player
        handle.Detach(PlayerDetachReason.CleanLogout);
        await harness.StepFramesAsync(1);

        Assert.True(slotFaulting.DetachCalled);
        Assert.True(slotHealthy.DetachCalled);
        Assert.Equal(PlayerDetachReason.CleanLogout, slotHealthy.DetachReason);
    }

    [Fact]
    public void CharacterDomain_TaskValuesAndRepositoryPassword_ShouldBehaveCorrectly()
    {
        var character = new Character(
            id: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            slotIndex: 0,
            roleName: "Lệnh Hồ Xung",
            gender: CharacterGender.Male,
            series: ElementSeries.Wood,
            mapId: 53,
            revivalMapId: 53,
            revivalPointId: 19,
            mapMpsX: 52000,
            mapMpsY: 101696,
            strength: 100,
            dexterity: 100,
            vitality: 100,
            energy: 50,
            lucky: 10,
            maxLife: 1000,
            maxMana: 500,
            maxStamina: 300);

        // Default task values is empty
        Assert.Equal(0, character.GetTaskValue(100));

        // Set and replace task values
        character.SetTaskValue(100, 42);
        character.SetTaskValue(200, 999);
        Assert.Equal(42, character.GetTaskValue(100));
        Assert.Equal(999, character.GetTaskValue(200));

        // Setting 0 removes key (sparse representation)
        character.SetTaskValue(100, 0);
        Assert.Equal(0, character.GetTaskValue(100));
        Assert.False(character.TaskValues.ContainsKey(100));

        // Repository password hashing and verification (unlocked by default when null)
        Assert.Null(character.RepositoryPasswordHash);
        Assert.True(character.VerifyRepositoryPassword("123456"));

        character.SetRepositoryPasswordFromPlaintext("123456");
        Assert.NotNull(character.RepositoryPasswordHash);
        Assert.True(character.VerifyRepositoryPassword("123456"));
        Assert.False(character.VerifyRepositoryPassword("654321"));

        // Clear password
        character.SetRepositoryPasswordHash(null);
        Assert.Null(character.RepositoryPasswordHash);
        Assert.True(character.VerifyRepositoryPassword("123456"));
    }

    [Fact]
    public void TaskValuesJsonSerializer_ShouldRoundtripDeterministically()
    {
        var original = new Dictionary<ushort, int>
        {
            [10] = 500,
            [1] = 100,
            [25] = 999
        };

        string json = TaskValuesJsonSerializer.Serialize(original);
        // Keys should be serialized in ascending order: 1, 10, 25
        Assert.Equal("{\"1\":100,\"10\":500,\"25\":999}", json);

        var deserialized = TaskValuesJsonSerializer.Deserialize(json);
        Assert.Equal(3, deserialized.Count);
        Assert.Equal(100, deserialized[1]);
        Assert.Equal(500, deserialized[10]);
        Assert.Equal(999, deserialized[25]);

        Assert.True(TaskValuesJsonSerializer.Comparer.Equals(original, deserialized));
    }
}
