using System.Diagnostics;
using JXServer.Contracts.Characters;
using JXServer.Domain.Characters;
using JXServer.Infrastructure.Characters;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Encodings;
using JXServer.Primitives.ErrorCodes;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;
using Xunit;

namespace JXServer.ZoneEngine.Tests.SimulationHarnesses;

[Collection(NonParallelZoneBenchmarkCollection.Name)]
public sealed class ZoneSimulationHarnessAdversarialTests
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
            RoleNameUnicode: "Lệnh Hồ Xung",
            Gender: CharacterGender.Male,
            Series: ElementSeries.Wood,
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
            Strength: 100,
            Dexterity: 100,
            Vitality: 100,
            Energy: 50,
            Lucky: 10,
            UnallocatedAttributePoints: 0,
            UnallocatedSkillPoints: 0,
            InventoryMoney: 0,
            RepositoryMoney: 0,
            CurrentFactionId: 255,
            FirstFactionId: 255,
            FactionJoinCount: 0,
            Skills: [new CharacterSkillDto(1, 1, 0)]);
    }

    private sealed record HealthyTestZoneCommand(Guid CharId, int TargetX, int TargetY) : IZoneCommand
    {
        public Guid? FaultIsolationCharacterId => CharId;
    }

    private sealed class HealthyTestCommandHandler : IZoneCommandHandler<HealthyTestZoneCommand>
    {
        public int ExecutedCount { get; private set; }

        public ValueTask HandleAsync(HealthyTestZoneCommand command, ZoneExecutionContext context, CancellationToken cancellationToken)
        {
            ExecutedCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed record CatastrophicFaultZoneCommand(Guid CharId) : IZoneCommand
    {
        public Guid? FaultIsolationCharacterId => CharId;
    }

    private sealed class CatastrophicFaultCommandHandler : IZoneCommandHandler<CatastrophicFaultZoneCommand>
    {
        public ValueTask HandleAsync(CatastrophicFaultZoneCommand command, ZoneExecutionContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Explosive command handler failure!");
        }
    }

    private sealed record UnregisteredAdversarialZoneCommand(Guid CharId) : IZoneCommand
    {
        public Guid? FaultIsolationCharacterId => CharId;
    }

    private sealed class MultiCommandModule : IZoneModule
    {
        public HealthyTestCommandHandler HealthyHandler { get; } = new();
        public CatastrophicFaultCommandHandler FaultHandler { get; } = new();

        public void Configure(IZoneExtensionBuilder builder)
        {
            builder.AddCommandHandler<HealthyTestZoneCommand>(HealthyHandler);
            builder.AddCommandHandler<CatastrophicFaultZoneCommand>(FaultHandler);
        }
    }

    private sealed class AdversarialFaultingTickSystem(string name, ZoneTickPhase phase, int subOrder) : IZoneTickSystem
    {
        public string Name => name;
        public ZoneTickPhase Phase => phase;
        public int SubOrder => subOrder;

        public ValueTask StepTickAsync(ZoneExecutionContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException($"Fault from {name} in phase {phase}");
        }
    }

    private sealed class MultiFaultingTickSystemModule : IZoneModule
    {
        public void Configure(IZoneExtensionBuilder builder)
        {
            builder.AddTickSystem(new AdversarialFaultingTickSystem("StatusCrash", ZoneTickPhase.StatusEffects, 0));
            builder.AddTickSystem(new AdversarialFaultingTickSystem("AiCrash", ZoneTickPhase.MonsterAi, 10));
            builder.AddTickSystem(new AdversarialFaultingTickSystem("MissileCrash", ZoneTickPhase.Missiles, -5));
        }
    }

    private sealed class DetachTrackingFeatureSlot(int index, bool throwOnDetach) : IPlayerFeatureSlot
    {
        public int Index => index;
        public bool DetachCalled { get; private set; }

        public void OnPlayerDetached(PlayerZoneState player, ZoneExecutionContext context, PlayerDetachReason reason)
        {
            DetachCalled = true;
            if (throwOnDetach)
            {
                throw new InvalidOperationException($"Feature slot {index} blew up on detach");
            }
        }
    }

    [Fact]
    public async Task Benchmark_1000VirtualFrames_ExecutionTime_EmpiricalMeasurement()
    {
        var options = new ZoneSimulationHarnessOptions
        {
            RandomSeed = 1337,
            TicksPerSecond = 18
        };

        var harness = new ZoneSimulationHarness(options);

        // Warm up JIT execution path thoroughly (200 frames)
        await harness.StepFramesAsync(200);

        // Run benchmark across 3 iterations of 1,000 frames
        double[] measurements = new double[3];
        for (int i = 0; i < 3; i++)
        {
            long start = Stopwatch.GetTimestamp();
            await harness.StepFramesAsync(1000);
            measurements[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }

        double minElapsedMs = measurements.Min();
        Assert.Equal(3200, harness.TotalSteppedFrames);
        // The contract specifies 1,000 frames under 5ms
        Assert.True(minElapsedMs < 5.0, $"Expected 1,000 virtual frames to execute under 5.0ms, but min was {minElapsedMs:F3}ms (runs: {string.Join(", ", measurements.Select(m => $"{m:F3}ms"))}).");
    }

    [Fact]
    public async Task SeedDeterminism_FixedSeed_ProducesBitIdenticalFrameSequences()
    {
        const int fixedSeed = 424242;

        async Task<(long TotalFrames, int FinalX, int FinalY, int EventCount)> RunScenarioAsync()
        {
            var options = new ZoneSimulationHarnessOptions
            {
                RandomSeed = fixedSeed,
                TicksPerSecond = 18
            };

            var harness = new ZoneSimulationHarness(options);
            CharacterDto charDto = CreateTestCharacter(x: 52000, y: 101696);
            SimulatedPlayerHandle handle = await harness.AttachSimulatedPlayerAsync(charDto);

            handle.Move(52500, 101800);
            await harness.StepFramesAsync(50);

            handle.TalkNearby("Deterministic sword dance");
            await harness.StepFramesAsync(50);

            PlayerZoneState state = handle.GetZoneState()!;
            return (harness.TotalSteppedFrames, state.CurrentMpsX, state.CurrentMpsY, handle.Recorder.Events.Count);
        }

        var run1 = await RunScenarioAsync();
        var run2 = await RunScenarioAsync();

        Assert.Equal(run1.TotalFrames, run2.TotalFrames);
        Assert.Equal(run1.FinalX, run2.FinalX);
        Assert.Equal(run1.FinalY, run2.FinalY);
        Assert.Equal(run1.EventCount, run2.EventCount);
    }

    [Fact]
    public void SeedDeterminism_SeedDerivationFormula_DifferentSeedsYieldDifferentSeeds()
    {
        const ushort mapId = 53;
        int seedA = 1111;
        int seedB = 9999;

        int zoneSeedA = unchecked((seedA * 397) ^ mapId);
        int zoneSeedB = unchecked((seedB * 397) ^ mapId);

        Assert.NotEqual(zoneSeedA, zoneSeedB);

        var rngA = new Random(zoneSeedA);
        var rngB = new Random(zoneSeedB);

        // Sequence from rngA and rngB must diverge
        int[] seqA = [rngA.Next(), rngA.Next(), rngA.Next()];
        int[] seqB = [rngB.Next(), rngB.Next(), rngB.Next()];
        Assert.False(seqA.SequenceEqual(seqB));
    }

    [Fact]
    public async Task FaultContainment_MultipleFaultingCommands_DoNotAffectOtherCommandsOrZone()
    {
        var module = new MultiCommandModule();
        var options = new ZoneSimulationHarnessOptions();
        options.CustomModules.Add(module);

        var harness = new ZoneSimulationHarness(options);
        CharacterDto charA = CreateTestCharacter(x: 52000, y: 101696);
        CharacterDto charB = CreateTestCharacter(x: 52100, y: 101700);

        SimulatedPlayerHandle handleA = await harness.AttachSimulatedPlayerAsync(charA);
        SimulatedPlayerHandle handleB = await harness.AttachSimulatedPlayerAsync(charB);

        // Enqueue: Healthy1 -> CatastrophicFault1 -> CatastrophicFault2 -> Healthy2
        harness.EnqueueCommand(53, new HealthyTestZoneCommand(charA.Id, 52050, 101696));
        harness.EnqueueCommand(53, new CatastrophicFaultZoneCommand(charA.Id));
        harness.EnqueueCommand(53, new CatastrophicFaultZoneCommand(charB.Id));
        harness.EnqueueCommand(53, new HealthyTestZoneCommand(charB.Id, 52150, 101700));

        // Step 1 frame: faults must be trapped and healthy commands executed
        await harness.StepFramesAsync(1);

        Assert.Equal(2, module.HealthyHandler.ExecutedCount);
        Assert.Equal(3, harness.TotalSteppedFrames);
    }

    [Fact]
    public async Task FaultContainment_UnregisteredCommand_SkipsCleanlyWithoutCrash()
    {
        var harness = new ZoneSimulationHarness();
        CharacterDto charA = CreateTestCharacter();
        await harness.AttachSimulatedPlayerAsync(charA);

        // Enqueue command with no registered handler in registry
        harness.EnqueueCommand(53, new UnregisteredAdversarialZoneCommand(charA.Id));

        // Stepping should log unhandled command warning and continue without throwing
        await harness.StepFramesAsync(2);

        Assert.Equal(3, harness.TotalSteppedFrames);
    }

    [Fact]
    public async Task FaultContainment_MultipleFaultingTickSystemsAcrossPhases_DoNotHaltPipeline()
    {
        var options = new ZoneSimulationHarnessOptions();
        options.CustomModules.Add(new MultiFaultingTickSystemModule());

        var harness = new ZoneSimulationHarness(options);
        CharacterDto charA = CreateTestCharacter();
        SimulatedPlayerHandle handle = await harness.AttachSimulatedPlayerAsync(charA);

        // Step 10 frames with faulting systems in 3 distinct phases
        await harness.StepFramesAsync(10);

        Assert.Equal(11, harness.TotalSteppedFrames);
        PlayerZoneState? state = handle.GetZoneState();
        Assert.NotNull(state);
    }

    [Fact]
    public async Task FaultContainment_FeatureSlotDetachThrows_AllOtherSlotsStillDetached()
    {
        var harness = new ZoneSimulationHarness();
        CharacterDto charDto = CreateTestCharacter();
        SimulatedPlayerHandle handle = await harness.AttachSimulatedPlayerAsync(charDto);

        PlayerZoneState state = handle.GetZoneState()!;
        Assert.NotNull(state);

        var slots = new DetachTrackingFeatureSlot[5];
        for (int i = 0; i < 5; i++)
        {
            int slotIdx = i;
            bool shouldThrow = (slotIdx == 1 || slotIdx == 3);
            var key = new PlayerFeatureSlotKey<DetachTrackingFeatureSlot>($"Slot_{slotIdx}");
            slots[slotIdx] = state.GetOrCreateFeatureSlot(key, () => new DetachTrackingFeatureSlot(slotIdx, shouldThrow));
        }

        // Detach player: slots 1 and 3 will throw inside OnPlayerDetached
        handle.Detach(PlayerDetachReason.CleanLogout);
        await harness.StepFramesAsync(1);

        // Confirm every single slot had OnPlayerDetached called despite intermediate exceptions
        for (int i = 0; i < 5; i++)
        {
            Assert.True(slots[i].DetachCalled, $"Slot {i} should have had OnPlayerDetached called.");
        }
    }

    [Fact]
    public void TaskValues_BoundaryConditions_TaskId0And65535()
    {
        var character = new Character(
            id: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            slotIndex: 0,
            roleName: "Vô Danh",
            gender: CharacterGender.Female,
            series: ElementSeries.Water,
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

        // Initial state
        Assert.Equal(0, character.GetTaskValue(0));
        Assert.Equal(0, character.GetTaskValue(65535));
        Assert.Empty(character.TaskValues);

        // Set taskId = 0 (boundary ushort.MinValue)
        character.SetTaskValue(0, 12345);
        Assert.Equal(12345, character.GetTaskValue(0));
        Assert.Single(character.TaskValues);

        // Set taskId = 65535 (boundary ushort.MaxValue)
        character.SetTaskValue(65535, -99999);
        Assert.Equal(-99999, character.GetTaskValue(65535));
        Assert.Equal(2, character.TaskValues.Count);

        // Sparse zero removal for boundaries
        character.SetTaskValue(0, 0);
        Assert.Equal(0, character.GetTaskValue(0));
        Assert.False(character.TaskValues.ContainsKey(0));

        character.SetTaskValue(65535, 0);
        Assert.Equal(0, character.GetTaskValue(65535));
        Assert.False(character.TaskValues.ContainsKey(65535));
        Assert.Empty(character.TaskValues);

        // Setting 0 on an already-absent key does not create an entry
        character.SetTaskValue(32768, 0);
        Assert.Empty(character.TaskValues);
    }

    [Fact]
    public void TaskValuesJsonSerializer_AdversarialInputs_HandledRobustly()
    {
        // Null dictionary -> "{}"
        Assert.Equal("{}", TaskValuesJsonSerializer.Serialize(null));

        // Empty dictionary -> "{}"
        Assert.Equal("{}", TaskValuesJsonSerializer.Serialize(new Dictionary<ushort, int>()));

        // All zeroes -> "{}"
        var allZeroes = new Dictionary<ushort, int> { [0] = 0, [50] = 0, [65535] = 0 };
        Assert.Equal("{}", TaskValuesJsonSerializer.Serialize(allZeroes));

        // Ascending numeric key serialization order
        var unsorted = new Dictionary<ushort, int>
        {
            [65535] = 999,
            [10] = 50,
            [0] = 1,
            [500] = 200
        };
        string json = TaskValuesJsonSerializer.Serialize(unsorted);
        Assert.Equal("{\"0\":1,\"10\":50,\"500\":200,\"65535\":999}", json);

        // Deserialization of null, whitespace, empty
        Assert.Empty(TaskValuesJsonSerializer.Deserialize(null));
        Assert.Empty(TaskValuesJsonSerializer.Deserialize(""));
        Assert.Empty(TaskValuesJsonSerializer.Deserialize("   "));
        Assert.Empty(TaskValuesJsonSerializer.Deserialize("{}"));

        // Deserialization of invalid malformed JSON -> returns empty without throwing
        Assert.Empty(TaskValuesJsonSerializer.Deserialize("not a json"));
        Assert.Empty(TaskValuesJsonSerializer.Deserialize("{broken: 123"));

        // Deserialization strips 0 values
        var stripped = TaskValuesJsonSerializer.Deserialize("{\"1\":100,\"2\":0,\"3\":300}");
        Assert.Equal(2, stripped.Count);
        Assert.Equal(100, stripped[1]);
        Assert.Equal(300, stripped[3]);
        Assert.False(stripped.ContainsKey(2));

        // ValueComparer equality and hash code consistency
        var dictA = new Dictionary<ushort, int> { [0] = 1, [65535] = 2 };
        var dictB = new Dictionary<ushort, int> { [65535] = 2, [0] = 1 };
        Assert.True(TaskValuesJsonSerializer.Comparer.Equals(dictA, dictB));
        Assert.Equal(
            TaskValuesJsonSerializer.Comparer.GetHashCode(dictA),
            TaskValuesJsonSerializer.Comparer.GetHashCode(dictB));
    }

    [Fact]
    public void RepositoryPasswordHash_ValidationAndNormalization()
    {
        var character = new Character(
            id: Guid.NewGuid(),
            accountId: Guid.NewGuid(),
            slotIndex: 0,
            roleName: "Đông Phương",
            gender: CharacterGender.Female,
            series: ElementSeries.Fire,
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

        // Initial unlocked state
        Assert.Null(character.RepositoryPasswordHash);
        Assert.True(character.VerifyRepositoryPassword("any_password"));

        // Valid 32-char lowercase hex is normalized to uppercase
        string lowercaseHex = "e10adc3949ba59abbe56e057f20f883e";
        character.SetRepositoryPasswordHash(lowercaseHex);
        Assert.Equal(lowercaseHex.ToUpperInvariant(), character.RepositoryPasswordHash);
        Assert.True(character.VerifyRepositoryPassword("123456"));

        // Non-hex characters rejected (e.g. 'G' or 'Z')
        string nonHex = "E10ADC3949BA59ABBE56E057F20F883G";
        Assert.Equal(32, nonHex.Length);
        var exNonHex = Assert.Throws<BusinessException>(() => character.SetRepositoryPasswordHash(nonHex));
        Assert.Equal(JXServerErrorCodes.CharacterInvalidRepositoryCredential, exNonHex.Code);

        // Incorrect lengths (31 chars, 33 chars) rejected
        string hex31 = lowercaseHex[..31];
        var ex31 = Assert.Throws<BusinessException>(() => character.SetRepositoryPasswordHash(hex31));
        Assert.Equal(JXServerErrorCodes.CharacterInvalidRepositoryCredential, ex31.Code);

        string hex33 = lowercaseHex + "A";
        var ex33 = Assert.Throws<BusinessException>(() => character.SetRepositoryPasswordHash(hex33));
        Assert.Equal(JXServerErrorCodes.CharacterInvalidRepositoryCredential, ex33.Code);

        // Set from plaintext password computes uppercase MD5
        character.SetRepositoryPasswordFromPlaintext("ThienDiaHuyenHoang");
        string expectedMd5 = KsgPasswordHasher.HashPlaintext("ThienDiaHuyenHoang");
        Assert.Equal(expectedMd5, character.RepositoryPasswordHash);
        Assert.True(character.VerifyRepositoryPassword("ThienDiaHuyenHoang"));
        Assert.False(character.VerifyRepositoryPassword("WrongPassword"));

        // Clearing with empty/null/whitespace restores unlocked state
        character.SetRepositoryPasswordHash("   ");
        Assert.Null(character.RepositoryPasswordHash);
        Assert.True(character.VerifyRepositoryPassword("anything"));
    }
}
