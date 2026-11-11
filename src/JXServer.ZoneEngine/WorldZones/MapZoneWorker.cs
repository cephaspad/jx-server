using System.Text;
using System.Threading.Channels;
using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.GameAssets.Items;
using JXServer.GameAssets.LootTables;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.Skills;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Chats;
using JXServer.Primitives.Combat;
using JXServer.Primitives.Items;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.Chats;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.Logging;
using JXServer.ZoneEngine.NpcEntities;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.SpatialGrid;
using Microsoft.Extensions.Logging;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Single-threaded 18-Hz (`55.5ms`) map zone worker managing spatial <c>3x3</c> region AOI, static NPCs,
/// obstacle-validated movement, Five-Elements combat &amp; 64-direction projectiles, portal trap transitions,
/// multi-channel chat, and session heartbeats.
/// </summary>
public sealed class MapZoneWorker
{
    private readonly MapDescriptor _mapDescriptor;
    private readonly ISessionLeaseAppService _sessionLeaseAppService;
    private readonly ZoneEngineOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly IMapCatalog? _mapCatalog;
    private readonly INpcTemplateCatalog? _npcTemplateCatalog;
    private readonly ITrapScriptCatalog? _trapScriptCatalog;
    private readonly ICharacterAppService? _characterAppService;
    private readonly IZoneClusterRouter? _clusterRouter;
    private readonly ISkillTemplateCatalog? _skillTemplateCatalog;
    private readonly IMissileTemplateCatalog? _missileTemplateCatalog;
    private readonly IPlayerTemplateCatalog? _playerTemplateCatalog;
    private readonly IItemTemplateCatalog? _itemTemplateCatalog;
    private readonly ILootTableCatalog? _lootTableCatalog;
    private readonly ItemGenerator? _itemGenerator;
    private readonly Channel<IZoneCommand> _commandChannel;
    private readonly SpatialRegionGrid _regionGrid = new();
    private readonly SpatialRegionSlot[] _aoiSlotBuffer = new SpatialRegionSlot[9];
    private readonly SpatialRegionSlot[] _enteringSlotBuffer = new SpatialRegionSlot[9];
    private readonly List<PlayerZoneState> _tickPlayerBuffer = new(128);
    private readonly Dictionary<Guid, PlayerZoneState> _playersByCharacterId = [];
    private readonly Dictionary<uint, PlayerZoneState> _playersByNpcId = [];
    private readonly Dictionary<uint, NpcZoneState> _npcsById = [];
    private readonly MissileSimulationTickSystem _missileSystem = new();
    private readonly MonsterAiTickSystem _monsterAiSystem = new();
    private readonly GroundObjectTickSystem _groundObjectSystem = new();
    private readonly Random _random = new();

    private uint _nextStaticNpcId = 1;
    private uint _nextRuntimePlayerNpcId = 10000;
    private long _currentFrameTick;

    public ushort MapId => _mapDescriptor.MapId;

    public string MapNameUnicode => _mapDescriptor.MapNameUnicode;

    public long CurrentFrameTick => Interlocked.Read(ref _currentFrameTick);

    public int ActivePlayerCount => _playersByCharacterId.Count;

    public int ActiveNpcCount => _npcsById.Count;

    public int ActiveMissileCount => _missileSystem.ActiveMissileCount;

    public int ActiveGroundObjectCount => _groundObjectSystem.ActiveGroundObjectCount;

    public GroundObjectTickSystem GroundObjectSystem => _groundObjectSystem;

    public SpatialRegionGrid RegionGrid => _regionGrid;

    public IReadOnlyDictionary<uint, NpcZoneState> StaticNpcs => _npcsById;

    public MapZoneWorker(
        MapDescriptor mapDescriptor,
        ISessionLeaseAppService sessionLeaseAppService,
        ZoneEngineOptions options,
        TimeProvider timeProvider,
        ILogger logger,
        IMapCatalog? mapCatalog = null,
        INpcTemplateCatalog? npcTemplateCatalog = null,
        ITrapScriptCatalog? trapScriptCatalog = null,
        ICharacterAppService? characterAppService = null,
        IZoneClusterRouter? clusterRouter = null,
        ISkillTemplateCatalog? skillTemplateCatalog = null,
        IMissileTemplateCatalog? missileTemplateCatalog = null,
        IPlayerTemplateCatalog? playerTemplateCatalog = null,
        IItemTemplateCatalog? itemTemplateCatalog = null,
        ILootTableCatalog? lootTableCatalog = null,
        ItemGenerator? itemGenerator = null)
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
        _mapCatalog = mapCatalog;
        _npcTemplateCatalog = npcTemplateCatalog;
        _trapScriptCatalog = trapScriptCatalog;
        _characterAppService = characterAppService;
        _clusterRouter = clusterRouter;
        _skillTemplateCatalog = skillTemplateCatalog;
        _missileTemplateCatalog = missileTemplateCatalog;
        _playerTemplateCatalog = playerTemplateCatalog;
        _itemTemplateCatalog = itemTemplateCatalog;
        _lootTableCatalog = lootTableCatalog;
        _itemGenerator = itemGenerator ?? (itemTemplateCatalog is not null ? new ItemGenerator(itemTemplateCatalog) : null);
        _commandChannel = Channel.CreateUnbounded<IZoneCommand>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        SpawnInitialStaticNpcs();
    }

    /// <summary>
    /// Enqueues a command into the zone's lock-free channel.
    /// </summary>
    public bool TryEnqueueCommand(IZoneCommand command) =>
        _commandChannel.Writer.TryWrite(command);

    /// <summary>
    /// Executes a single 18-Hz tick: drains all pending <see cref="IZoneCommand"/> items, steps player movement/postures,
    /// and runs periodic heartbeat checks.
    /// </summary>
    public async ValueTask StepSingleTickAsync(CancellationToken cancellationToken = default)
    {
        long tick = Interlocked.Increment(ref _currentFrameTick);

        while (_commandChannel.Reader.TryRead(out IZoneCommand? cmd))
        {
            await ProcessCommandAsync(cmd, tick, cancellationToken);
        }

        ProcessStatusEffectsTick();
        _monsterAiSystem.StepTick(
            tick,
            _mapDescriptor.MapId,
            _mapCatalog,
            _skillTemplateCatalog,
            _missileTemplateCatalog,
            _missileSystem,
            _regionGrid,
            _playersByNpcId,
            _npcsById,
            OnNpcRespawned,
            BroadcastNpcCompactSyncToAoi,
            OnNpcCastSkill,
            ApplyDamageToPlayer);
        _missileSystem.StepTick(
            _mapDescriptor.MapId,
            _mapCatalog,
            _regionGrid,
            _playersByNpcId,
            _npcsById,
            OnMissileHitPlayer,
            OnMissileHitNpc);
        _groundObjectSystem.StepTick(
            tick,
            _regionGrid,
            _playersByCharacterId,
            BroadcastGroundObjectRemoveToAoi);
        await ProcessMovementAndPostureTickAsync(tick, cancellationToken);
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

    private void SpawnInitialStaticNpcs()
    {
        IReadOnlyList<StaticNpcSpawnTemplate> spawns = _mapDescriptor.RegionNpcSpawns;
        if (spawns.Count == 0 && _mapCatalog is not null)
        {
            spawns = _mapCatalog.GetStaticNpcSpawnsForMap(_mapDescriptor.MapId);
        }

        if (spawns.Count == 0)
        {
            return;
        }

        foreach (StaticNpcSpawnTemplate spawn in spawns)
        {
            if (_nextStaticNpcId >= 10000u)
            {
                break;
            }

            uint npcId = _nextStaticNpcId++;
            NpcTemplate? tpl = null;
            _npcTemplateCatalog?.TryGetTemplate(spawn.TemplateId, out tpl);

            int walkSpeed = tpl?.WalkSpeed ?? 10;
            int runSpeed = tpl?.RunSpeed ?? 20;
            uint maxLife = tpl?.CombatScaling is not null
                ? (uint)tpl.CombatScaling.EvaluateMaxLife(tpl.BaseLifeMax, spawn.Level)
                : (uint)Math.Max(100, (tpl?.BaseLifeMax ?? 500) * Math.Max((int)spawn.Level, 1));

            var npc = new NpcZoneState(
                npcId: npcId,
                templateId: spawn.TemplateId,
                name: spawn.DisplayName,
                level: spawn.Level,
                kind: spawn.Kind,
                camp: spawn.Camp,
                series: spawn.Series,
                isSpecialNpc: spawn.IsSpecialNpc,
                mapId: _mapDescriptor.MapId,
                mpsX: spawn.SpawnMpsX,
                mpsY: spawn.SpawnMpsY,
                walkSpeed: walkSpeed,
                runSpeed: runSpeed,
                maxLife: maxLife,
                scriptPath: spawn.ScriptPath,
                template: tpl);

            _npcsById[npcId] = npc;
            _regionGrid.AddNpc(npc);
        }

        ZoneEngineLogMessages.LogStaticNpcsSpawned(
            _logger,
            _npcsById.Count,
            _mapDescriptor.MapId,
            _mapDescriptor.MapNameUnicode);
    }

    private async ValueTask ProcessCommandAsync(IZoneCommand command, long tick, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case AttachPlayerZoneCommand attach:
                await HandleAttachPlayerAsync(attach, tick, cancellationToken);
                break;

            case AttachTransferredPlayerZoneCommand transfer:
                HandleAttachTransferredPlayer(transfer, tick);
                break;

            case CompletePlayerSyncZoneCommand syncEnd:
                HandleCompletePlayerSync(syncEnd, tick);
                break;

            case RequestEntitySyncZoneCommand requestEntity:
                HandleRequestEntitySync(requestEntity);
                break;

            case MovePlayerZoneCommand move:
                await HandleMovePlayerAsync(move, tick, cancellationToken);
                break;

            case JumpPlayerZoneCommand jump:
                await HandleJumpPlayerAsync(jump, tick, cancellationToken);
                break;

            case TogglePlayerSitZoneCommand sit:
                HandleTogglePlayerSit(sit, tick);
                break;

            case UpdateChatChannelMaskZoneCommand chatMask:
                HandleUpdateChatChannelMask(chatMask);
                break;

            case SendNearbyTalkZoneCommand talk:
                HandleSendNearbyTalk(talk);
                break;

            case SendStructuredChatZoneCommand chat:
                HandleSendStructuredChat(chat);
                break;

            case DeliverClusterChatZoneCommand clusterChat:
                HandleDeliverClusterChat(clusterChat);
                break;

            case ProcessPlayerPingZoneCommand ping:
                await HandleProcessPlayerPingAsync(ping, tick, cancellationToken);
                break;

            case CastSkillZoneCommand castSkill:
                HandleCastSkill(castSkill, tick);
                break;

            case ChangeAuraSkillZoneCommand changeAura:
                HandleChangeAuraSkill(changeAura, tick);
                break;

            case ChangePkModeZoneCommand changePk:
                HandleChangePkMode(changePk, tick);
                break;

            case AllocateBaseAttributeZoneCommand allocateAttr:
                HandleAllocateBaseAttribute(allocateAttr, tick);
                break;

            case AllocateSkillPointZoneCommand allocateSkill:
                HandleAllocateSkillPoint(allocateSkill, tick);
                break;

            case RevivePlayerZoneCommand revivePlayer:
                await HandleRevivePlayerAsync(revivePlayer, tick, cancellationToken);
                break;

            case RequestGroundObjectSyncZoneCommand reqObj:
                HandleRequestGroundObjectSync(reqObj, tick);
                break;

            case PickupGroundObjectZoneCommand pickupObj:
                HandlePickupGroundObject(pickupObj, tick);
                break;

            case MoveInventoryItemZoneCommand moveItem:
                HandleMoveInventoryItem(moveItem, tick);
                break;

            case UseInventoryItemZoneCommand useItem:
                await HandleUseInventoryItemAsync(useItem, tick, cancellationToken);
                break;

            case DropInventoryItemZoneCommand dropItem:
                HandleDropInventoryItem(dropItem, tick);
                break;

            case DivideInventoryItemZoneCommand divideItem:
                HandleDivideInventoryItem(divideItem, tick);
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

        uint runtimeNpcId = AllocatePlayerNpcId();

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

        // Initialize LastTriggeredTrapId to the spawn cell's trap (if any) so spawning on a trap doesn't immediately bounce
        state.LastTriggeredTrapId = GetCellTrapId(state.CurrentMpsX, state.CurrentMpsY);
        state.PkState = dto.PkState;
        state.PkValue = dto.PkValue;
        state.RevivalMapId = dto.RevivalMapId;
        state.RevivalPointId = dto.RevivalPointId;

        state.SetLearnedSkill(1, 1);
        state.SetLearnedSkill(53, 1);
        foreach (CharacterSkillDto skillDto in dto.Skills)
        {
            state.SetLearnedSkill(skillDto.SkillId, skillDto.BaseLevel);
        }

        if (dto.Items is not null)
        {
            foreach (CharacterItemDto itemDto in dto.Items)
            {
                PlayerItemSlot slot = CreatePlayerItemSlotFromDto(state.Inventory.AllocateRuntimeItemId(), itemDto);
                _ = state.Inventory.TryAddOrAutoPlaceItem(
                    slot,
                    itemDto.ContainerPlace,
                    itemDto.GridX,
                    itemDto.GridY,
                    state);
            }
        }

        state.Inventory.RecalculateEquipmentAndFiveElementsActivation(state);

        _playersByCharacterId[dto.Id] = state;
        _playersByNpcId[state.RuntimeNpcId] = state;

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

    private void HandleAttachTransferredPlayer(AttachTransferredPlayerZoneCommand cmd, long tick)
    {
        PlayerZoneState state = cmd.Player;
        if (_playersByCharacterId.TryGetValue(state.CharacterId, out PlayerZoneState? existing) &&
            !ReferenceEquals(existing, state))
        {
            RemovePlayerFromZoneMemory(existing);
        }

        if (_playersByNpcId.ContainsKey(state.RuntimeNpcId))
        {
            state.RuntimeNpcId = AllocatePlayerNpcId();
        }

        state.MapId = _mapDescriptor.MapId;
        state.CurrentMpsX = cmd.ArrivalMpsX;
        state.CurrentMpsY = cmd.ArrivalMpsY;
        state.TargetMpsX = cmd.ArrivalMpsX;
        state.TargetMpsY = cmd.ArrivalMpsY;
        state.Doing = EntityDoingPosture.Stand;
        state.FightMode = cmd.ArrivalFightMode;
        state.LastTriggeredTrapId = GetCellTrapId(cmd.ArrivalMpsX, cmd.ArrivalMpsY);
        state.LastPingReceivedTick = tick;

        _playersByCharacterId[state.CharacterId] = state;
        _playersByNpcId[state.RuntimeNpcId] = state;
        _regionGrid.AddPlayer(state, state.RegionX, state.RegionY);

        // Same-server map transition sends 0x49 (s2c_syncworld) + 0x4E (s2c_syncnpcminplayer) WITHOUT 0x43 (s2c_syncend)
        state.OutboundSink.SendWorldMapTransitionSync(state, _mapDescriptor.MapNameUnicode, (uint)tick);
        SynchronizeFullAoiForEnteringPlayer(state);
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

        _regionGrid.AddPlayer(state, state.RegionX, state.RegionY);

        state.OutboundSink.SendActiveWorldConfirmation(state);
        state.OutboundSink.SendMoneySync(state.InventoryMoney, state.RepositoryMoney);
        foreach (PlayerItemSlot item in state.Inventory.AllItems)
        {
            PlayerItemSyncSnapshot itemSnap = PlayerItemSyncSnapshot.CreateFromSlot(item);
            state.OutboundSink.SendItemSync(in itemSnap);
        }

        SynchronizeFullAoiForEnteringPlayer(state);
    }

    private void HandleRequestEntitySync(RequestEntitySyncZoneCommand cmd)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.RequesterCharacterId, out PlayerZoneState? requester) ||
            requester.LeaseEpoch != cmd.LeaseEpoch)
        {
            return;
        }

        if (_playersByNpcId.TryGetValue(cmd.TargetNpcId, out PlayerZoneState? targetPlayer))
        {
            EntityFullSyncSnapshot playerSnap = targetPlayer.CreateFullSyncSnapshot();
            requester.OutboundSink.SendNpcFullSync(in playerSnap);
            return;
        }

        if (_npcsById.TryGetValue(cmd.TargetNpcId, out NpcZoneState? targetNpc))
        {
            EntityFullSyncSnapshot npcSnap = CreateNpcSnapshot(targetNpc);
            requester.OutboundSink.SendNpcFullSync(in npcSnap);
        }
    }

    private async ValueTask HandleMovePlayerAsync(
        MovePlayerZoneCommand cmd,
        long tick,
        CancellationToken cancellationToken)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? state) ||
            state.LeaseEpoch != cmd.LeaseEpoch ||
            !state.IsSyncCompleted)
        {
            return;
        }

        state.LastPingReceivedTick = tick;
        int dx = cmd.TargetMpsX - state.CurrentMpsX;
        int dy = cmd.TargetMpsY - state.CurrentMpsY;
        if (dx == 0 && dy == 0)
        {
            state.Doing = EntityDoingPosture.Stand;
            state.OutboundSink.SendSelfPositionSync(state);
            return;
        }

        // Check whether the immediate first step is into a full obstacle
        int stepSpeed = cmd.IsRun ? state.RunSpeed : state.WalkSpeed;
        (int firstStepX, int firstStepY) = ComputeNextStep(
            state.CurrentMpsX,
            state.CurrentMpsY,
            cmd.TargetMpsX,
            cmd.TargetMpsY,
            stepSpeed);

        if (IsCellBlocked(firstStepX, firstStepY))
        {
            state.TargetMpsX = state.CurrentMpsX;
            state.TargetMpsY = state.CurrentMpsY;
            state.Doing = EntityDoingPosture.Stand;
            state.OutboundSink.SendSelfPositionSync(state);
            return;
        }

        state.TargetMpsX = cmd.TargetMpsX;
        state.TargetMpsY = cmd.TargetMpsY;
        state.Doing = cmd.IsRun ? EntityDoingPosture.Run : EntityDoingPosture.Walk;
        state.Dir = ComputeDirection64(dx, dy);

        // Immediately broadcast 0x50 (Walk) or 0x51 (Run) to the 3x3 region AOI
        SpatialRegionSlot[] buffer = new SpatialRegionSlot[9];
        int regionCount = _regionGrid.GetSurroundingRegions(state.RegionX, state.RegionY, buffer);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = buffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                PlayerZoneState observer = players[pIdx];
                if (cmd.IsRun)
                {
                    observer.OutboundSink.SendRunBroadcast(state.RuntimeNpcId, cmd.TargetMpsX, cmd.TargetMpsY);
                }
                else
                {
                    observer.OutboundSink.SendWalkBroadcast(state.RuntimeNpcId, cmd.TargetMpsX, cmd.TargetMpsY);
                }
            }
        }

        await ValueTask.CompletedTask;
        _ = cancellationToken;
    }

    private async ValueTask HandleJumpPlayerAsync(
        JumpPlayerZoneCommand cmd,
        long tick,
        CancellationToken cancellationToken)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? state) ||
            state.LeaseEpoch != cmd.LeaseEpoch ||
            !state.IsSyncCompleted)
        {
            return;
        }

        state.LastPingReceivedTick = tick;
        if (IsCellBlocked(cmd.TargetMpsX, cmd.TargetMpsY))
        {
            state.Doing = EntityDoingPosture.Stand;
            state.OutboundSink.SendSelfPositionSync(state);
            return;
        }

        int dx = cmd.TargetMpsX - state.CurrentMpsX;
        int dy = cmd.TargetMpsY - state.CurrentMpsY;
        state.Dir = ComputeDirection64(dx, dy);

        MovePlayerCoordinateAndUpdateRegion(state, cmd.TargetMpsX, cmd.TargetMpsY);
        state.TargetMpsX = cmd.TargetMpsX;
        state.TargetMpsY = cmd.TargetMpsY;
        state.Doing = EntityDoingPosture.Stand;

        SpatialRegionSlot[] buffer = new SpatialRegionSlot[9];
        int regionCount = _regionGrid.GetSurroundingRegions(state.RegionX, state.RegionY, buffer);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = buffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                players[pIdx].OutboundSink.SendJumpBroadcast(state.RuntimeNpcId, cmd.TargetMpsX, cmd.TargetMpsY);
            }
        }

        state.OutboundSink.SendSelfPositionSync(state);
        await EvaluatePlayerTrapAsync(state, cancellationToken);
    }

    private void HandleTogglePlayerSit(TogglePlayerSitZoneCommand cmd, long tick)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? state) ||
            state.LeaseEpoch != cmd.LeaseEpoch ||
            !state.IsSyncCompleted)
        {
            return;
        }

        state.LastPingReceivedTick = tick;
        state.TargetMpsX = state.CurrentMpsX;
        state.TargetMpsY = state.CurrentMpsY;

        if (cmd.IsSitting)
        {
            state.Doing = EntityDoingPosture.Sit;
            state.LastSitRegenTick = tick;

            SpatialRegionSlot[] buffer = new SpatialRegionSlot[9];
            int regionCount = _regionGrid.GetSurroundingRegions(state.RegionX, state.RegionY, buffer);
            for (int i = 0; i < regionCount; i++)
            {
                List<PlayerZoneState> players = buffer[i].Players;
                for (int pIdx = 0; pIdx < players.Count; pIdx++)
                {
                    players[pIdx].OutboundSink.SendSitBroadcast(state.RuntimeNpcId);
                }
            }
        }
        else
        {
            state.Doing = EntityDoingPosture.Stand;
            state.OutboundSink.SendSelfPositionSync(state);
            BroadcastCompactSyncToNeighbors(state);
        }
    }

    private void HandleUpdateChatChannelMask(UpdateChatChannelMaskZoneCommand cmd)
    {
        if (_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? state) &&
            state.LeaseEpoch == cmd.LeaseEpoch)
        {
            state.SubscribedChatChannelMask = cmd.SubscribedChannelMask;
        }
    }

    private void HandleSendNearbyTalk(SendNearbyTalkZoneCommand cmd)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.SenderCharacterId, out PlayerZoneState? sender) ||
            sender.LeaseEpoch != cmd.LeaseEpoch ||
            string.IsNullOrWhiteSpace(cmd.MessageUtf8))
        {
            return;
        }

        SpatialRegionSlot[] buffer = new SpatialRegionSlot[9];
        int regionCount = _regionGrid.GetSurroundingRegions(sender.RegionX, sender.RegionY, buffer);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = buffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                players[pIdx].OutboundSink.SendPlayerTalkBroadcast(sender.RuntimeNpcId, cmd.MessageUtf8);
            }
        }
    }

    private void HandleSendStructuredChat(SendStructuredChatZoneCommand cmd)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.SenderCharacterId, out PlayerZoneState? sender) ||
            sender.LeaseEpoch != cmd.LeaseEpoch ||
            string.IsNullOrWhiteSpace(cmd.MessageUtf8))
        {
            return;
        }

        if (cmd.Channel is ChatChannelKind.Nearby or ChatChannelKind.Screen)
        {
            uint channelBit = 1u << (int)cmd.Channel;
            SpatialRegionSlot[] buffer = new SpatialRegionSlot[9];
            int regionCount = _regionGrid.GetSurroundingRegions(sender.RegionX, sender.RegionY, buffer);
            for (int i = 0; i < regionCount; i++)
            {
                List<PlayerZoneState> players = buffer[i].Players;
                for (int pIdx = 0; pIdx < players.Count; pIdx++)
                {
                    PlayerZoneState recipient = players[pIdx];
                    if ((recipient.SubscribedChatChannelMask & channelBit) != 0)
                    {
                        recipient.OutboundSink.SendChatMessage(sender.RuntimeNpcId, sender.RoleNameUnicode, cmd.MessageUtf8);
                    }
                }
            }

            return;
        }

        var clusterChat = new DeliverClusterChatZoneCommand(
            SenderNpcId: sender.RuntimeNpcId,
            SenderNameUtf8: sender.RoleNameUnicode,
            Channel: cmd.Channel,
            TargetNpcId: cmd.TargetNpcId,
            TargetRoleNameUtf8: cmd.TargetOrPrefixUtf8 ?? string.Empty,
            SenderFactionId: sender.CurrentFactionId,
            MessageUtf8: cmd.MessageUtf8);

        if (_clusterRouter is not null)
        {
            _clusterRouter.RouteClusterChat(clusterChat);
        }
        else
        {
            HandleDeliverClusterChat(clusterChat);
        }
    }

    private void HandleDeliverClusterChat(DeliverClusterChatZoneCommand cmd)
    {
        if (_playersByCharacterId.Count == 0 || string.IsNullOrWhiteSpace(cmd.MessageUtf8))
        {
            return;
        }

        if (cmd.Channel == ChatChannelKind.System)
        {
            foreach (PlayerZoneState recipient in _playersByCharacterId.Values)
            {
                recipient.OutboundSink.SendSystemMessage(cmd.MessageUtf8);
            }

            return;
        }

        uint channelBit = 1u << (int)cmd.Channel;
        string normalizedTargetName = NormalizeRoleName(cmd.TargetRoleNameUtf8);
        string normalizedSenderName = NormalizeRoleName(cmd.SenderNameUtf8);

        foreach (PlayerZoneState recipient in _playersByCharacterId.Values)
        {
            if ((recipient.SubscribedChatChannelMask & channelBit) == 0)
            {
                continue;
            }

            bool deliver = cmd.Channel switch
            {
                ChatChannelKind.World or ChatChannelKind.All => true,
                ChatChannelKind.PrivateWhisper =>
                    (cmd.TargetNpcId != 0 && recipient.RuntimeNpcId == cmd.TargetNpcId) ||
                    (!string.IsNullOrEmpty(normalizedTargetName) &&
                     string.Equals(NormalizeRoleName(recipient.RoleNameUnicode), normalizedTargetName, StringComparison.Ordinal)) ||
                    string.Equals(NormalizeRoleName(recipient.RoleNameUnicode), normalizedSenderName, StringComparison.Ordinal),
                ChatChannelKind.Faction =>
                    cmd.SenderFactionId != CharacterConsts.NoFactionId &&
                    recipient.CurrentFactionId == cmd.SenderFactionId,
                ChatChannelKind.Team or ChatChannelKind.Tong => true,
                _ => false,
            };

            if (deliver)
            {
                recipient.OutboundSink.SendChatMessage(cmd.SenderNpcId, cmd.SenderNameUtf8, cmd.MessageUtf8);
            }
        }
    }

    private async ValueTask ProcessMovementAndPostureTickAsync(long tick, CancellationToken cancellationToken)
    {
        if (_playersByCharacterId.Count == 0)
        {
            return;
        }

        int ticksPerSec = Math.Max(1, _options.TicksPerSecond);
        _tickPlayerBuffer.Clear();
        foreach (PlayerZoneState p in _playersByCharacterId.Values)
        {
            _tickPlayerBuffer.Add(p);
        }

        for (int i = 0; i < _tickPlayerBuffer.Count; i++)
        {
            PlayerZoneState state = _tickPlayerBuffer[i];
            if (!state.IsSyncCompleted || !_playersByCharacterId.ContainsKey(state.CharacterId))
            {
                continue;
            }

            if (state.Doing is EntityDoingPosture.Walk or EntityDoingPosture.Run)
            {
                if (state.StatusEffects.IsStunned || state.Doing == EntityDoingPosture.Death)
                {
                    continue;
                }

                int stepSpeed = state.Doing == EntityDoingPosture.Run ? state.EffectiveRunSpeed : state.EffectiveWalkSpeed;
                (int nextX, int nextY) = ComputeNextStep(
                    state.CurrentMpsX,
                    state.CurrentMpsY,
                    state.TargetMpsX,
                    state.TargetMpsY,
                    stepSpeed);

                if (IsCellBlocked(nextX, nextY))
                {
                    state.TargetMpsX = state.CurrentMpsX;
                    state.TargetMpsY = state.CurrentMpsY;
                    state.Doing = EntityDoingPosture.Stand;
                    state.OutboundSink.SendSelfPositionSync(state);
                    BroadcastCompactSyncToNeighbors(state);
                    continue;
                }

                int dx = nextX - state.CurrentMpsX;
                int dy = nextY - state.CurrentMpsY;
                if (dx != 0 || dy != 0)
                {
                    state.Dir = ComputeDirection64(dx, dy);
                }

                MovePlayerCoordinateAndUpdateRegion(state, nextX, nextY);

                if (state.CurrentMpsX == state.TargetMpsX && state.CurrentMpsY == state.TargetMpsY)
                {
                    state.Doing = EntityDoingPosture.Stand;
                }

                state.OutboundSink.SendSelfPositionSync(state);
                BroadcastCompactSyncToNeighbors(state);

                await EvaluatePlayerTrapAsync(state, cancellationToken);
            }
            else if (state.Doing == EntityDoingPosture.Sit)
            {
                if (tick - state.LastSitRegenTick >= ticksPerSec)
                {
                    state.LastSitRegenTick = tick;
                    if (state.CurrentLife < state.MaxLife || state.CurrentMana < state.MaxMana)
                    {
                        int lifeRegen = Math.Max(5, state.MaxLife / 20);
                        int manaRegen = Math.Max(5, state.MaxMana / 20);
                        state.CurrentLife = Math.Min(state.MaxLife, state.CurrentLife + lifeRegen);
                        state.CurrentMana = Math.Min(state.MaxMana, state.CurrentMana + manaRegen);
                        state.OutboundSink.SendNormalVitalSync(state);
                    }
                }
            }
        }
    }

    private async ValueTask EvaluatePlayerTrapAsync(PlayerZoneState state, CancellationToken cancellationToken)
    {
        uint trapId = GetCellTrapId(state.CurrentMpsX, state.CurrentMpsY);
        if (trapId == 0)
        {
            state.LastTriggeredTrapId = 0;
            return;
        }

        if (trapId == state.LastTriggeredTrapId)
        {
            return;
        }

        state.LastTriggeredTrapId = trapId;
        if (_trapScriptCatalog is null || !_trapScriptCatalog.TryGetTrapBinding(trapId, out PortalTrapBindingTemplate? binding) || binding is null)
        {
            return;
        }

        if (binding.ActionKind == PortalTrapActionKind.ToggleFightStateAndSetPos)
        {
            bool enteringWilderness = state.FightMode == 0;
            int destX = enteringWilderness ? binding.TargetMpsX : (binding.AlternateMpsX > 0 ? binding.AlternateMpsX : binding.TargetMpsX);
            int destY = enteringWilderness ? binding.TargetMpsY : (binding.AlternateMpsY > 0 ? binding.AlternateMpsY : binding.TargetMpsY);
            byte nextFightMode = enteringWilderness ? binding.TargetFightMode : binding.AlternateFightMode;

            state.FightMode = nextFightMode;
            state.Doing = EntityDoingPosture.Stand;
            state.TargetMpsX = destX;
            state.TargetMpsY = destY;
            MovePlayerCoordinateAndUpdateRegion(state, destX, destY);
            state.LastTriggeredTrapId = GetCellTrapId(destX, destY);

            state.OutboundSink.SendSelfPositionSync(state);
            BroadcastCompactSyncToNeighbors(state);
            return;
        }

        if (binding.ActionKind == PortalTrapActionKind.ChangeMapPortal)
        {
            if (state.Level < binding.MinRequiredLevel)
            {
                if (binding.BounceBackMpsX > 0 && binding.BounceBackMpsY > 0)
                {
                    state.Doing = EntityDoingPosture.Stand;
                    state.TargetMpsX = binding.BounceBackMpsX;
                    state.TargetMpsY = binding.BounceBackMpsY;
                    MovePlayerCoordinateAndUpdateRegion(state, binding.BounceBackMpsX, binding.BounceBackMpsY);
                    state.LastTriggeredTrapId = GetCellTrapId(binding.BounceBackMpsX, binding.BounceBackMpsY);
                    state.OutboundSink.SendSelfPositionSync(state);
                    BroadcastCompactSyncToNeighbors(state);
                }

                if (!string.IsNullOrWhiteSpace(binding.DeniedMessage))
                {
                    state.OutboundSink.SendSystemMessage(binding.DeniedMessage);
                }

                return;
            }

            if (binding.TargetMapId == _mapDescriptor.MapId || binding.TargetMapId == 0)
            {
                state.FightMode = binding.TargetFightMode;
                state.Doing = EntityDoingPosture.Stand;
                state.TargetMpsX = binding.TargetMpsX;
                state.TargetMpsY = binding.TargetMpsY;
                MovePlayerCoordinateAndUpdateRegion(state, binding.TargetMpsX, binding.TargetMpsY);
                state.LastTriggeredTrapId = GetCellTrapId(binding.TargetMpsX, binding.TargetMpsY);
                state.OutboundSink.SendSelfPositionSync(state);
                BroadcastCompactSyncToNeighbors(state);
                return;
            }

            if (_clusterRouter is not null &&
                _clusterRouter.TryTransferPlayerAcrossMaps(
                    state,
                    binding.TargetMapId,
                    binding.TargetMpsX,
                    binding.TargetMpsY,
                    binding.TargetFightMode))
            {
                RemovePlayerFromZoneMemory(state);

                ZoneEngineLogMessages.LogPlayerMapTransition(
                    _logger,
                    state.RoleNameUnicode,
                    state.RuntimeNpcId,
                    trapId,
                    _mapDescriptor.MapId,
                    binding.TargetMapId,
                    binding.TargetMpsX,
                    binding.TargetMpsY);

                if (_characterAppService is not null)
                {
                    _ = await _characterAppService.UpdateCharacterWorldSnapshotAsync(
                        BuildWorldSnapshotDto(
                            state,
                            binding.TargetMapId,
                            binding.TargetMpsX,
                            binding.TargetMpsY,
                            binding.TargetFightMode),
                        cancellationToken);
                }
            }
        }
    }

    private void SynchronizeFullAoiForEnteringPlayer(PlayerZoneState state)
    {
        int regionCount = _regionGrid.GetSurroundingRegions(state.RegionX, state.RegionY, _aoiSlotBuffer);
        EntityFullSyncSnapshot selfSnap = state.CreateFullSyncSnapshot();

        for (int i = 0; i < regionCount; i++)
        {
            SpatialRegionSlot slot = _aoiSlotBuffer[i];

            for (int nIdx = 0; nIdx < slot.Npcs.Count; nIdx++)
            {
                EntityFullSyncSnapshot npcSnap = CreateNpcSnapshot(slot.Npcs[nIdx]);
                state.OutboundSink.SendNpcFullSync(in npcSnap);
            }

            for (int gIdx = 0; gIdx < slot.GroundObjects.Count; gIdx++)
            {
                GroundObjectSyncSnapshot objSnap = slot.GroundObjects[gIdx].CreateSyncSnapshot();
                state.OutboundSink.SendGroundObjectAdd(in objSnap);
            }

            for (int pIdx = 0; pIdx < slot.Players.Count; pIdx++)
            {
                PlayerZoneState other = slot.Players[pIdx];
                if (other.CharacterId == state.CharacterId)
                {
                    continue;
                }

                EntityFullSyncSnapshot otherSnap = other.CreateFullSyncSnapshot();
                state.OutboundSink.SendNpcFullSync(in otherSnap);
                other.OutboundSink.SendNpcFullSync(in selfSnap);
            }
        }
    }

    private void MovePlayerCoordinateAndUpdateRegion(PlayerZoneState state, int nextMpsX, int nextMpsY)
    {
        int oldRx = state.RegionX;
        int oldRy = state.RegionY;

        state.CurrentMpsX = nextMpsX;
        state.CurrentMpsY = nextMpsY;

        int newRx = state.RegionX;
        int newRy = state.RegionY;
        if (oldRx == newRx && oldRy == newRy)
        {
            return;
        }

        _regionGrid.RemovePlayer(state, oldRx, oldRy);
        _regionGrid.AddPlayer(state, newRx, newRy);

        int enteringCount = _regionGrid.ComputeEnteringRegions(oldRx, oldRy, newRx, newRy, _enteringSlotBuffer);
        EntityFullSyncSnapshot selfSnap = state.CreateFullSyncSnapshot();

        for (int i = 0; i < enteringCount; i++)
        {
            SpatialRegionSlot slot = _enteringSlotBuffer[i];

            for (int nIdx = 0; nIdx < slot.Npcs.Count; nIdx++)
            {
                EntityFullSyncSnapshot npcSnap = CreateNpcSnapshot(slot.Npcs[nIdx]);
                state.OutboundSink.SendNpcFullSync(in npcSnap);
            }

            for (int gIdx = 0; gIdx < slot.GroundObjects.Count; gIdx++)
            {
                GroundObjectSyncSnapshot objSnap = slot.GroundObjects[gIdx].CreateSyncSnapshot();
                state.OutboundSink.SendGroundObjectAdd(in objSnap);
            }

            for (int pIdx = 0; pIdx < slot.Players.Count; pIdx++)
            {
                PlayerZoneState other = slot.Players[pIdx];
                if (other.CharacterId == state.CharacterId)
                {
                    continue;
                }

                EntityFullSyncSnapshot otherSnap = other.CreateFullSyncSnapshot();
                state.OutboundSink.SendNpcFullSync(in otherSnap);
                other.OutboundSink.SendNpcFullSync(in selfSnap);
            }
        }
    }

    private void BroadcastCompactSyncToNeighbors(PlayerZoneState state)
    {
        int regionCount = _regionGrid.GetSurroundingRegions(state.RegionX, state.RegionY, _aoiSlotBuffer);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = _aoiSlotBuffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                PlayerZoneState observer = players[pIdx];
                if (observer.CharacterId == state.CharacterId)
                {
                    continue;
                }

                observer.OutboundSink.SendNpcCompactSync(
                    state.RuntimeNpcId,
                    state.CurrentMpsX,
                    state.CurrentMpsY,
                    state.Doing,
                    state.LifePercent,
                    (byte)state.Camp,
                    state.Dir);
            }
        }
    }

    private bool IsCellBlocked(int mpsX, int mpsY)
    {
        if (mpsX < 0 || mpsY < 0)
        {
            return true;
        }

        if (_mapCatalog is not null)
        {
            if (_mapCatalog.GetCellTrapId(_mapDescriptor.MapId, mpsX, mpsY) != 0)
            {
                return false;
            }

            return _mapCatalog.GetCellObstacle(_mapDescriptor.MapId, mpsX, mpsY) == 1;
        }

        return false;
    }

    private uint GetCellTrapId(int mpsX, int mpsY) =>
        _mapCatalog?.GetCellTrapId(_mapDescriptor.MapId, mpsX, mpsY) ?? 0u;

    private static (int NextMpsX, int NextMpsY) ComputeNextStep(
        int curX,
        int curY,
        int targetX,
        int targetY,
        int stepSpeed)
    {
        long dx = (long)targetX - curX;
        long dy = (long)targetY - curY;
        double dist = Math.Sqrt((dx * dx) + (dy * dy));
        if (dist <= stepSpeed || dist < 1.0)
        {
            return (targetX, targetY);
        }

        double ratio = stepSpeed / dist;
        int nextX = curX + (int)Math.Round(dx * ratio);
        int nextY = curY + (int)Math.Round(dy * ratio);
        return (nextX, nextY);
    }

    private static byte ComputeDirection64(int dx, int dy)
    {
        if (dx == 0 && dy == 0)
        {
            return 0;
        }

        double angle = Math.Atan2(dx, -dy);
        if (angle < 0)
        {
            angle += 2.0 * Math.PI;
        }

        int dir = (int)Math.Round((angle / (2.0 * Math.PI)) * 64.0) & 63;
        return (byte)dir;
    }

    private static EntityFullSyncSnapshot CreateNpcSnapshot(NpcZoneState npc) =>
        new(
            NpcId: npc.NpcId,
            MpsX: npc.MpsX,
            MpsY: npc.MpsY,
            NpcSettingIdx: npc.PackedNpcSettingIdx,
            Series: npc.Series,
            LifePer: npc.LifePercent,
            Doing: npc.Doing,
            Camp: npc.Camp,
            Dir: npc.Dir,
            Kind: npc.Kind,
            IsSpecialNpc: npc.IsSpecialNpc,
            NameUtf8: npc.Name);

    private uint AllocatePlayerNpcId()
    {
        uint id = _nextRuntimePlayerNpcId++;
        if (id < 10000u)
        {
            _nextRuntimePlayerNpcId = 10000u;
            id = _nextRuntimePlayerNpcId++;
        }

        return id;
    }

    private void RemovePlayerFromZoneMemory(PlayerZoneState state)
    {
        _playersByCharacterId.Remove(state.CharacterId);
        _playersByNpcId.Remove(state.RuntimeNpcId);
        _regionGrid.RemovePlayer(state, state.RegionX, state.RegionY);
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

    private void HandleCastSkill(CastSkillZoneCommand cmd, long tick)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? caster) ||
            caster.LeaseEpoch != cmd.LeaseEpoch ||
            !caster.IsSyncCompleted ||
            caster.CurrentLife <= 0 ||
            caster.Doing == EntityDoingPosture.Death ||
            caster.StatusEffects.IsStunned)
        {
            return;
        }

        caster.LastPingReceivedTick = tick;

        if (!caster.TryGetLearnedSkillLevel(cmd.SkillId, out ushort learnedLevel) || learnedLevel == 0)
        {
            ZoneEngineLogMessages.LogSkillCastRejected(
                _logger,
                caster.RoleNameUnicode,
                caster.CharacterId,
                cmd.SkillId,
                "SkillNotLearned");
            return;
        }

        SkillTemplate? skill = null;
        if (_skillTemplateCatalog is null || !_skillTemplateCatalog.TryGetSkill(cmd.SkillId, out skill) || skill is null)
        {
            skill = new SkillTemplate(
                Id: cmd.SkillId,
                Code: $"skill-{cmd.SkillId}",
                Name: $"Skill {cmd.SkillId}",
                Style: SkillStyleKind.Melee,
                ElementSeries: caster.Series,
                IsOffensive: true,
                IsPhysical: true,
                IsMelee: true,
                IsAura: false,
                IsWeaponSkill: true,
                IsExpSkill: false,
                UseAttackRating: true,
                DoHurtAnimation: true,
                PeaceCanUse: false,
                ReqLevel: 1,
                MaxLevel: 20,
                EquipLimit: -2,
                HorseLimit: 0,
                CastRangeMps: 96,
                WaitFrames: 9,
                MissileId: 0,
                MissileForm: "line",
                ChildSkillId: 0,
                ChildSkillLevel: 0,
                ChildSkillCount: 1,
                CostScaling: new SkillCostScalingTemplate("mana", 0),
                EffectScaling:
                [
                    new SkillEffectScalingTemplate(
                        1,
                        "physics_damage_v",
                        "damage",
                        new SkillParameterCurveTemplate("Linear", 15, 5),
                        null,
                        new SkillParameterCurveTemplate("Linear", 25, 8)),
                ]);
        }

        if (caster.FightMode == 0 && !skill.PeaceCanUse)
        {
            ZoneEngineLogMessages.LogSkillCastRejected(
                _logger,
                caster.RoleNameUnicode,
                caster.CharacterId,
                cmd.SkillId,
                "PeaceZoneDisallowed");
            return;
        }

        if (!caster.IsSkillOffCooldown(cmd.SkillId, tick))
        {
            ZoneEngineLogMessages.LogSkillCastRejected(
                _logger,
                caster.RoleNameUnicode,
                caster.CharacterId,
                cmd.SkillId,
                "SkillOnCooldown");
            return;
        }

        // Resolve target coordinate / entity
        int targetMpsX;
        int targetMpsY;
        uint targetNpcId = 0;

        if (cmd.TargetParam1 == -1 && cmd.TargetParam2 > 0)
        {
            targetNpcId = (uint)cmd.TargetParam2;
            if (_npcsById.TryGetValue(targetNpcId, out NpcZoneState? targetNpc) &&
                targetNpc.CurLife > 0 &&
                targetNpc.AiState != MonsterAiStateKind.DeadRespawnWait)
            {
                targetMpsX = targetNpc.MpsX;
                targetMpsY = targetNpc.MpsY;
            }
            else if (_playersByNpcId.TryGetValue(targetNpcId, out PlayerZoneState? targetPlayer) && targetPlayer.CurrentLife > 0)
            {
                targetMpsX = targetPlayer.CurrentMpsX;
                targetMpsY = targetPlayer.CurrentMpsY;
            }
            else
            {
                return;
            }
        }
        else if (cmd.TargetParam1 > 0 &&
                 cmd.TargetParam2 > 0 &&
                 cmd.TargetParam2 < 10000 &&
                 _npcsById.TryGetValue((uint)cmd.TargetParam2, out NpcZoneState? directNpc) &&
                 directNpc.CurLife > 0 &&
                 directNpc.AiState != MonsterAiStateKind.DeadRespawnWait)
        {
            targetNpcId = directNpc.NpcId;
            targetMpsX = directNpc.MpsX;
            targetMpsY = directNpc.MpsY;
        }
        else
        {
            targetMpsX = cmd.TargetParam1;
            targetMpsY = cmd.TargetParam2;
        }

        int distance = SixtyFourDirectionTrigTable.GetDistance(caster.CurrentMpsX, caster.CurrentMpsY, targetMpsX, targetMpsY);
        int maxRange = Math.Max(64, skill.CastRangeMps + 64);
        if (distance > maxRange)
        {
            ZoneEngineLogMessages.LogSkillCastRejected(
                _logger,
                caster.RoleNameUnicode,
                caster.CharacterId,
                cmd.SkillId,
                "OutOfRange");
            return;
        }

        // Check & deduct resource cost
        int cost = skill.CostScaling.EvaluateCost(learnedLevel);
        string costType = skill.CostScaling.CostType ?? "mana";
        if (string.Equals(costType, "stamina", StringComparison.OrdinalIgnoreCase))
        {
            if (caster.CurrentStamina < cost)
            {
                return;
            }

            caster.CurrentStamina = (ushort)Math.Max(0, caster.CurrentStamina - cost);
        }
        else if (string.Equals(costType, "life", StringComparison.OrdinalIgnoreCase))
        {
            if (caster.CurrentLife <= cost)
            {
                return;
            }

            caster.CurrentLife -= cost;
        }
        else
        {
            if (caster.CurrentMana < cost)
            {
                ZoneEngineLogMessages.LogSkillCastRejected(
                    _logger,
                    caster.RoleNameUnicode,
                    caster.CharacterId,
                    cmd.SkillId,
                    "InsufficientMana");
                return;
            }

            caster.CurrentMana = Math.Max(0, caster.CurrentMana - cost);
        }

        if (cost > 0)
        {
            caster.OutboundSink.SendNormalVitalSync(caster);
        }

        int waitFrames = Math.Max(1, skill.WaitFrames);
        caster.SetSkillCooldown(cmd.SkillId, tick + waitFrames);
        caster.Doing = EntityDoingPosture.Stand;
        caster.TargetMpsX = caster.CurrentMpsX;
        caster.TargetMpsY = caster.CurrentMpsY;
        int dirIdx = SixtyFourDirectionTrigTable.GetDirectionIndex(caster.CurrentMpsX, caster.CurrentMpsY, targetMpsX, targetMpsY);
        if (dirIdx >= 0)
        {
            caster.Dir = (byte)(dirIdx & 63);
        }

        // Broadcast 0x5A (s2c_skillcast) to 3x3 AOI
        BroadcastSkillCastToAoi(
            caster.RegionX,
            caster.RegionY,
            caster.RuntimeNpcId,
            (uint)cmd.SkillId,
            learnedLevel,
            cmd.TargetParam1,
            cmd.TargetParam2,
            castDirectly: false);

        SkillDamagePayload damagePayload = CombatDamageCalculator.BuildPlayerSkillDamagePayload(caster, skill, learnedLevel);

        if (skill.MissileId > 0 && !skill.IsMelee)
        {
            MissileTemplate? missileTpl = null;
            if (_missileTemplateCatalog is null || !_missileTemplateCatalog.TryGetMissile(skill.MissileId, out missileTpl) || missileTpl is null)
            {
                missileTpl = new MissileTemplate(
                    Id: skill.MissileId,
                    Code: $"missile-{skill.MissileId}",
                    Name: $"Missile {skill.MissileId}",
                    MoveKind: MissileMoveKind.Line64,
                    FollowKind: "none",
                    SpeedMpsPerFrame: 32,
                    HeightMps: 0,
                    ZSpeedMpsPerFrame: 0,
                    ZAccelMpsPerFrame2: 0,
                    LifeFrames: 24,
                    HitRadiusMps: 64,
                    CollideRadiusCells: 1,
                    DamageRadiusCells: 1,
                    IsRangeDamage: false,
                    DamageIntervalFrames: 0,
                    PierceCount: 1,
                    CollideVanish: true,
                    CanCollideFriend: false,
                    AutoExplode: false,
                    MustHit: false,
                    SubMissileId: 0);
            }

            _missileSystem.SpawnSkillMissiles(
                caster.RuntimeNpcId,
                isLauncherPlayer: true,
                caster.Camp,
                caster.PkState,
                caster.CurrentMpsX,
                caster.CurrentMpsY,
                targetMpsX,
                targetMpsY,
                targetNpcId,
                skill,
                missileTpl,
                in damagePayload);
            return;
        }

        // Immediate melee / direct hit resolution
        ResolveImmediateSkillHit(caster, targetNpcId, targetMpsX, targetMpsY, skill, in damagePayload);
    }

    private void ResolveImmediateSkillHit(
        PlayerZoneState caster,
        uint explicitTargetNpcId,
        int targetMpsX,
        int targetMpsY,
        SkillTemplate skill,
        in SkillDamagePayload damagePayload)
    {
        if (explicitTargetNpcId != 0)
        {
            if (_npcsById.TryGetValue(explicitTargetNpcId, out NpcZoneState? targetNpc) && targetNpc.CurLife > 0)
            {
                TryApplyPlayerDamageToNpc(caster, targetNpc, in damagePayload);
                return;
            }

            if (_playersByNpcId.TryGetValue(explicitTargetNpcId, out PlayerZoneState? targetPlayer) && targetPlayer.CurrentLife > 0)
            {
                TryApplyPlayerDamageToPlayer(caster, targetPlayer, in damagePayload);
                return;
            }
        }

        // Scan 3x3 regions around target coordinate within melee hit radius
        int rx = targetMpsX / WorldCoordinateConsts.RegionWidthMps;
        int ry = targetMpsY / WorldCoordinateConsts.RegionHeightMps;
        int regionCount = _regionGrid.GetSurroundingRegions(rx, ry, _aoiSlotBuffer);
        int hitRadius = Math.Max(64, skill.CastRangeMps);

        for (int i = 0; i < regionCount; i++)
        {
            SpatialRegionSlot slot = _aoiSlotBuffer[i];
            for (int nIdx = 0; nIdx < slot.Npcs.Count; nIdx++)
            {
                NpcZoneState npc = slot.Npcs[nIdx];
                if (npc.CurLife == 0 || npc.AiState == MonsterAiStateKind.DeadRespawnWait)
                {
                    continue;
                }

                if (SixtyFourDirectionTrigTable.GetDistance(targetMpsX, targetMpsY, npc.MpsX, npc.MpsY) <= hitRadius)
                {
                    if (TryApplyPlayerDamageToNpc(caster, npc, in damagePayload))
                    {
                        return;
                    }
                }
            }

            for (int pIdx = 0; pIdx < slot.Players.Count; pIdx++)
            {
                PlayerZoneState other = slot.Players[pIdx];
                if (other.CharacterId == caster.CharacterId || other.CurrentLife <= 0)
                {
                    continue;
                }

                if (SixtyFourDirectionTrigTable.GetDistance(targetMpsX, targetMpsY, other.CurrentMpsX, other.CurrentMpsY) <= hitRadius)
                {
                    if (TryApplyPlayerDamageToPlayer(caster, other, in damagePayload))
                    {
                        return;
                    }
                }
            }
        }
    }

    private bool TryApplyPlayerDamageToNpc(
        PlayerZoneState attacker,
        NpcZoneState npc,
        in SkillDamagePayload payload)
    {
        EntityRelationKind rel = CombatDamageCalculator.EvaluateRelation(
            attacker.RuntimeNpcId,
            EntityKind.Player,
            attacker.Camp,
            attacker.FightMode,
            attacker.PkState,
            npc.NpcId,
            npc.Kind,
            npc.Camp,
            defenderFightMode: 1,
            defenderPkMode: PlayerPkMode.Fight);

        if (rel != EntityRelationKind.Enemy)
        {
            return false;
        }

        if (!CombatDamageCalculator.CheckHitTarget(
            payload.UseAttackRating,
            payload.AttackRatingScore,
            npc.Defense,
            _random) &&
            payload.AttackRatingScore < npc.Defense * 3)
        {
            return true;
        }

        int dmg = CombatDamageCalculator.ResolveHitAndApplyDamage(
            attacker.RuntimeNpcId,
            isPvp: false,
            in payload,
            npc.Series,
            npc.PhysicsResistPercent,
            npc.ColdResistPercent,
            npc.FireResistPercent,
            npc.LightResistPercent,
            npc.PoisonResistPercent,
            npc.StatusEffects,
            _random);

        ApplyDamageToNpc(attacker.RuntimeNpcId, npc, dmg, payload.DoHurtAnimation);
        return true;
    }

    private bool TryApplyPlayerDamageToPlayer(
        PlayerZoneState attacker,
        PlayerZoneState defender,
        in SkillDamagePayload payload)
    {
        EntityRelationKind rel = CombatDamageCalculator.EvaluateRelation(
            attacker.RuntimeNpcId,
            EntityKind.Player,
            attacker.Camp,
            attacker.FightMode,
            attacker.PkState,
            defender.RuntimeNpcId,
            EntityKind.Player,
            defender.Camp,
            defender.FightMode,
            defender.PkState);

        if (rel != EntityRelationKind.Enemy)
        {
            return false;
        }

        if (!CombatDamageCalculator.CheckHitTarget(
            payload.UseAttackRating,
            payload.AttackRatingScore,
            defender.EffectiveDefense,
            _random))
        {
            return true;
        }

        int dmg = CombatDamageCalculator.ResolveHitAndApplyDamage(
            attacker.RuntimeNpcId,
            isPvp: true,
            in payload,
            defender.Series,
            defender.PhysicsResistPercent,
            defender.ColdResistPercent,
            defender.FireResistPercent,
            defender.LightResistPercent,
            defender.PoisonResistPercent,
            defender.StatusEffects,
            _random);

        ApplyDamageToPlayer(attacker.RuntimeNpcId, defender, dmg, payload.DoHurtAnimation);
        return true;
    }

    private void OnMissileHitPlayer(ActiveMissileSlot missile, PlayerZoneState defender, int damage) =>
        ApplyDamageToPlayer(missile.LauncherNpcId, defender, damage, missile.DamagePayload.DoHurtAnimation);

    private void OnMissileHitNpc(ActiveMissileSlot missile, NpcZoneState npc, int damage) =>
        ApplyDamageToNpc(missile.LauncherNpcId, npc, damage, missile.DamagePayload.DoHurtAnimation);

    private void ApplyDamageToNpc(uint attackerNpcId, NpcZoneState npc, int damage, bool doHurtAnimation)
    {
        if (npc.CurLife == 0 || npc.AiState == MonsterAiStateKind.DeadRespawnWait)
        {
            return;
        }

        uint clampedDmg = (uint)Math.Max(1, damage);
        npc.CurLife = npc.CurLife > clampedDmg ? npc.CurLife - clampedDmg : 0u;
        npc.AggroTargetNpcId = attackerNpcId;
        if (npc.AiState == MonsterAiStateKind.IdlePatrol)
        {
            npc.AiState = MonsterAiStateKind.Pursuit;
        }

        if (npc.CurLife > 0)
        {
            if (doHurtAnimation && npc.HitRecoverFrames > 0)
            {
                npc.StatusEffects.HurtRecoveryRemainingFrames = npc.HitRecoverFrames;
                BroadcastHurtToAoi(npc.RegionX, npc.RegionY, npc.NpcId, npc.MpsX, npc.MpsY, npc.HitRecoverFrames);
            }

            BroadcastNpcCompactSyncToAoi(npc);
        }
        else
        {
            HandleNpcDeath(npc, attackerNpcId);
        }
    }

    private void ApplyDamageToPlayer(uint attackerNpcId, PlayerZoneState defender, int damage, bool doHurtAnimation)
    {
        if (defender.CurrentLife <= 0 || defender.Doing == EntityDoingPosture.Death)
        {
            return;
        }

        defender.CurrentLife = Math.Max(0, defender.CurrentLife - Math.Max(1, damage));
        defender.OutboundSink.SendNormalVitalSync(defender);

        if (defender.CurrentLife > 0)
        {
            if (doHurtAnimation)
            {
                defender.StatusEffects.HurtRecoveryRemainingFrames = 6;
                BroadcastHurtToAoi(defender.RegionX, defender.RegionY, defender.RuntimeNpcId, defender.CurrentMpsX, defender.CurrentMpsY, 6);
            }

            BroadcastCompactSyncToNeighbors(defender);
        }
        else
        {
            HandlePlayerDeath(defender, attackerNpcId);
        }
    }

    private void HandleNpcDeath(NpcZoneState npc, uint killerNpcId)
    {
        npc.CurLife = 0;
        npc.Doing = EntityDoingPosture.Death;
        npc.AiState = MonsterAiStateKind.DeadRespawnWait;
        npc.AggroTargetNpcId = 0;
        npc.RespawnReadyTick = CurrentFrameTick + Math.Max(18, npc.RespawnFrames);
        npc.StatusEffects.ClearAll();

        BroadcastNpcDeathToAoi(npc.RegionX, npc.RegionY, npc.NpcId);

        PlayerZoneState? killer = null;
        if (killerNpcId != 0 && _playersByNpcId.TryGetValue(killerNpcId, out PlayerZoneState? resolvedKiller) && resolvedKiller.CurrentLife > 0)
        {
            killer = resolvedKiller;
            AwardPlayerExperience(killer, Math.Max(10, npc.ExpReward));
        }

        List<GroundObjectSlot> droppedObjects = _groundObjectSystem.EvaluateAndSpawnMonsterLoot(
            _mapDescriptor.MapId,
            _mapCatalog,
            _lootTableCatalog,
            _itemGenerator,
            _regionGrid,
            npc,
            killer,
            CurrentFrameTick);

        for (int i = 0; i < droppedObjects.Count; i++)
        {
            BroadcastGroundObjectAddToAoi(droppedObjects[i]);
        }

        ZoneEngineLogMessages.LogEntityDefeated(
            _logger,
            npc.NpcId,
            killerNpcId,
            _mapDescriptor.MapId,
            npc.ExpReward,
            droppedObjects.Count);
    }

    private void HandleRequestGroundObjectSync(RequestGroundObjectSyncZoneCommand cmd, long tick)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? player) ||
            player.LeaseEpoch != cmd.LeaseEpoch ||
            !player.IsSyncCompleted)
        {
            return;
        }

        player.LastPingReceivedTick = tick;
        if (_groundObjectSystem.TryGetGroundObject(cmd.ObjId, out GroundObjectSlot? obj) && obj is not null)
        {
            GroundObjectSyncSnapshot snap = obj.CreateSyncSnapshot();
            player.OutboundSink.SendGroundObjectAdd(in snap);
        }
    }

    private void HandlePickupGroundObject(PickupGroundObjectZoneCommand cmd, long tick)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? player) ||
            player.LeaseEpoch != cmd.LeaseEpoch ||
            !player.IsSyncCompleted ||
            player.CurrentLife <= 0 ||
            player.Doing == EntityDoingPosture.Death)
        {
            return;
        }

        player.LastPingReceivedTick = tick;
        if (!_groundObjectSystem.TryGetGroundObject(cmd.ObjId, out GroundObjectSlot? obj) || obj is null)
        {
            return;
        }

        int distance = SixtyFourDirectionTrigTable.GetDistance(player.CurrentMpsX, player.CurrentMpsY, obj.MpsX, obj.MpsY);
        if (distance > ItemConsts.MaxGroundPickupDistanceMps)
        {
            return;
        }

        if (!obj.CanCharacterPickup(player.CharacterId, tick))
        {
            player.OutboundSink.SendSystemMessage("Vật phẩm này đang thuộc quyền nhặt của người chơi khác.");
            return;
        }

        if (obj.Kind == GroundObjectKind.Money)
        {
            if (!_groundObjectSystem.TryRemoveGroundObject(cmd.ObjId, _regionGrid, out _))
            {
                return;
            }

            BroadcastGroundObjectRemoveToAoi(obj, 1);
            int addedMoney = Math.Max(0, obj.MoneyAmount);
            player.InventoryMoney = (int)Math.Clamp((long)player.InventoryMoney + addedMoney, 0, int.MaxValue);
            player.OutboundSink.SendMoneySync(player.InventoryMoney, player.RepositoryMoney);
            return;
        }

        if (obj.ItemPayload is null)
        {
            return;
        }

        PlayerItemSlot item = obj.ItemPayload;
        item.RuntimeItemId = player.Inventory.AllocateRuntimeItemId();
        ItemContainerPlace preferredPlace = cmd.TargetPlace == ItemContainerPlace.None
            ? ItemContainerPlace.EquipRoom
            : cmd.TargetPlace;

        if (!player.Inventory.TryAddOrAutoPlaceItem(item, preferredPlace, cmd.TargetGridX, cmd.TargetGridY, player))
        {
            player.OutboundSink.SendSystemMessage("Hành trang không đủ chỗ trống.");
            return;
        }

        if (!_groundObjectSystem.TryRemoveGroundObject(cmd.ObjId, _regionGrid, out _))
        {
            _ = player.Inventory.TryRemoveItem(item.RuntimeItemId, out _, player);
            return;
        }

        BroadcastGroundObjectRemoveToAoi(obj, 1);
        PlayerItemSyncSnapshot syncSnap = PlayerItemSyncSnapshot.CreateFromSlot(item);
        player.OutboundSink.SendItemSync(in syncSnap);
        if (item.ContainerPlace == ItemContainerPlace.Equip)
        {
            player.OutboundSink.SendNormalVitalSync(player);
        }
    }

    private void HandleMoveInventoryItem(MoveInventoryItemZoneCommand cmd, long tick)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? player) ||
            player.LeaseEpoch != cmd.LeaseEpoch ||
            !player.IsSyncCompleted ||
            player.CurrentLife <= 0 ||
            player.Doing == EntityDoingPosture.Death)
        {
            return;
        }

        player.LastPingReceivedTick = tick;
        bool moved = player.Inventory.TryMoveOrSwapItem(
            player,
            cmd.SrcPlace,
            cmd.SrcGridX,
            cmd.SrcGridY,
            cmd.DestPlace,
            cmd.DestGridX,
            cmd.DestGridY);

        if (moved)
        {
            player.OutboundSink.SendPlayerMoveItemConfirm(
                downY: cmd.SrcGridY,
                downPos: cmd.SrcPlace,
                downX: cmd.SrcGridX,
                upY: cmd.DestGridY,
                upX: cmd.DestGridX,
                upPos: cmd.DestPlace);

            if (cmd.SrcPlace == ItemContainerPlace.Equip || cmd.DestPlace == ItemContainerPlace.Equip)
            {
                player.OutboundSink.SendNormalVitalSync(player);
            }
        }

        player.OutboundSink.SendItemAutoMoveUnlock();
    }

    private async ValueTask HandleUseInventoryItemAsync(
        UseInventoryItemZoneCommand cmd,
        long tick,
        CancellationToken cancellationToken)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? player) ||
            player.LeaseEpoch != cmd.LeaseEpoch ||
            !player.IsSyncCompleted ||
            player.CurrentLife <= 0 ||
            player.Doing == EntityDoingPosture.Death)
        {
            return;
        }

        player.LastPingReceivedTick = tick;
        PlayerItemSlot? item = ResolveConsumableItem(player, cmd);
        if (item is null)
        {
            return;
        }

        if (item.Genre == ItemGenreKind.Medicine)
        {
            ApplyMedicineRegenFromItem(player, item);
            ConsumeOneStackFromItem(player, item);
            return;
        }

        if (item.Genre == ItemGenreKind.TownPortal)
        {
            if (!_mapDescriptor.AllowTownPortal)
            {
                player.OutboundSink.SendSystemMessage("Bản đồ hiện tại không thể sử dụng Thổ Địa Phù.");
                return;
            }

            ConsumeOneStackFromItem(player, item);
            await TeleportPlayerToRevivalTownAsync(player, cancellationToken);
        }
    }

    private void HandleDropInventoryItem(DropInventoryItemZoneCommand cmd, long tick)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? player) ||
            player.LeaseEpoch != cmd.LeaseEpoch ||
            !player.IsSyncCompleted ||
            player.CurrentLife <= 0 ||
            player.Doing == EntityDoingPosture.Death)
        {
            return;
        }

        player.LastPingReceivedTick = tick;

        if (cmd.ThrowAllMedicine)
        {
            var medicines = new List<PlayerItemSlot>();
            foreach (PlayerItemSlot slot in player.Inventory.AllItems)
            {
                if (slot.Genre == ItemGenreKind.Medicine && slot.BindState == ItemBindState.Unbound)
                {
                    medicines.Add(slot);
                }
            }

            for (int i = 0; i < medicines.Count; i++)
            {
                PlayerItemSlot med = medicines[i];
                if (player.Inventory.TryRemoveItem(med.RuntimeItemId, out _, player))
                {
                    player.OutboundSink.SendItemRemove(med.RuntimeItemId);
                    GroundObjectSlot dropped = _groundObjectSystem.SpawnItemGroundObject(
                        _mapDescriptor.MapId,
                        _mapCatalog,
                        _regionGrid,
                        med,
                        player.CurrentMpsX,
                        player.CurrentMpsY,
                        ownerCharacterId: null,
                        currentTick: tick,
                        spiralIndex: i);
                    BroadcastGroundObjectAddToAoi(dropped);
                }
            }

            return;
        }

        PlayerItemSlot? handItem = player.Inventory.HandItem;
        if (handItem is null || handItem.BindState != ItemBindState.Unbound)
        {
            return;
        }

        if (player.Inventory.TryRemoveItem(handItem.RuntimeItemId, out _, player))
        {
            player.OutboundSink.SendItemRemove(handItem.RuntimeItemId);
            GroundObjectSlot dropped = _groundObjectSystem.SpawnItemGroundObject(
                _mapDescriptor.MapId,
                _mapCatalog,
                _regionGrid,
                handItem,
                player.CurrentMpsX,
                player.CurrentMpsY,
                ownerCharacterId: null,
                currentTick: tick);
            BroadcastGroundObjectAddToAoi(dropped);
        }
    }

    private void HandleDivideInventoryItem(DivideInventoryItemZoneCommand cmd, long tick)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? player) ||
            player.LeaseEpoch != cmd.LeaseEpoch ||
            !player.IsSyncCompleted ||
            player.CurrentLife <= 0 ||
            player.Doing == EntityDoingPosture.Death ||
            cmd.DivideCount == 0 ||
            player.Inventory.HandItem is not null)
        {
            return;
        }

        player.LastPingReceivedTick = tick;
        if (!player.Inventory.TryGetItemByRuntimeId(cmd.RuntimeItemId, out PlayerItemSlot? sourceItem) ||
            sourceItem is null ||
            sourceItem.StackCount <= cmd.DivideCount)
        {
            return;
        }

        sourceItem.StackCount -= cmd.DivideCount;
        PlayerItemSyncSnapshot updatedSourceSnap = PlayerItemSyncSnapshot.CreateFromSlot(sourceItem);
        player.OutboundSink.SendItemSync(in updatedSourceSnap);

        uint splitId = player.Inventory.AllocateRuntimeItemId();
        var splitItem = new PlayerItemSlot(
            runtimeItemId: splitId,
            domainItemId: Guid.NewGuid(),
            template: sourceItem.Template,
            genre: sourceItem.Genre,
            detailType: sourceItem.DetailType,
            particularType: sourceItem.ParticularType,
            level: sourceItem.Level,
            series: sourceItem.Series,
            quality: sourceItem.Quality,
            randomSeed: sourceItem.RandomSeed,
            generatorVersion: sourceItem.GeneratorVersion,
            luck: sourceItem.Luck,
            currentDurability: sourceItem.CurrentDurability,
            maxDurability: sourceItem.MaxDurability,
            stackCount: cmd.DivideCount,
            containerPlace: ItemContainerPlace.Hand,
            gridX: 0,
            gridY: 0,
            magicAttributes: sourceItem.MagicAttributes,
            bindState: sourceItem.BindState,
            expireTimeUnixSeconds: sourceItem.ExpireTimeUnixSeconds,
            creatorName: sourceItem.CreatorName);

        if (player.Inventory.TryPlaceItemDirect(splitItem, ItemContainerPlace.Hand, 0, 0, player))
        {
            PlayerItemSyncSnapshot splitSnap = PlayerItemSyncSnapshot.CreateFromSlot(splitItem);
            player.OutboundSink.SendItemSync(in splitSnap);
        }
    }

    private static PlayerItemSlot? ResolveConsumableItem(PlayerZoneState player, UseInventoryItemZoneCommand cmd)
    {
        if (cmd.IsEmergencyTownPortal)
        {
            for (byte i = 0; i < ItemConsts.ImmediacyBeltSlots; i++)
            {
                PlayerItemSlot? beltItem = player.Inventory.GetImmediacyItem(i);
                if (beltItem is not null && beltItem.Genre == ItemGenreKind.TownPortal)
                {
                    return beltItem;
                }
            }

            foreach (PlayerItemSlot candidate in player.Inventory.AllItems)
            {
                if (candidate.Genre == ItemGenreKind.TownPortal)
                {
                    return candidate;
                }
            }

            return null;
        }

        if (cmd.RuntimeItemId != 0 &&
            player.Inventory.TryGetItemByRuntimeId(cmd.RuntimeItemId, out PlayerItemSlot? byId) &&
            byId is not null)
        {
            return byId;
        }

        return player.Inventory.FindItemAt(cmd.Place, cmd.GridX, cmd.GridY);
    }

    private static void ApplyMedicineRegenFromItem(PlayerZoneState player, PlayerItemSlot item)
    {
        bool appliedFromTemplate = false;
        foreach (ItemBaseAttributeTemplate baseAttr in item.Template.BaseAttributes)
        {
            string code = baseAttr.AttributeCode ?? string.Empty;
            int durationFrames = Math.Max(9, baseAttr.DurationFrames > 0 ? baseAttr.DurationFrames : 90);
            int ticks = Math.Max(1, durationFrames / 9);
            int totalAmount = Math.Max(10, baseAttr.MinValue);
            int regenPerTick = Math.Max(1, totalAmount / ticks);

            if (string.Equals(code, "lifepotion_v", StringComparison.OrdinalIgnoreCase))
            {
                player.MedicineLifeRegenPerTick = Math.Max(1, baseAttr.MinValue);
                player.MedicineLifeRemainingFrames += durationFrames;
                appliedFromTemplate = true;
            }
            else if (string.Equals(code, "manapotion_v", StringComparison.OrdinalIgnoreCase))
            {
                player.MedicineManaRegenPerTick = Math.Max(1, baseAttr.MinValue);
                player.MedicineManaRemainingFrames += durationFrames;
                appliedFromTemplate = true;
            }
            else if (string.Equals(code, "life_v", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(code, "lifereplenish_v", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(code, "restore_life_v", StringComparison.OrdinalIgnoreCase))
            {
                player.MedicineLifeRegenPerTick = regenPerTick;
                player.MedicineLifeRemainingFrames += durationFrames;
                appliedFromTemplate = true;
            }
            else if (string.Equals(code, "mana_v", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(code, "manareplenish_v", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(code, "restore_mana_v", StringComparison.OrdinalIgnoreCase))
            {
                player.MedicineManaRegenPerTick = regenPerTick;
                player.MedicineManaRemainingFrames += durationFrames;
                appliedFromTemplate = true;
            }
        }

        if (!appliedFromTemplate)
        {
            if (item.ParticularType == 2)
            {
                player.MedicineManaRegenPerTick = 15;
                player.MedicineManaRemainingFrames += 90;
            }
            else
            {
                player.MedicineLifeRegenPerTick = 15;
                player.MedicineLifeRemainingFrames += 90;
            }
        }
    }

    private static void ConsumeOneStackFromItem(PlayerZoneState player, PlayerItemSlot item)
    {
        if (item.StackCount > 1)
        {
            item.StackCount--;
            PlayerItemSyncSnapshot updatedSnap = PlayerItemSyncSnapshot.CreateFromSlot(item);
            player.OutboundSink.SendItemSync(in updatedSnap);
            return;
        }

        if (player.Inventory.TryRemoveItem(item.RuntimeItemId, out _, player))
        {
            player.OutboundSink.SendItemRemove(item.RuntimeItemId);
        }
    }

    private async ValueTask TeleportPlayerToRevivalTownAsync(
        PlayerZoneState player,
        CancellationToken cancellationToken)
    {
        ushort reviveMapId = (ushort)(player.RevivalMapId > 0 ? player.RevivalMapId : CharacterConsts.DefaultSpawnMapId);
        int reviveMpsX = 52000;
        int reviveMpsY = 101696;
        if (_playerTemplateCatalog is not null &&
            _playerTemplateCatalog.TryGetStarterVillage(reviveMapId, out StarterVillageTemplate? village) &&
            village is not null)
        {
            reviveMpsX = village.SpawnMpsX;
            reviveMpsY = village.SpawnMpsY;
        }

        player.Doing = EntityDoingPosture.Stand;
        player.FightMode = 0;

        if (reviveMapId == _mapDescriptor.MapId || _clusterRouter is null)
        {
            MovePlayerCoordinateAndUpdateRegion(player, reviveMpsX, reviveMpsY);
            player.TargetMpsX = reviveMpsX;
            player.TargetMpsY = reviveMpsY;
            player.LastTriggeredTrapId = GetCellTrapId(reviveMpsX, reviveMpsY);

            player.OutboundSink.SendSelfPositionSync(player);
            BroadcastCompactSyncToNeighbors(player);
            return;
        }

        if (_clusterRouter.TryTransferPlayerAcrossMaps(
            player,
            reviveMapId,
            reviveMpsX,
            reviveMpsY,
            arrivalFightMode: 0))
        {
            RemovePlayerFromZoneMemory(player);
            if (_characterAppService is not null)
            {
                _ = await _characterAppService.UpdateCharacterWorldSnapshotAsync(
                    BuildWorldSnapshotDto(
                        player,
                        reviveMapId,
                        reviveMpsX,
                        reviveMpsY,
                        fightMode: 0),
                    cancellationToken);
            }
        }
    }

    private PlayerItemSlot CreatePlayerItemSlotFromDto(uint runtimeItemId, CharacterItemDto dto)
    {
        ItemTemplate? tpl = null;
        _itemTemplateCatalog?.TryGetItem(dto.Genre, dto.DetailType, dto.ParticularType, dto.Level, out tpl);
        if (tpl is null && _itemGenerator is not null)
        {
            tpl = dto.Genre switch
            {
                ItemGenreKind.TownPortal => _itemGenerator.GenerateTownPortal(dto.ParticularType, dto.StackCount).Template,
                ItemGenreKind.Medicine => _itemGenerator.GenerateMedicine(dto.DetailType, dto.ParticularType, dto.Level, dto.StackCount).Template,
                _ => _itemGenerator.GenerateEquipment(dto.DetailType, dto.ParticularType, dto.Series, dto.Level, [], dto.Luck, dto.RandomSeed).Template,
            };
        }

        tpl ??= new ItemTemplate(
            Id: 90000 + dto.ParticularType,
            Code: $"item-{dto.Genre}-{dto.DetailType}-{dto.ParticularType}",
            Name: $"Vật Phẩm {dto.ParticularType}",
            Genre: dto.Genre,
            Quality: dto.Quality,
            DetailType: dto.DetailType,
            ParticularType: dto.ParticularType,
            Level: Math.Max((byte)1, dto.Level),
            ElementSeries: dto.Series,
            GridWidthCells: 1,
            GridHeightCells: 1,
            ObjDataId: 2,
            AppearanceResId: 1,
            PriceGold: 50,
            MaxStackCount: 50,
            BaseAttributes: [],
            Requirements: []);

        var affixes = new List<ItemMagicAttributeSlot>(dto.MagicAttributes.Count);
        foreach (CharacterItemMagicAttributeDto ma in dto.MagicAttributes)
        {
            affixes.Add(new ItemMagicAttributeSlot(
                ma.SlotIndex,
                ma.AttributeId,
                ma.Value0,
                ma.Value1,
                ma.Value2,
                ma.EnchaseLevel));
        }

        return new PlayerItemSlot(
            runtimeItemId: runtimeItemId,
            domainItemId: dto.Id,
            template: tpl,
            genre: dto.Genre,
            detailType: dto.DetailType,
            particularType: dto.ParticularType,
            level: dto.Level,
            series: dto.Series,
            quality: dto.Quality,
            randomSeed: dto.RandomSeed,
            generatorVersion: dto.GeneratorVersion,
            luck: dto.Luck,
            currentDurability: dto.CurrentDurability,
            maxDurability: dto.MaxDurability,
            stackCount: dto.StackCount,
            containerPlace: dto.ContainerPlace,
            gridX: dto.GridX,
            gridY: dto.GridY,
            magicAttributes: affixes,
            bindState: dto.BindState,
            expireTimeUnixSeconds: dto.ExpireTimeUnixSeconds,
            creatorName: dto.CreatorName);
    }

    private void BroadcastGroundObjectAddToAoi(GroundObjectSlot obj)
    {
        GroundObjectSyncSnapshot snap = obj.CreateSyncSnapshot();
        int regionCount = _regionGrid.GetSurroundingRegions(obj.RegionX, obj.RegionY, _aoiSlotBuffer);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = _aoiSlotBuffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                players[pIdx].OutboundSink.SendGroundObjectAdd(in snap);
            }
        }
    }

    private void BroadcastGroundObjectRemoveToAoi(GroundObjectSlot obj, byte reason)
    {
        int regionCount = _regionGrid.GetSurroundingRegions(obj.RegionX, obj.RegionY, _aoiSlotBuffer);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = _aoiSlotBuffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                players[pIdx].OutboundSink.SendGroundObjectRemove(obj.ObjId, reason);
            }
        }
    }

    private void HandlePlayerDeath(PlayerZoneState player, uint killerNpcId)
    {
        player.CurrentLife = 0;
        player.Doing = EntityDoingPosture.Death;
        player.TargetMpsX = player.CurrentMpsX;
        player.TargetMpsY = player.CurrentMpsY;
        player.StatusEffects.ClearAll();

        player.OutboundSink.SendNormalVitalSync(player);
        BroadcastNpcDeathToAoi(player.RegionX, player.RegionY, player.RuntimeNpcId);

        ZoneEngineLogMessages.LogEntityDefeated(
            _logger,
            player.RuntimeNpcId,
            killerNpcId,
            _mapDescriptor.MapId,
            0,
            0);
    }

    private void AwardPlayerExperience(PlayerZoneState player, int expGain)
    {
        if (expGain <= 0 || player.CurrentLife <= 0)
        {
            return;
        }

        player.Experience += expGain;
        int syncExp = (int)Math.Clamp(player.Experience % CharacterConsts.ExpHighDivisor, 0, int.MaxValue);
        player.OutboundSink.SendPlayerExpSync(syncExp);

        PlayerSeriesGrowthRuleTemplate growth = _playerTemplateCatalog?.GetSeriesGrowthRule(player.Series)
            ?? new PlayerSeriesGrowthRuleTemplate(player.Series, 3, 8, 9, 2, 6, 1);

        bool leveledUp = false;
        while (player.Level < 200)
        {
            long reqExp = _playerTemplateCatalog?.GetRequiredExpForNextLevel(player.Level)
                ?? (player.Level * 100L);
            if (reqExp <= 0 || player.Experience < reqExp)
            {
                break;
            }

            player.Experience -= reqExp;
            player.Level = (byte)Math.Min(200, player.Level + 1);
            player.UnallocatedAttributePoints = (ushort)Math.Min(ushort.MaxValue, player.UnallocatedAttributePoints + Math.Max(1, growth.AttributePointsPerLevel));
            player.UnallocatedSkillPoints = (ushort)Math.Min(ushort.MaxValue, player.UnallocatedSkillPoints + Math.Max(1, growth.SkillPointsPerLevel));

            player.MaxLife = (ushort)Math.Clamp(player.MaxLife + Math.Max(1, growth.LifePerLevel), 1, ushort.MaxValue);
            player.MaxMana = (ushort)Math.Clamp(player.MaxMana + Math.Max(1, growth.ManaPerLevel), 1, ushort.MaxValue);
            player.MaxStamina = (ushort)Math.Clamp(player.MaxStamina + Math.Max(1, growth.StaminaPerLevel), 1, ushort.MaxValue);
            player.CurrentLife = player.MaxLife;
            player.CurrentMana = player.MaxMana;
            player.CurrentStamina = player.MaxStamina;

            int newSyncExp = (int)Math.Clamp(player.Experience % CharacterConsts.ExpHighDivisor, 0, int.MaxValue);
            player.OutboundSink.SendPlayerLevelUpSync(
                player.Level,
                player.UnallocatedAttributePoints,
                player.UnallocatedSkillPoints,
                newSyncExp);

            ZoneEngineLogMessages.LogPlayerLeveledUp(
                _logger,
                player.RoleNameUnicode,
                player.RuntimeNpcId,
                player.Level,
                _mapDescriptor.MapId);
            leveledUp = true;
        }

        if (leveledUp)
        {
            player.OutboundSink.SendNormalVitalSync(player);
        }
    }

    private void HandleAllocateBaseAttribute(AllocateBaseAttributeZoneCommand cmd, long tick)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? player) ||
            player.LeaseEpoch != cmd.LeaseEpoch ||
            !player.IsSyncCompleted ||
            cmd.PointsToAdd <= 0 ||
            player.UnallocatedAttributePoints < cmd.PointsToAdd)
        {
            return;
        }

        player.LastPingReceivedTick = tick;
        ushort points = (ushort)Math.Min((int)player.UnallocatedAttributePoints, cmd.PointsToAdd);
        player.UnallocatedAttributePoints -= points;

        PlayerSeriesGrowthRuleTemplate growth = _playerTemplateCatalog?.GetSeriesGrowthRule(player.Series)
            ?? new PlayerSeriesGrowthRuleTemplate(player.Series, 3, 8, 9, 2, 6, 1);

        int baseVal;
        int curVal;
        switch (cmd.Attribute)
        {
            case BaseAttributeKind.Strength:
                player.Strength = (ushort)Math.Clamp(player.Strength + points, 0, ushort.MaxValue);
                baseVal = player.Strength;
                curVal = player.EffectiveStrength;
                break;

            case BaseAttributeKind.Dexterity:
                player.Dexterity = (ushort)Math.Clamp(player.Dexterity + points, 0, ushort.MaxValue);
                baseVal = player.Dexterity;
                curVal = player.EffectiveDexterity;
                break;

            case BaseAttributeKind.Vitality:
                player.Vitality = (ushort)Math.Clamp(player.Vitality + points, 0, ushort.MaxValue);
                int lifeAdd = points * Math.Max(1, growth.LifePerVitality);
                int stamAdd = points * Math.Max(1, growth.StaminaPerVitality);
                player.MaxLife = (ushort)Math.Clamp(player.MaxLife + lifeAdd, 1, ushort.MaxValue);
                player.CurrentLife = Math.Min(player.MaxLife, player.CurrentLife + lifeAdd);
                player.MaxStamina = (ushort)Math.Clamp(player.MaxStamina + stamAdd, 1, ushort.MaxValue);
                player.CurrentStamina = (ushort)Math.Min(player.MaxStamina, player.CurrentStamina + stamAdd);
                baseVal = player.Vitality;
                curVal = player.EffectiveVitality;
                player.OutboundSink.SendNormalVitalSync(player);
                break;

            case BaseAttributeKind.Energy:
            default:
                player.Energy = (ushort)Math.Clamp(player.Energy + points, 0, ushort.MaxValue);
                int manaAdd = points * Math.Max(1, growth.ManaPerEnergy);
                player.MaxMana = (ushort)Math.Clamp(player.MaxMana + manaAdd, 1, ushort.MaxValue);
                player.CurrentMana = Math.Min(player.MaxMana, player.CurrentMana + manaAdd);
                baseVal = player.Energy;
                curVal = player.EffectiveEnergy;
                player.OutboundSink.SendNormalVitalSync(player);
                break;
        }

        player.OutboundSink.SendPlayerAttributeSync(
            (byte)cmd.Attribute,
            baseVal,
            curVal,
            player.UnallocatedAttributePoints);
    }

    private void HandleAllocateSkillPoint(AllocateSkillPointZoneCommand cmd, long tick)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? player) ||
            player.LeaseEpoch != cmd.LeaseEpoch ||
            !player.IsSyncCompleted ||
            cmd.PointsToAdd <= 0 ||
            player.UnallocatedSkillPoints < cmd.PointsToAdd)
        {
            return;
        }

        player.LastPingReceivedTick = tick;
        if (!player.TryGetLearnedSkillLevel(cmd.SkillId, out ushort currentLevel) || currentLevel == 0)
        {
            return;
        }

        int maxLevel = 20;
        if (_skillTemplateCatalog is not null &&
            _skillTemplateCatalog.TryGetSkill(cmd.SkillId, out SkillTemplate? skillTpl) &&
            skillTpl is not null)
        {
            maxLevel = Math.Max(1, skillTpl.MaxLevel);
        }

        int points = Math.Min(cmd.PointsToAdd, maxLevel - currentLevel);
        if (points <= 0)
        {
            return;
        }

        ushort newLevel = (ushort)(currentLevel + points);
        player.UnallocatedSkillPoints -= (ushort)points;
        player.SetLearnedSkill(cmd.SkillId, newLevel);

        player.OutboundSink.SendPlayerSkillLevelSync(
            cmd.SkillId,
            newLevel,
            0,
            (short)player.UnallocatedSkillPoints);
    }

    private async ValueTask HandleRevivePlayerAsync(
        RevivePlayerZoneCommand cmd,
        long tick,
        CancellationToken cancellationToken)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? player) ||
            player.LeaseEpoch != cmd.LeaseEpoch ||
            !player.IsSyncCompleted ||
            (player.CurrentLife > 0 && player.Doing != EntityDoingPosture.Death))
        {
            return;
        }

        player.LastPingReceivedTick = tick;

        ushort reviveMapId = (ushort)(player.RevivalMapId > 0 ? player.RevivalMapId : CharacterConsts.DefaultSpawnMapId);
        int reviveMpsX = 52000;
        int reviveMpsY = 101696;
        if (_playerTemplateCatalog is not null &&
            _playerTemplateCatalog.TryGetStarterVillage(reviveMapId, out StarterVillageTemplate? village) &&
            village is not null)
        {
            reviveMpsX = village.SpawnMpsX;
            reviveMpsY = village.SpawnMpsY;
        }

        player.CurrentLife = player.MaxLife;
        player.CurrentMana = player.MaxMana;
        player.CurrentStamina = player.MaxStamina;
        player.Doing = EntityDoingPosture.Stand;
        player.FightMode = 0;
        player.StatusEffects.ClearAll();

        if (reviveMapId == _mapDescriptor.MapId || _clusterRouter is null)
        {
            MovePlayerCoordinateAndUpdateRegion(player, reviveMpsX, reviveMpsY);
            player.TargetMpsX = reviveMpsX;
            player.TargetMpsY = reviveMpsY;
            player.LastTriggeredTrapId = GetCellTrapId(reviveMpsX, reviveMpsY);

            player.OutboundSink.SendNormalVitalSync(player);
            player.OutboundSink.SendSelfPositionSync(player);
            BroadcastNpcReviveToAoi(player.RegionX, player.RegionY, player.RuntimeNpcId, (byte)cmd.ReviveType);
            BroadcastCompactSyncToNeighbors(player);
            return;
        }

        if (_clusterRouter.TryTransferPlayerAcrossMaps(
            player,
            reviveMapId,
            reviveMpsX,
            reviveMpsY,
            arrivalFightMode: 0))
        {
            RemovePlayerFromZoneMemory(player);
            if (_characterAppService is not null)
            {
                _ = await _characterAppService.UpdateCharacterWorldSnapshotAsync(
                    BuildWorldSnapshotDto(
                        player,
                        reviveMapId,
                        reviveMpsX,
                        reviveMpsY,
                        fightMode: 0),
                    cancellationToken);
            }
        }
    }

    private void OnNpcRespawned(NpcZoneState npc)
    {
        int regionCount = _regionGrid.GetSurroundingRegions(npc.RegionX, npc.RegionY, _aoiSlotBuffer);
        EntityFullSyncSnapshot npcSnap = CreateNpcSnapshot(npc);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = _aoiSlotBuffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                players[pIdx].OutboundSink.SendNpcFullSync(in npcSnap);
            }
        }
    }

    private void OnNpcCastSkill(NpcZoneState npc, PlayerZoneState target, int skillId, int skillLevel, int targetParam2) =>
        BroadcastSkillCastToAoi(
            npc.RegionX,
            npc.RegionY,
            npc.NpcId,
            (uint)skillId,
            (uint)skillLevel,
            -1,
            targetParam2,
            castDirectly: false);

    private void BroadcastNpcDeathToAoi(int regionX, int regionY, uint npcId)
    {
        int regionCount = _regionGrid.GetSurroundingRegions(regionX, regionY, _aoiSlotBuffer);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = _aoiSlotBuffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                players[pIdx].OutboundSink.SendNpcDeathBroadcast(npcId);
            }
        }
    }

    private void BroadcastNpcReviveToAoi(int regionX, int regionY, uint npcId, byte reviveType)
    {
        int regionCount = _regionGrid.GetSurroundingRegions(regionX, regionY, _aoiSlotBuffer);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = _aoiSlotBuffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                players[pIdx].OutboundSink.SendNpcReviveBroadcast(npcId, reviveType);
            }
        }
    }

    private void HandleChangeAuraSkill(ChangeAuraSkillZoneCommand cmd, long tick)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? player) ||
            player.LeaseEpoch != cmd.LeaseEpoch ||
            !player.IsSyncCompleted)
        {
            return;
        }

        player.LastPingReceivedTick = tick;
        if (cmd.AuraSkillId == 0)
        {
            player.ActiveAuraSkillId = 0;
            return;
        }

        if (!player.TryGetLearnedSkillLevel(cmd.AuraSkillId, out ushort level) || level == 0)
        {
            return;
        }

        if (_skillTemplateCatalog is not null &&
            _skillTemplateCatalog.TryGetSkill(cmd.AuraSkillId, out SkillTemplate? skillTpl) &&
            skillTpl is not null &&
            !skillTpl.IsAura)
        {
            return;
        }

        player.ActiveAuraSkillId = cmd.AuraSkillId;
    }

    private void HandleChangePkMode(ChangePkModeZoneCommand cmd, long tick)
    {
        if (!_playersByCharacterId.TryGetValue(cmd.CharacterId, out PlayerZoneState? player) ||
            player.LeaseEpoch != cmd.LeaseEpoch ||
            !player.IsSyncCompleted)
        {
            return;
        }

        player.LastPingReceivedTick = tick;

        if (!_mapDescriptor.AllowPkChange)
        {
            player.OutboundSink.SendPkNormalFlagSync((byte)player.PkState);
            return;
        }

        if (cmd.RequestedPkMode == PlayerPkMode.Peace &&
            player.PkState != PlayerPkMode.Peace &&
            player.LastPkModeChangeTick > 0 &&
            (tick - player.LastPkModeChangeTick) < CombatConsts.SwitchToPeaceCooldownFrames)
        {
            player.OutboundSink.SendPkNormalFlagSync((byte)player.PkState);
            return;
        }

        player.PkState = cmd.RequestedPkMode;
        player.LastPkModeChangeTick = tick;
        player.OutboundSink.SendPkNormalFlagSync((byte)player.PkState);
    }

    private void ProcessStatusEffectsTick()
    {
        foreach (PlayerZoneState player in _playersByCharacterId.Values)
        {
            if (player.CurrentLife <= 0 || player.Doing == EntityDoingPosture.Death)
            {
                continue;
            }

            EntityStatusEffectState fx = player.StatusEffects;
            if (fx.FreezeRemainingFrames > 0)
            {
                fx.FreezeRemainingFrames--;
            }

            if (fx.StunRemainingFrames > 0)
            {
                fx.StunRemainingFrames--;
            }

            if (fx.HurtRecoveryRemainingFrames > 0)
            {
                fx.HurtRecoveryRemainingFrames--;
            }

            if (fx.IsPoisoned)
            {
                fx.PoisonRemainingFrames--;
                fx.PoisonTicksSinceLastHit++;
                if (fx.PoisonTicksSinceLastHit >= Math.Max(1, fx.PoisonIntervalFrames))
                {
                    fx.PoisonTicksSinceLastHit = 0;
                    ApplyDamageToPlayer(fx.LastPoisonAttackerNpcId, player, fx.PoisonDamagePerTick, doHurtAnimation: false);
                }
            }
        }

        foreach (NpcZoneState npc in _npcsById.Values)
        {
            if (npc.CurLife == 0 || npc.AiState == MonsterAiStateKind.DeadRespawnWait)
            {
                continue;
            }

            EntityStatusEffectState fx = npc.StatusEffects;
            if (fx.FreezeRemainingFrames > 0)
            {
                fx.FreezeRemainingFrames--;
            }

            if (fx.StunRemainingFrames > 0)
            {
                fx.StunRemainingFrames--;
            }

            if (fx.HurtRecoveryRemainingFrames > 0)
            {
                fx.HurtRecoveryRemainingFrames--;
            }

            if (fx.IsPoisoned)
            {
                fx.PoisonRemainingFrames--;
                fx.PoisonTicksSinceLastHit++;
                if (fx.PoisonTicksSinceLastHit >= Math.Max(1, fx.PoisonIntervalFrames))
                {
                    fx.PoisonTicksSinceLastHit = 0;
                    ApplyDamageToNpc(fx.LastPoisonAttackerNpcId, npc, fx.PoisonDamagePerTick, doHurtAnimation: false);
                }
            }
        }
    }

    private void BroadcastSkillCastToAoi(
        int regionX,
        int regionY,
        uint casterNpcId,
        uint skillId,
        uint skillLevel,
        int targetMpsX,
        int targetMpsYOrNpcId,
        bool castDirectly)
    {
        int regionCount = _regionGrid.GetSurroundingRegions(regionX, regionY, _aoiSlotBuffer);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = _aoiSlotBuffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                players[pIdx].OutboundSink.SendSkillCastBroadcast(
                    casterNpcId,
                    skillId,
                    skillLevel,
                    targetMpsX,
                    targetMpsYOrNpcId,
                    castDirectly);
            }
        }
    }

    private void BroadcastHurtToAoi(int regionX, int regionY, uint npcId, int mpsX, int mpsY, int hurtFrames)
    {
        int regionCount = _regionGrid.GetSurroundingRegions(regionX, regionY, _aoiSlotBuffer);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = _aoiSlotBuffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                players[pIdx].OutboundSink.SendHurtBroadcast(npcId, mpsX, mpsY, hurtFrames);
            }
        }
    }

    private void BroadcastNpcCompactSyncToAoi(NpcZoneState npc)
    {
        int regionCount = _regionGrid.GetSurroundingRegions(npc.RegionX, npc.RegionY, _aoiSlotBuffer);
        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = _aoiSlotBuffer[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                players[pIdx].OutboundSink.SendNpcCompactSync(
                    npc.NpcId,
                    npc.MpsX,
                    npc.MpsY,
                    npc.Doing,
                    npc.LifePercent,
                    (byte)npc.Camp,
                    npc.Dir);
            }
        }
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
        RemovePlayerFromZoneMemory(state);

        ZoneEngineLogMessages.LogPlayerDetached(
            _logger,
            state.RoleNameUnicode,
            state.CharacterId,
            state.RuntimeNpcId,
            state.LeaseEpoch,
            state.MapId,
            reason);

        UpdateCharacterWorldSnapshotDto snapshot = BuildWorldSnapshotDto(
            state,
            state.MapId,
            state.CurrentMpsX,
            state.CurrentMpsY,
            state.FightMode);

        await _sessionLeaseAppService.CompleteLogoutOrEvictAsync(
            new ReleaseSessionLeaseInput(state.AccountId, state.LeaseEpoch, snapshot),
            cancellationToken);

        if (reason is PlayerDetachReason.HeartbeatTimeout or PlayerDetachReason.LeaseSuperseded)
        {
            state.OutboundSink.Disconnect(reason);
        }
    }

    private static UpdateCharacterWorldSnapshotDto BuildWorldSnapshotDto(
        PlayerZoneState state,
        ushort mapId,
        int mapMpsX,
        int mapMpsY,
        byte fightMode)
    {
        var skills = new List<CharacterSkillDto>(state.LearnedSkills.Count);
        foreach (KeyValuePair<int, ushort> kvp in state.LearnedSkills)
        {
            if (kvp.Key > 0 && kvp.Key <= ushort.MaxValue && kvp.Value > 0 && kvp.Value <= byte.MaxValue)
            {
                skills.Add(new CharacterSkillDto((ushort)kvp.Key, (byte)kvp.Value, 0));
            }
        }

        return new UpdateCharacterWorldSnapshotDto(
            CharacterId: state.CharacterId,
            MapId: mapId,
            MapMpsX: mapMpsX,
            MapMpsY: mapMpsY,
            CurrentLife: state.CurrentLife,
            CurrentMana: state.CurrentMana,
            CurrentStamina: state.CurrentStamina,
            LeaseEpoch: state.LeaseEpoch,
            FightMode: fightMode,
            Level: state.Level,
            Experience: state.Experience,
            LeadExperience: state.LeadExperience,
            Strength: state.Strength,
            Dexterity: state.Dexterity,
            Vitality: state.Vitality,
            Energy: state.Energy,
            UnallocatedAttributePoints: state.UnallocatedAttributePoints,
            UnallocatedSkillPoints: state.UnallocatedSkillPoints,
            MaxLife: state.MaxLife,
            MaxMana: state.MaxMana,
            MaxStamina: state.MaxStamina,
            InventoryMoney: state.InventoryMoney,
            RepositoryMoney: state.RepositoryMoney,
            PkState: state.PkState,
            PkValue: state.PkValue,
            ActiveAuraSkillId: (ushort)Math.Clamp(state.ActiveAuraSkillId, 0, ushort.MaxValue),
            Skills: skills,
            Items: state.Inventory.ToCharacterItemDtos());
    }

    private static string NormalizeRoleName(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Normalize(NormalizationForm.FormC);
}
