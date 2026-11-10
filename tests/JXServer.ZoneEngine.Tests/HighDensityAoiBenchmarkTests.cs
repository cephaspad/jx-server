using System.Diagnostics;
using JXServer.Contracts.Characters;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Chats;
using JXServer.ZoneEngine.Chats;
using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JXServer.ZoneEngine.Tests;

public sealed class HighDensityAoiBenchmarkTests
{
    [Fact]
    public async Task HighDensity_50PlayersCrossingRegionBoundariesAndChatting_ShouldMaintainP99TickUnder15Ms()
    {
        var fakeTime = new FakeTimeProvider();
        var mapDesc = new MapDescriptor(53, "Ba Lăng huyện", "balang", 96, 88, 16, 20);
        var worker = new MapZoneWorker(
            mapDesc,
            new SpySessionLeaseAppService(),
            new ZoneEngineOptions(),
            fakeTime,
            NullLogger.Instance);

        var players = new List<(CharacterDto Dto, RecordingOutboundSink Sink)>(50);
        for (int i = 0; i < 50; i++)
        {
            var sink = new RecordingOutboundSink { CountOnly = true };
            var dto = new CharacterDto(
                Id: Guid.NewGuid(),
                AccountId: Guid.NewGuid(),
                SlotIndex: 0,
                RoleNameUnicode: $"CaoThủ{i:D2}",
                Gender: (CharacterGender)(i % 2),
                Series: (ElementSeries)(i % 5),
                Level: 20,
                Experience: 0,
                LeadExperience: 0,
                MapId: 53,
                RevivalMapId: 53,
                RevivalPointId: 19,
                MapMpsX: 51700 + ((i % 10) * 64),
                MapMpsY: 101400 + ((i / 10) * 64),
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

            players.Add((dto, sink));
            Assert.True(worker.TryEnqueueCommand(new AttachPlayerZoneCommand(dto, i + 1, sink)));
            Assert.True(worker.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(dto.Id, i + 1, (uint)(i + 1), 100u)));
        }

        await worker.StepSingleTickAsync();
        Assert.Equal(50, worker.ActivePlayerCount);

        // Warmup JIT for movement and chat paths before measuring p99 tick latency
        worker.TryEnqueueCommand(new MovePlayerZoneCommand(players[0].Dto.Id, 1, 52200, 101696, IsRun: true));
        worker.TryEnqueueCommand(new SendNearbyTalkZoneCommand(players[0].Dto.Id, 1, "Warmup"));
        for (int w = 0; w < 3; w++)
        {
            await worker.StepSingleTickAsync();
        }

        // Issue concurrent movement across region boundary (X=51712 -> X=52800) and chat commands over 180 ticks (10 seconds @ 18 Hz)
        const int totalTicks = 180;
        var tickDurationsMs = new List<double>(totalTicks);
        for (int tick = 0; tick < totalTicks; tick++)
        {
            if (tick % 18 == 0)
            {
                int targetX = (tick % 36 == 0) ? 52800 : 51600;
                for (int i = 0; i < players.Count; i++)
                {
                    worker.TryEnqueueCommand(new MovePlayerZoneCommand(
                        players[i].Dto.Id,
                        i + 1,
                        targetX + (i * 8),
                        101696,
                        IsRun: true));
                }

                worker.TryEnqueueCommand(new SendNearbyTalkZoneCommand(
                    players[0].Dto.Id,
                    1,
                    $"Huynh đệ tập hợp tại Ba Lăng huyện tick {tick}!"));

                worker.TryEnqueueCommand(new SendStructuredChatZoneCommand(
                    players[1].Dto.Id,
                    2,
                    ChatChannelKind.Nearby,
                    0,
                    string.Empty,
                    "Sẵn sàng chiến đấu!"));
            }

            long start = Stopwatch.GetTimestamp();
            await worker.StepSingleTickAsync();
            double elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            tickDurationsMs.Add(elapsedMs);
        }

        tickDurationsMs.Sort();
        int p99Index = (int)Math.Floor(tickDurationsMs.Count * 0.99) - 1;
        double p99Ms = tickDurationsMs[Math.Clamp(p99Index, 0, tickDurationsMs.Count - 1)];

        // SC-001: 99th-percentile tick duration under 15 milliseconds for 50 concurrent players in a zone
        Assert.True(p99Ms < 15.0, $"Expected p99 tick duration < 15ms, actual was {p99Ms:F3}ms.");
    }
}
