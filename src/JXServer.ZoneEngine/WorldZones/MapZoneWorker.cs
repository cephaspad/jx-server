using System.Threading.Channels;
using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.GameAssets.WorldMaps;
using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.Logging;
using JXServer.ZoneEngine.PlayerSessions;
using Microsoft.Extensions.Logging;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Single-threaded 18-Hz (`55.5ms`) map zone worker processing player attach, sync-end, heartbeat ping, and detach commands.
/// </summary>
public sealed class MapZoneWorker
{
    private readonly MapDescriptor _mapDescriptor;
    private readonly ISessionLeaseAppService _sessionLeaseAppService;
    private readonly ZoneEngineOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly Channel<IZoneCommand> _commandChannel;
    private readonly Dictionary<Guid, PlayerZoneState> _playersByCharacterId = [];

    private uint _nextRuntimeNpcId = 1;
    private long _currentFrameTick;

    public ushort MapId => _mapDescriptor.MapId;

    public string MapNameUnicode => _mapDescriptor.MapNameUnicode;

    public long CurrentFrameTick => Interlocked.Read(ref _currentFrameTick);

    public int ActivePlayerCount => _playersByCharacterId.Count;

    public MapZoneWorker(
        MapDescriptor mapDescriptor,
        ISessionLeaseAppService sessionLeaseAppService,
        ZoneEngineOptions options,
        TimeProvider timeProvider,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(mapDescriptor);
        ArgumentNullException.ThrowIfNull(sessionLeaseAppService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _mapDescriptor = mapDescriptor;
        _sessionLeaseAppService = sessionLeaseAppService;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
        _commandChannel = Channel.CreateUnbounded<IZoneCommand>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Enqueues a command into the zone's lock-free channel.
    /// </summary>
    public bool TryEnqueueCommand(IZoneCommand command) =>
        _commandChannel.Writer.TryWrite(command);

    /// <summary>
    /// Executes a single 18-Hz tick: drains all pending <see cref="IZoneCommand"/> items and runs periodic heartbeat checks.
    /// </summary>
    public async ValueTask StepSingleTickAsync(CancellationToken cancellationToken = default)
    {
        long tick = Interlocked.Increment(ref _currentFrameTick);

        while (_commandChannel.Reader.TryRead(out IZoneCommand? cmd))
        {
            await ProcessCommandAsync(cmd, tick, cancellationToken);
        }

        await ProcessHeartbeatsAsync(tick, cancellationToken);
    }

    /// <summary>
    /// Runs the continuous 18-Hz simulation loop until <paramref name="stoppingToken"/> is canceled.
    /// </summary>
    public async Task RunLoopAsync(CancellationToken stoppingToken)
    {
        int ticksPerSecond = Math.Max(1, _options.TicksPerSecond);
        TimeSpan tickInterval = TimeSpan.FromMilliseconds(1000.0 / ticksPerSecond);
        using var timer = new PeriodicTimer(tickInterval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await StepSingleTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async ValueTask ProcessCommandAsync(IZoneCommand command, long tick, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case AttachPlayerZoneCommand attach:
                await HandleAttachPlayerAsync(attach, tick, cancellationToken);
                break;

            case CompletePlayerSyncZoneCommand syncEnd:
                HandleCompletePlayerSync(syncEnd, tick);
                break;

            case ProcessPlayerPingZoneCommand ping:
                await HandleProcessPlayerPingAsync(ping, tick, cancellationToken);
                break;

            case DetachPlayerZoneCommand detach:
                await HandleDetachPlayerAsync(detach, cancellationToken);
                break;
        }
    }

    private async ValueTask HandleAttachPlayerAsync(
        AttachPlayerZoneCommand cmd,
        long tick,
        CancellationToken cancellationToken)
    {
        CharacterDto dto = cmd.Character;
        if (_playersByCharacterId.TryGetValue(dto.Id, out PlayerZoneState? existing))
        {
            await DetachAndPersistAsync(existing, PlayerDetachReason.LeaseSuperseded, cancellationToken);
        }

        uint runtimeNpcId = _nextRuntimeNpcId++;
        if (runtimeNpcId == 0)
        {
            runtimeNpcId = _nextRuntimeNpcId++;
        }

        var state = new PlayerZoneState(
            runtimeNpcId,
            dto.AccountId,
            dto.Id,
            cmd.LeaseEpoch,
            dto.RoleNameUnicode,
            dto.Gender,
            dto.Series,
            dto.Level,
            dto.Experience,
            dto.LeadExperience,
            _mapDescriptor.MapId,
            dto.MapMpsX,
            dto.MapMpsY,
            dto.CurrentLife,
            dto.MaxLife,
            dto.CurrentMana,
            dto.MaxMana,
            dto.CurrentStamina,
            dto.MaxStamina,
            dto.Strength,
            dto.Dexterity,
            dto.Vitality,
            dto.Energy,
            dto.Lucky,
            dto.UnallocatedAttributePoints,
            dto.UnallocatedSkillPoints,
            dto.InventoryMoney,
            dto.RepositoryMoney,
            dto.CurrentFactionId,
            dto.FirstFactionId,
            dto.FactionJoinCount,
            tick,
            cmd.OutboundSink);

        _playersByCharacterId[dto.Id] = state;

        ZoneEngineLogMessages.LogPlayerAttached(
            _logger,
            state.RoleNameUnicode,
            state.CharacterId,
            state.RuntimeNpcId,
            state.LeaseEpoch,
            state.MapId,
            state.CurrentMpsX,
            state.CurrentMpsY);

        state.OutboundSink.SendInitialWorldSyncBurst(state, _mapDescriptor.MapNameUnicode, (uint)tick, dto.Skills);
    }

    private void HandleCompletePlayerSync(CompletePlayerSyncZoneCommand cmd, long tick)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? state) ||
            state.LeaseEpoch != cmd.LeaseEpoch)
        {
            return;
        }

        state.ClientToken = cmd.ClientToken;
        state.ClientTick = cmd.ClientTick;
        state.IsSyncCompleted = true;
        state.LastPingReceivedTick = tick;
        state.LastPingSentTick = tick;

        state.OutboundSink.SendActiveWorldConfirmation(state);
    }

    private async ValueTask HandleProcessPlayerPingAsync(
        ProcessPlayerPingZoneCommand cmd,
        long tick,
        CancellationToken cancellationToken)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? state) ||
            state.LeaseEpoch != cmd.LeaseEpoch)
        {
            return;
        }

        state.LastPingReceivedTick = tick;
        state.OutboundSink.SendServerPingReply(cmd.ClientTimestamp);
        _ = await _sessionLeaseAppService.RecordHeartbeatAsync(state.AccountId, state.LeaseEpoch, cancellationToken);
    }

    private async ValueTask HandleDetachPlayerAsync(
        DetachPlayerZoneCommand cmd,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? state) &&
                state.LeaseEpoch <= cmd.LeaseEpoch)
            {
                await DetachAndPersistAsync(state, cmd.Reason, cancellationToken);
            }
        }
        finally
        {
            cmd.CompletionSignal?.TrySetResult();
        }
    }

    private async ValueTask ProcessHeartbeatsAsync(long tick, CancellationToken cancellationToken)
    {
        if (_playersByCharacterId.Count == 0)
        {
            return;
        }

        int ticksPerSec = Math.Max(1, _options.TicksPerSecond);
        long pingIntervalTicks = Math.Max(1, _options.HeartbeatIntervalSeconds) * ticksPerSec;
        long timeoutTicks = Math.Max(5, _options.HeartbeatTimeoutSeconds) * ticksPerSec;

        List<PlayerZoneState>? timedOut = null;

        foreach (PlayerZoneState state in _playersByCharacterId.Values)
        {
            if ((tick - state.LastPingReceivedTick) > timeoutTicks)
            {
                timedOut ??= [];
                timedOut.Add(state);
                continue;
            }

            if (state.IsSyncCompleted && (tick - state.LastPingSentTick) >= pingIntervalTicks)
            {
                uint serverTimestamp = (uint)tick;
                state.LastSentPingServerTimestamp = serverTimestamp;
                state.LastPingSentTick = tick;
                state.OutboundSink.SendServerPing(serverTimestamp);
            }
        }

        if (timedOut is not null)
        {
            foreach (PlayerZoneState expired in timedOut)
            {
                await DetachAndPersistAsync(expired, PlayerDetachReason.HeartbeatTimeout, cancellationToken);
            }
        }
    }

    private async ValueTask DetachAndPersistAsync(
        PlayerZoneState state,
        PlayerDetachReason reason,
        CancellationToken cancellationToken)
    {
        _playersByCharacterId.Remove(state.CharacterId);

        ZoneEngineLogMessages.LogPlayerDetached(
            _logger,
            state.RoleNameUnicode,
            state.CharacterId,
            state.RuntimeNpcId,
            state.LeaseEpoch,
            state.MapId,
            reason);

        var snapshot = new UpdateCharacterWorldSnapshotDto(
            state.CharacterId,
            state.MapId,
            state.CurrentMpsX,
            state.CurrentMpsY,
            state.CurrentLife,
            state.CurrentMana,
            state.CurrentStamina);

        await _sessionLeaseAppService.CompleteLogoutOrEvictAsync(
            new ReleaseSessionLeaseInput(state.AccountId, state.LeaseEpoch, snapshot),
            cancellationToken);

        if (reason is PlayerDetachReason.HeartbeatTimeout or PlayerDetachReason.LeaseSuperseded)
        {
            state.OutboundSink.Disconnect(reason);
        }
    }
}
