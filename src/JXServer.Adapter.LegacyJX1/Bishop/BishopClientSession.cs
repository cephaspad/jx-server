using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Security.Cryptography;
using JXServer.Adapter.LegacyJX1.Framing;
using JXServer.Adapter.LegacyJX1.Logging;
using JXServer.Contracts.SessionLeases;
using Microsoft.Extensions.Logging;

namespace JXServer.Adapter.LegacyJX1.Bishop;

/// <summary>
/// Represents an active client TCP session connected to the Bishop Gateway (<c>5622</c>).
/// </summary>
public sealed class BishopClientSession : IAsyncDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly IReadOnlyDictionary<byte, IBishopPacketHandler> _handlers;
    private readonly ISessionLeaseAppService _sessionLeaseAppService;
    private readonly ILogger _logger;
    private readonly JxFramePipeProcessor _frameProcessor = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();

    private NetworkStream? _stream;
    private int _disposed;

    public string ConnectionId { get; } = Guid.NewGuid().ToString("N");

    public Guid? AuthenticatedAccountId { get; private set; }

    public string? AuthenticatedUserName { get; private set; }

    public long LeaseEpoch { get; private set; }

    public bool IsAuthenticated => AuthenticatedAccountId.HasValue && LeaseEpoch > 0;

    public BishopClientSession(
        TcpClient tcpClient,
        IReadOnlyDictionary<byte, IBishopPacketHandler> handlers,
        ISessionLeaseAppService sessionLeaseAppService,
        ILogger logger)
    {
        _tcpClient = tcpClient;
        _handlers = handlers;
        _sessionLeaseAppService = sessionLeaseAppService;
        _logger = logger;
    }

    public void SetAuthenticated(Guid accountId, string userName, long leaseEpoch)
    {
        AuthenticatedAccountId = accountId;
        AuthenticatedUserName = userName;
        LeaseEpoch = leaseEpoch;
    }

    public async Task RunAsync(CancellationToken serverStoppingToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(serverStoppingToken, _cts.Token);
        CancellationToken ct = linkedCts.Token;

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

            var reader = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
            while (!ct.IsCancellationRequested)
            {
                ReadResult readResult = await reader.ReadAsync(ct);
                ReadOnlySequence<byte> buffer = readResult.Buffer;

                bool keepConnection = true;
                while (_frameProcessor.TryReadC2SFrame(ref buffer, out byte[] payload))
                {
                    if (!ProtocolSizeTable.IsValidBishopC2SPayload(payload) ||
                        !_handlers.TryGetValue(payload[0], out IBishopPacketHandler? handler))
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
            if (AuthenticatedAccountId.HasValue && LeaseEpoch > 0)
            {
                try
                {
                    await _sessionLeaseAppService.CompleteLogoutOrEvictAsync(
                        new ReleaseSessionLeaseInput(
                            AuthenticatedAccountId.Value,
                            LeaseEpoch,
                            CharacterSnapshot: null,
                            OnlyIfGatewayAuthenticated: true),
                        CancellationToken.None);
                }
                catch
                {
                }
            }

            await DisposeAsync();
        }
    }

    public async ValueTask SendPacketAsync<T>(T packet, CancellationToken cancellationToken = default)
        where T : unmanaged
    {
        if (Volatile.Read(ref _disposed) != 0 || _stream is null)
        {
            return;
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (_stream is null)
            {
                return;
            }

            var writer = new ArrayBufferWriter<byte>(256);
            _frameProcessor.WriteEncryptedS2CStruct(writer, in packet);
            await _stream.WriteAsync(writer.WrittenMemory, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask SendRawPayloadAsync(ReadOnlyMemory<byte> plaintextPayload, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0 || _stream is null)
        {
            return;
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (_stream is null)
            {
                return;
            }

            var writer = new ArrayBufferWriter<byte>(plaintextPayload.Length + 2);
            _frameProcessor.WriteEncryptedS2CFrame(writer, plaintextPayload.Span);
            await _stream.WriteAsync(writer.WrittenMemory, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void CloseSession()
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
            CloseSession();
            _sendLock.Dispose();
            _cts.Dispose();
            _tcpClient.Dispose();
        }

        return ValueTask.CompletedTask;
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
