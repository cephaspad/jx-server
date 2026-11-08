using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JXServer.ZoneEngine.Tests;

/// <summary>
/// Deterministic 18-Hz unit tests for <see cref="MapZoneWorker"/> attach, sync-end, heartbeat ping, and timeout eviction.
/// </summary>
public sealed class MapZoneWorkerTests
{
    [Fact]
    public async Task MapZoneWorker_ShouldExecuteInitialSync_ActiveConfirmation_PingCycle_AndTimeoutEviction()
    {
        var fakeTime = new FakeTimeProvider();
        var mapDesc = new MapDescriptor(53, "Ba Lăng huyện", "balang", 96, 88, 16, 20);
        var leaseSpy = new SpySessionLeaseAppService();
        var options = new ZoneEngineOptions
        {
            TicksPerSecond = 18,
            HeartbeatIntervalSeconds = 5,
            HeartbeatTimeoutSeconds = 15
        };

        var worker = new MapZoneWorker(mapDesc, leaseSpy, options, fakeTime, NullLogger.Instance);
        var sink = new RecordingOutboundSink();

        var charDto = new CharacterDto(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            SlotIndex: 0,
            RoleNameUnicode: "Dương Quá",
            Gender: CharacterGender.Male,
            Series: ElementSeries.Metal,
            Level: 1,
            Experience: 0,
            LeadExperience: 0,
            MapId: 53,
            RevivalMapId: 53,
            RevivalPointId: 19,
            MapMpsX: 52032,
            MapMpsY: 101696,
            CurrentLife: 204,
            MaxLife: 204,
            CurrentMana: 16,
            MaxMana: 16,
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
            Skills: [new CharacterSkillDto(53, 1, 0, 0)]);

        // 1. Enqueue AttachPlayerZoneCommand
        Assert.True(worker.TryEnqueueCommand(new AttachPlayerZoneCommand(charDto, 1, sink)));
        await worker.StepSingleTickAsync();

        Assert.Equal(1, worker.ActivePlayerCount);
        Assert.Equal(1, sink.InitialSyncBurstCount);
        Assert.Equal(0, sink.ActiveConfirmationCount);

        // 2. Enqueue CompletePlayerSyncZoneCommand (0x43 reply)
        Assert.True(worker.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(charDto.Id, 1, 0x11112222u, 0x33334444u)));
        await worker.StepSingleTickAsync();

        Assert.Equal(1, sink.ActiveConfirmationCount);

        // 3. Step 90 ticks (5 seconds @ 18 Hz) -> should emit 0x82 server ping
        for (int i = 0; i < 90; i++)
        {
            await worker.StepSingleTickAsync();
        }

        Assert.NotEmpty(sink.SentPings);
        uint serverPingTs = sink.SentPings[^1];

        // 4. Reply with 0x70 ProcessPlayerPingZoneCommand -> should emit 0x99 reply and record lease heartbeat
        Assert.True(worker.TryEnqueueCommand(new ProcessPlayerPingZoneCommand(charDto.Id, 1, serverPingTs, 0x778899AAu)));
        await worker.StepSingleTickAsync();

        Assert.Single(sink.SentPingReplies);
        Assert.Equal(0x778899AAu, sink.SentPingReplies[0]);
        Assert.Equal(1, leaseSpy.HeartbeatCount);

        // 5. Advance > 270 ticks (15 seconds @ 18 Hz) without replying -> player should be evicted for HeartbeatTimeout
        for (int i = 0; i < 275; i++)
        {
            await worker.StepSingleTickAsync();
        }

        Assert.Equal(0, worker.ActivePlayerCount);
        Assert.Equal(PlayerDetachReason.HeartbeatTimeout, sink.LastDisconnectReason);
        Assert.Equal(1, leaseSpy.ReleaseCount);
    }

    private sealed class RecordingOutboundSink : IPlayerOutboundSink
    {
        public int InitialSyncBurstCount { get; private set; }
        public int ActiveConfirmationCount { get; private set; }
        public List<uint> SentPings { get; } = [];
        public List<uint> SentPingReplies { get; } = [];
        public PlayerDetachReason? LastDisconnectReason { get; private set; }

        public void SendInitialWorldSyncBurst(PlayerZoneState playerState, string mapNameUnicode, uint currentFrameTick, IReadOnlyList<CharacterSkillDto> skills) =>
            InitialSyncBurstCount++;

        public void SendActiveWorldConfirmation(PlayerZoneState playerState) =>
            ActiveConfirmationCount++;

        public void SendServerPing(uint serverTimestamp) =>
            SentPings.Add(serverTimestamp);

        public void SendServerPingReply(uint echoedClientTimestamp) =>
            SentPingReplies.Add(echoedClientTimestamp);

        public void Disconnect(PlayerDetachReason reason) =>
            LastDisconnectReason = reason;
    }

    private sealed class SpySessionLeaseAppService : ISessionLeaseAppService
    {
        public int HeartbeatCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public Task<WorldHandoffTicketDto> SelectCharacterForWorldEntryAsync(SelectCharacterInput input, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RedeemHandoffTicketResultDto> RedeemWorldHandoffTicketAsync(RedeemHandoffTicketInput input, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RecordHeartbeatAsync(Guid accountId, long leaseEpoch, CancellationToken cancellationToken = default)
        {
            HeartbeatCount++;
            return Task.FromResult(true);
        }

        public Task CompleteLogoutOrEvictAsync(ReleaseSessionLeaseInput input, CancellationToken cancellationToken = default)
        {
            ReleaseCount++;
            return Task.CompletedTask;
        }
    }
}
