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
public sealed partial class GameServerClientSession : IPlayerOutboundSink, IAsyncDisposable
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
