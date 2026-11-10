using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Channels;
using JXServer.Adapter.LegacyJX1.Framing;
using JXServer.Adapter.LegacyJX1.Logging;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Contracts.Characters;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.WorldZones;
using Microsoft.Extensions.Logging;

namespace JXServer.Adapter.LegacyJX1.GameServer;

/// <summary>
/// Represents an active client TCP session connected to the GameServer (<c>6666</c>) and implements <see cref="IPlayerOutboundSink"/>.
/// </summary>
public sealed class GameServerClientSession : IPlayerOutboundSink, IAsyncDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly IReadOnlyDictionary<byte, IGameServerPacketHandler> _handlers;
    private readonly IZoneClusterRouter _zoneClusterRouter;
    private readonly ILogger _logger;
    private readonly JxFramePipeProcessor _frameProcessor = new();
    private readonly Channel<byte[]> _outboundChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private readonly CancellationTokenSource _cts = new();

    private NetworkStream? _stream;
    private int _disposed;
    private int _cleanLogoutCompleted;

    public string ConnectionId { get; } = Guid.NewGuid().ToString("N");

    public Guid? AccountId { get; private set; }

    public Guid? CharacterId { get; private set; }

    public ushort? AttachedMapId { get; private set; }

    public long LeaseEpoch { get; private set; }

    public uint ClientToken1 { get; private set; }

    public uint ClientToken2 { get; private set; }

    public bool IsAttachedToWorld => AccountId.HasValue && CharacterId.HasValue && AttachedMapId.HasValue && LeaseEpoch > 0;

    public GameServerClientSession(
        TcpClient tcpClient,
        IReadOnlyDictionary<byte, IGameServerPacketHandler> handlers,
        IZoneClusterRouter zoneClusterRouter,
        ILogger logger)
    {
        _tcpClient = tcpClient;
        _handlers = handlers;
        _zoneClusterRouter = zoneClusterRouter;
        _logger = logger;
    }

    public void SetWorldAttached(Guid accountId, Guid characterId, ushort mapId, long leaseEpoch)
    {
        AccountId = accountId;
        CharacterId = characterId;
        AttachedMapId = mapId;
        LeaseEpoch = leaseEpoch;
    }

    public void SetClientSyncTokens(uint token1, uint token2)
    {
        ClientToken1 = token1;
        ClientToken2 = token2;
    }

    public void MarkCleanLogoutCompleted() =>
        Interlocked.Exchange(ref _cleanLogoutCompleted, 1);

    public async Task RunAsync(CancellationToken serverStoppingToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(serverStoppingToken, _cts.Token);
        CancellationToken ct = linkedCts.Token;

        Task? writerTask = null;
        try
        {
            _tcpClient.NoDelay = true;
            _stream = _tcpClient.GetStream();

            uint clientKey = GenerateNonZeroKey();
            uint serverKey = GenerateNonZeroKey();

            var handshakeWriter = new ArrayBufferWriter<byte>(64);
            _frameProcessor.WriteHandshakeFrame(handshakeWriter, clientKey, serverKey);
            await _stream.WriteAsync(handshakeWriter.WrittenMemory, ct);
            await _stream.FlushAsync(ct);

            writerTask = Task.Run(() => PumpOutboundFramesAsync(ct), ct);

            var reader = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
            while (!ct.IsCancellationRequested)
            {
                ReadResult readResult = await reader.ReadAsync(ct);
                ReadOnlySequence<byte> buffer = readResult.Buffer;

                bool keepConnection = true;
                while (_frameProcessor.TryReadC2SFrame(ref buffer, out byte[] payload))
                {
                    if (!ProtocolSizeTable.IsValidGameServerC2SPayload(payload) ||
                        !_handlers.TryGetValue(payload[0], out IGameServerPacketHandler? handler))
                    {
                        LegacyAdapterLogMessages.LogMalformedOrUnauthorizedPacket(
                            _logger,
                            ConnectionId,
                            payload.Length > 0 ? payload[0] : (byte)0,
                            payload.Length);
                        keepConnection = false;
                        break;
                    }

                    if (!await handler.HandleAsync(this, payload, ct))
                    {
                        keepConnection = false;
                        break;
                    }
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (!keepConnection || readResult.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }
        finally
        {
            _outboundChannel.Writer.TryComplete();
            if (writerTask is not null)
            {
                try
                {
                    await writerTask;
                }
                catch
                {
                }
            }

            if (IsAttachedToWorld &&
                Volatile.Read(ref _cleanLogoutCompleted) == 0 &&
                CharacterId.HasValue &&
                AttachedMapId.HasValue)
            {
                _zoneClusterRouter.TryDispatchToZone(
                    AttachedMapId.Value,
                    new DetachPlayerZoneCommand(CharacterId.Value, LeaseEpoch, PlayerDetachReason.SocketClosed));
            }

            await DisposeAsync();
        }
    }

    public void SendInitialWorldSyncBurst(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick,
        IReadOnlyList<CharacterSkillDto> skills)
    {
        var writer = new ArrayBufferWriter<byte>(1024);

        SyncWorldS2CPacket worldSync = SyncWorldS2CPacket.Create(
            playerState.MapId,
            playerState.CurrentMpsX,
            playerState.CurrentMpsY,
            currentFrameTick,
            mapNameUnicode);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in worldSync);

        SyncCurPlayerS2CPacket curPlayer = SyncCurPlayerS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in curPlayer);

        SyncCurPlayerSkillS2CPacket skillSync = SyncCurPlayerSkillS2CPacket.Create(skills);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in skillSync);

        SyncCurPlayerNormalS2CPacket normalSync = SyncCurPlayerNormalS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in normalSync);

        SyncNpcFullS2CPacket selfNpc = SyncNpcFullS2CPacket.CreateFromPlayer(playerState);
        _frameProcessor.WriteEncryptedS2CFrame(writer, selfNpc.ToPayloadBytes());

        DisabledFuncFlagS2CPacket disabledFlags = DisabledFuncFlagS2CPacket.Create(0);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in disabledFlags);

        SyncEndS2CPacket syncEnd = SyncEndS2CPacket.Create();
        _frameProcessor.WriteEncryptedS2CStruct(writer, in syncEnd);

        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendActiveWorldConfirmation(PlayerZoneState playerState)
    {
        var writer = new ArrayBufferWriter<byte>(64);

        SyncCurPlayerNormalS2CPacket normalSync = SyncCurPlayerNormalS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in normalSync);

        SyncNpcMinPlayerS2CPacket minPlayer = SyncNpcMinPlayerS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in minPlayer);

        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendServerPing(uint serverTimestamp)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        PingS2CPacket ping = PingS2CPacket.Create(serverTimestamp);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in ping);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendServerPingReply(uint echoedClientTimestamp)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        PingReplyS2CPacket reply = PingReplyS2CPacket.Create(echoedClientTimestamp);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in reply);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendNpcFullSync(in EntityFullSyncSnapshot snapshot)
    {
        var writer = new ArrayBufferWriter<byte>(256);
        SyncNpcFullS2CPacket fullSync = SyncNpcFullS2CPacket.CreateFromSnapshot(in snapshot);
        _frameProcessor.WriteEncryptedS2CFrame(writer, fullSync.ToPayloadBytes());
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendNpcCompactSync(
        uint npcId,
        int mpsX,
        int mpsY,
        EntityDoingPosture doing,
        byte currentLifePercent,
        byte camp,
        byte isPlayerFlag)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var minSync = new SyncNpcMinS2CPacket(
            npcId,
            mpsX,
            mpsY,
            (byte)doing,
            currentLifePercent,
            State: 0,
            camp,
            isPlayerFlag);
        Span<byte> payload = stackalloc byte[SyncNpcMinS2CPacket.PayloadSize];
        minSync.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendSelfPositionSync(PlayerZoneState playerState)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        SyncNpcMinPlayerS2CPacket minPlayer = SyncNpcMinPlayerS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in minPlayer);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendNormalVitalSync(PlayerZoneState playerState)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        SyncCurPlayerNormalS2CPacket normalSync = SyncCurPlayerNormalS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in normalSync);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendWalkBroadcast(uint npcId, int destMpsX, int destMpsY)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var walk = new NpcWalkS2CPacket(npcId, destMpsX, destMpsY);
        Span<byte> payload = stackalloc byte[NpcWalkS2CPacket.PayloadSize];
        walk.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendRunBroadcast(uint npcId, int destMpsX, int destMpsY)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var run = new NpcRunS2CPacket(npcId, destMpsX, destMpsY);
        Span<byte> payload = stackalloc byte[NpcRunS2CPacket.PayloadSize];
        run.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendJumpBroadcast(uint npcId, int destMpsX, int destMpsY)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var jump = new NpcJumpS2CPacket(npcId, destMpsX, destMpsY);
        Span<byte> payload = stackalloc byte[NpcJumpS2CPacket.PayloadSize];
        jump.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendSitBroadcast(uint npcId)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var sit = new NpcSitS2CPacket(npcId);
        Span<byte> payload = stackalloc byte[NpcSitS2CPacket.PayloadSize];
        sit.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendWorldMapTransitionSync(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick)
    {
        AttachedMapId = playerState.MapId;

        var writer = new ArrayBufferWriter<byte>(256);
        SyncWorldS2CPacket worldSync = SyncWorldS2CPacket.Create(
            playerState.MapId,
            playerState.CurrentMpsX,
            playerState.CurrentMpsY,
            currentFrameTick,
            mapNameUnicode);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in worldSync);

        SyncNpcMinPlayerS2CPacket minPlayer = SyncNpcMinPlayerS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in minPlayer);

        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendNotifyServerExchange(Guid handoffTicketGuid, uint ipAddressLittleEndian, ushort port)
    {
        var writer = new ArrayBufferWriter<byte>(64);
        var exchange = new NotifyPlayerExchangeS2CPacket(
            handoffTicketGuid,
            ipAddressLittleEndian,
            port);
        Span<byte> payload = stackalloc byte[NotifyPlayerExchangeS2CPacket.PayloadSize];
        exchange.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPlayerTalkBroadcast(uint talkerNpcId, string messageUtf8)
    {
        var writer = new ArrayBufferWriter<byte>(320);
        var talk = new PlayerTalkS2CPacket(talkerNpcId, messageUtf8);
        _frameProcessor.WriteEncryptedS2CFrame(writer, talk.ToPayloadBytes());
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendChatMessage(uint senderNpcId, string senderNameUtf8, string messageUtf8)
    {
        var writer = new ArrayBufferWriter<byte>(352);
        var chat = new ChatGetSomeoneS2CPacket(senderNpcId, senderNameUtf8, messageUtf8);
        _frameProcessor.WriteEncryptedS2CFrame(writer, chat.ToPayloadBytes());
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendSystemMessage(string messageUtf8)
    {
        var writer = new ArrayBufferWriter<byte>(320);
        var sysMsg = new SystemInfoExtendS2CPacket(messageUtf8);
        _frameProcessor.WriteEncryptedS2CFrame(writer, sysMsg.ToPayloadBytes());
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendSkillCastBroadcast(
        uint casterNpcId,
        uint skillId,
        uint skillLevel,
        int targetMpsX,
        int targetMpsYOrNpcId,
        bool castDirectly = false)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        Span<byte> payload = stackalloc byte[SkillCastS2CPacket.PayloadSize];
        if (castDirectly)
        {
            var pkt = new CastSkillDirectlyS2CPacket(targetMpsX, skillId, targetMpsYOrNpcId, casterNpcId, skillLevel);
            pkt.WritePayload(payload);
        }
        else
        {
            var pkt = new SkillCastS2CPacket(targetMpsX, skillId, targetMpsYOrNpcId, casterNpcId, skillLevel);
            pkt.WritePayload(payload);
        }

        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendHurtBroadcast(uint npcId, int mpsX, int mpsY, int hurtFrames)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        BroadcastNetCommandS2CPacket hurt = BroadcastNetCommandS2CPacket.CreateHurt(npcId, mpsX, mpsY, hurtFrames);
        Span<byte> payload = stackalloc byte[BroadcastNetCommandS2CPacket.PayloadSize];
        hurt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPkNormalFlagSync(byte pkMode)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new PkSyncNormalFlagS2CPacket((JXServer.Primitives.Combat.PlayerPkMode)pkMode);
        Span<byte> payload = stackalloc byte[PkSyncNormalFlagS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendNpcDeathBroadcast(uint npcId)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new NpcDeathS2CPacket(npcId);
        Span<byte> payload = stackalloc byte[NpcDeathS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendNpcReviveBroadcast(uint npcId, byte reviveType = 0)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new NpcReviveS2CPacket(npcId, reviveType);
        Span<byte> payload = stackalloc byte[NpcReviveS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPlayerExpSync(int currentExp)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new PlayerExpS2CPacket(currentExp);
        Span<byte> payload = stackalloc byte[PlayerExpS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPlayerLevelUpSync(int newLevel, int unallocatedAttrPoints, int unallocatedSkillPoints, int currentExp)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var pkt = new PlayerLevelUpS2CPacket(newLevel, unallocatedAttrPoints, unallocatedSkillPoints, currentExp);
        Span<byte> payload = stackalloc byte[PlayerLevelUpS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPlayerAttributeSync(byte attributeKind, int baseValue, int currentValue, int unallocatedAttrPoints)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var pkt = new PlayerAttributeS2CPacket(
            (JXServer.Primitives.Combat.BaseAttributeKind)attributeKind,
            baseValue,
            currentValue,
            unallocatedAttrPoints);
        Span<byte> payload = stackalloc byte[PlayerAttributeS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPlayerSkillLevelSync(int skillId, int skillLevel, int skillExp, short unallocatedSkillPoints)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var pkt = new PlayerSkillLevelS2CPacket(skillId, skillLevel, skillExp, unallocatedSkillPoints);
        Span<byte> payload = stackalloc byte[PlayerSkillLevelS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendGroundObjectAdd(in GroundObjectSyncSnapshot snapshot)
    {
        var writer = new ArrayBufferWriter<byte>(128);
        byte[] payload = ObjAddS2CPacket.CreatePayloadBytes(
            snapshot.Dir,
            snapshot.ObjId,
            snapshot.DataId,
            snapshot.MpsX,
            snapshot.MpsY,
            snapshot.MoneyNum,
            snapshot.ItemId,
            state: 0,
            snapshot.ItemWidth,
            snapshot.Genre,
            snapshot.ItemHeight,
            snapshot.ColorId,
            flag: 0,
            snapshot.NameUtf8);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendGroundObjectRemove(int objId, byte reason)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new ObjRemoveS2CPacket(objId, reason);
        Span<byte> payload = stackalloc byte[ObjRemoveS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendItemSync(in PlayerItemSyncSnapshot snapshot)
    {
        var writer = new ArrayBufferWriter<byte>(96);
        SyncItemS2CPacket pkt = SyncItemS2CPacket.CreateFromSnapshot(in snapshot);
        Span<byte> payload = stackalloc byte[SyncItemS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendItemRemove(uint runtimeItemId)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new RemoveItemS2CPacket(runtimeItemId);
        Span<byte> payload = stackalloc byte[RemoveItemS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendMoneySync(int inventoryMoney, int repositoryMoney, int tradeMoney = 0)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var pkt = new SyncMoneyS2CPacket(inventoryMoney, repositoryMoney, tradeMoney);
        Span<byte> payload = stackalloc byte[SyncMoneyS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPlayerMoveItemConfirm(
        byte downY,
        JXServer.Primitives.Items.ItemContainerPlace downPos,
        byte downX,
        byte upY,
        byte upX,
        JXServer.Primitives.Items.ItemContainerPlace upPos)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new PlayerMoveItemS2CPacket(downY, downPos, downX, upY, upX, upPos);
        Span<byte> payload = stackalloc byte[PlayerMoveItemS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendItemAutoMoveUnlock()
    {
        var writer = new ArrayBufferWriter<byte>(8);
        var pkt = new ItemAutoMoveS2CPacket();
        Span<byte> payload = stackalloc byte[ItemAutoMoveS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void Disconnect(PlayerDetachReason reason)
    {
        try
        {
            _cts.Cancel();
            _tcpClient.Close();
        }
        catch
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Disconnect(PlayerDetachReason.SocketClosed);
            _cts.Dispose();
            _tcpClient.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private void EnqueueOutboundWireBytes(byte[] wireBytes) =>
        _outboundChannel.Writer.TryWrite(wireBytes);

    private async Task PumpOutboundFramesAsync(CancellationToken ct)
    {
        while (await _outboundChannel.Reader.WaitToReadAsync(ct))
        {
            while (_outboundChannel.Reader.TryRead(out byte[]? wireBytes))
            {
                if (_stream is not null)
                {
                    await _stream.WriteAsync(wireBytes, ct);
                }
            }

            if (_stream is not null)
            {
                await _stream.FlushAsync(ct);
            }
        }
    }

    private static uint GenerateNonZeroKey()
    {
        Span<byte> buf = stackalloc byte[4];
        uint key;
        do
        {
            RandomNumberGenerator.Fill(buf);
            key = BitConverter.ToUInt32(buf);
        }
        while (key == 0);
        return key;
    }
}
