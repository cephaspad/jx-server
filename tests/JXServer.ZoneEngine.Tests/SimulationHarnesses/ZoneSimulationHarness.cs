using System.Diagnostics.CodeAnalysis;
using JXServer.Contracts.Characters;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.Chats;
using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace JXServer.ZoneEngine.Tests.SimulationHarnesses;

/// <summary>
/// Deterministic in-memory single/multi-zone frame stepper and router (<c>FR-007</c>–<c>FR-009</c>, <c>SC-003</c>, <c>SC-004</c>).
/// Replaces wall-clock delays with a synthetic <see cref="FakeTimeProvider"/> and routes commands across zone shards.
/// </summary>
public sealed class ZoneSimulationHarness : IZoneClusterRouter
{
    private readonly int _ticksPerSecond;
    private readonly TimeSpan _frameInterval;
    private readonly ILogger _logger;
    private readonly ZoneEngineOptions _zoneOptions;
    private readonly ZoneAssetServices _assets;
    private readonly Dictionary<ushort, MapZoneWorker> _workers = [];
    private readonly List<ushort> _sortedMapIds = [];
    private readonly List<ScheduledZoneCommandEntry> _scheduledCommands = [];
    private readonly Dictionary<Guid, SimulatedPlayerHandle> _attachedPlayers = [];
    private long _totalSteppedFrames;

    public FakeTimeProvider TimeProvider { get; }

    public InMemorySessionLeaseAppServiceStub SessionLeaseStub { get; }

    public ZoneExtensionRegistry ExtensionRegistry { get; }

    public long TotalSteppedFrames => Interlocked.Read(ref _totalSteppedFrames);

    public IReadOnlyCollection<MapZoneWorker> Workers => _workers.Values;

    public ZoneSimulationHarness(ZoneSimulationHarnessOptions? options = null)
    {
        ZoneSimulationHarnessOptions opt = options ?? new ZoneSimulationHarnessOptions();
        _ticksPerSecond = Math.Max(1, opt.TicksPerSecond);
        _frameInterval = TimeSpan.FromSeconds(1.0 / _ticksPerSecond);

        TimeProvider = new FakeTimeProvider(opt.InitialUtcTimestamp);
        SessionLeaseStub = new InMemorySessionLeaseAppServiceStub();

        ILoggerFactory loggerFactory = opt.LoggerFactory ?? NullLoggerFactory.Instance;
        _logger = loggerFactory.CreateLogger<ZoneSimulationHarness>();

        _zoneOptions = new ZoneEngineOptions
        {
            TicksPerSecond = _ticksPerSecond,
            HeartbeatIntervalSeconds = opt.HeartbeatIntervalSeconds,
            HeartbeatTimeoutSeconds = opt.HeartbeatTimeoutSeconds,
            RandomSeed = opt.RandomSeed
        };

        ExtensionRegistry = ZoneExtensionRegistry.CreateDefault(opt.CustomModules);

        _assets = opt.Assets ?? new ZoneAssetServices
        {
            ClusterRouter = this
        };

        if (opt.InitialMaps.Count > 0)
        {
            for (int i = 0; i < opt.InitialMaps.Count; i++)
            {
                AddMapZone(opt.InitialMaps[i]);
            }
        }
        else
        {
            // Default village map (Map 53 Ba Lăng Huyện)
            AddMapZone(new MapDescriptor(53, "Ba Lăng Huyện", "balanghuyen", 96, 88, 16, 20));
        }
    }

    public MapZoneWorker AddMapZone(MapDescriptor mapDescriptor)
    {
        ArgumentNullException.ThrowIfNull(mapDescriptor);

        if (_workers.TryGetValue(mapDescriptor.MapId, out var existing))
        {
            return existing;
        }

        var worker = new MapZoneWorker(
            mapDescriptor,
            SessionLeaseStub,
            _zoneOptions,
            TimeProvider,
            _logger,
            extensionRegistry: ExtensionRegistry,
            assetServices: _assets);

        _workers[mapDescriptor.MapId] = worker;
        _sortedMapIds.Add(mapDescriptor.MapId);
        _sortedMapIds.Sort();

        return worker;
    }

    public MapZoneWorker GetZone(ushort mapId)
    {
        if (_workers.TryGetValue(mapId, out var worker))
        {
            return worker;
        }

        throw new KeyNotFoundException($"Map {mapId} is not loaded in harness.");
    }

    public bool TryGetZone(ushort mapId, [NotNullWhen(true)] out MapZoneWorker? worker) =>
        _workers.TryGetValue(mapId, out worker);

    public async ValueTask<SimulatedPlayerHandle> AttachSimulatedPlayerAsync(
        CharacterDto character,
        long leaseEpoch = 1,
        IPlayerOutboundSink? customSink = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(character);

        ushort mapId = character.MapId;
        if (!TryGetZone(mapId, out var worker))
        {
            throw new InvalidOperationException($"Map {mapId} is not registered in harness.");
        }

        var recorder = customSink as PlayerOutboundEventRecorder ?? new PlayerOutboundEventRecorder(() => TotalSteppedFrames);
        if (customSink is not null && customSink != recorder)
        {
            recorder.RegisterCustomSink(customSink);
        }

        worker.TryEnqueueCommand(new AttachPlayerZoneCommand(character, leaseEpoch, recorder));
        worker.TryEnqueueCommand(new CompletePlayerSyncZoneCommand(character.Id, leaseEpoch, 0x1111u, 1u));

        // Step 1 frame to let the worker process attach and sync end commands
        await StepFramesAsync(1, cancellationToken);

        var handle = new SimulatedPlayerHandle(character, recorder, leaseEpoch, mapId, this);
        _attachedPlayers[character.Id] = handle;
        return handle;
    }

    public void EnqueueCommand(ushort mapId, IZoneCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (TryGetZone(mapId, out var worker))
        {
            worker.TryEnqueueCommand(command);
        }
        else
        {
            throw new KeyNotFoundException($"Cannot enqueue command: Map {mapId} not loaded.");
        }
    }

    public void ScheduleCommandAfterFrames(ushort mapId, int delayFrames, IZoneCommand command)
    {
        long targetFrame = TotalSteppedFrames + Math.Max(1, delayFrames);
        ScheduleCommandAtFrame(mapId, targetFrame, command);
    }

    public void ScheduleCommandAtFrame(ushort mapId, long executeAtFrame, IZoneCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _scheduledCommands.Add(new ScheduledZoneCommandEntry(executeAtFrame, mapId, command));
    }

    public async ValueTask StepFramesAsync(int frameCount = 1, CancellationToken cancellationToken = default)
    {
        if (frameCount <= 0)
        {
            return;
        }

        for (int f = 0; f < frameCount; f++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TimeProvider.Advance(_frameInterval);
            long currentFrame = Interlocked.Increment(ref _totalSteppedFrames);

            // Dispatch any scheduled commands whose execution frame has arrived
            if (_scheduledCommands.Count > 0)
            {
                for (int i = _scheduledCommands.Count - 1; i >= 0; i--)
                {
                    ScheduledZoneCommandEntry entry = _scheduledCommands[i];
                    if (entry.ExecuteAtFrame <= currentFrame)
                    {
                        if (TryGetZone(entry.MapId, out var targetZone))
                        {
                            targetZone.TryEnqueueCommand(entry.Command);
                        }
                        _scheduledCommands.RemoveAt(i);
                    }
                }
            }

            // Step each active zone in ascending MapId order
            for (int i = 0; i < _sortedMapIds.Count; i++)
            {
                ushort mapId = _sortedMapIds[i];
                MapZoneWorker worker = _workers[mapId];
                await worker.StepSingleTickAsync(cancellationToken);
            }
        }
    }

    #region IZoneClusterRouter
    public bool IsMapLoaded(ushort mapId) => _workers.ContainsKey(mapId);

    public bool TryGetWorker(ushort mapId, [NotNullWhen(true)] out MapZoneWorker? worker) =>
        _workers.TryGetValue(mapId, out worker);

    public bool TryDispatchToZone(ushort mapId, IZoneCommand command)
    {
        if (_workers.TryGetValue(mapId, out var worker))
        {
            return worker.TryEnqueueCommand(command);
        }

        return false;
    }

    public void RouteClusterChat(DeliverClusterChatZoneCommand command)
    {
        foreach (var worker in _workers.Values)
        {
            worker.TryEnqueueCommand(command);
        }
    }

    public bool TryTransferPlayerAcrossMaps(
        PlayerZoneState player,
        ushort targetMapId,
        int arrivalMpsX,
        int arrivalMpsY,
        byte arrivalFightMode)
    {
        if (_workers.TryGetValue(targetMapId, out var targetWorker))
        {
            bool dispatched = targetWorker.TryEnqueueCommand(new AttachTransferredPlayerZoneCommand(
                player,
                arrivalMpsX,
                arrivalMpsY,
                arrivalFightMode));

            if (dispatched && _attachedPlayers.TryGetValue(player.CharacterId, out var handle))
            {
                handle.MapId = targetMapId;
            }

            return dispatched;
        }

        return false;
    }

    public async Task EvictAccountFromAllZonesAsync(
        Guid accountId,
        long supersededEpoch,
        Guid? activeCharacterId = null,
        ushort? activeMapId = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var worker in _workers.Values)
        {
            foreach (var kvp in worker.PlayersByCharacterId)
            {
                if (kvp.Value.AccountId == accountId && kvp.Value.LeaseEpoch <= supersededEpoch)
                {
                    await worker.DetachPlayerAsync(kvp.Key, PlayerDetachReason.LeaseSuperseded, persistSnapshot: true, sendDisconnect: true, cancellationToken);
                }
            }
        }
    }
    #endregion
}
