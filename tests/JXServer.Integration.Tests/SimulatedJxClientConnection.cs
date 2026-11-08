using System.Buffers.Binary;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Ciphers;

namespace JXServer.Integration.Tests;

/// <summary>
/// Lightweight simulated client socket speaking the exact JX1 2-byte length-prefixed frame protocol,
/// <c>0x20</c> handshake key extraction, and <see cref="KsgStreamCipher"/> encryption/decryption.
/// </summary>
public sealed class SimulatedJxClientConnection : IAsyncDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;

    public uint ClientKey { get; private set; }

    public uint ServerKey { get; private set; }

    private SimulatedJxClientConnection(TcpClient tcpClient, NetworkStream stream, uint clientKey, uint serverKey)
    {
        _tcpClient = tcpClient;
        _stream = stream;
        ClientKey = clientKey;
        ServerKey = serverKey;
    }

    public static async Task<SimulatedJxClientConnection> ConnectAndCompleteHandshakeAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync("127.0.0.1", port, cancellationToken);
        NetworkStream stream = client.GetStream();

        byte[] handshakeWire = new byte[44];
        await ReadExactAsync(stream, handshakeWire, cancellationToken);

        ushort len = BinaryPrimitives.ReadUInt16LittleEndian(handshakeWire.AsSpan(0, 2));
        if (len != 44)
        {
            throw new InvalidDataException($"Expected 44-byte handshake frame, got {len}.");
        }

        (uint clientKey, uint serverKey) = HandshakeKeyObfuscator.ExtractHandshakeKeys(handshakeWire.AsSpan(2, 42));
        return new SimulatedJxClientConnection(client, stream, clientKey, serverKey);
    }

    public async Task SendEncryptedPayloadAsync(ReadOnlyMemory<byte> plaintextPayload, CancellationToken cancellationToken = default)
    {
        byte[] wire = new byte[plaintextPayload.Length + 2];
        BinaryPrimitives.WriteUInt16LittleEndian(wire.AsSpan(0, 2), (ushort)wire.Length);
        plaintextPayload.Span.CopyTo(wire.AsSpan(2));
        KsgStreamCipher.EncodeDecodeByValue(wire.AsSpan(2), ClientKey);

        await _stream.WriteAsync(wire, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }

    public Task SendEncryptedStructAsync<T>(T packet, CancellationToken cancellationToken = default)
        where T : unmanaged
    {
        byte[] raw = new byte[Marshal.SizeOf<T>()];
        MemoryMarshal.Write(raw, in packet);
        return SendEncryptedPayloadAsync(raw, cancellationToken);
    }

    public async Task<byte[]> ReadNextDecryptedPayloadAsync(CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[2];
        await ReadExactAsync(_stream, header, cancellationToken);
        ushort frameLen = BinaryPrimitives.ReadUInt16LittleEndian(header);
        if (frameLen < 3)
        {
            throw new InvalidDataException($"Invalid S2C frame length {frameLen}.");
        }

        byte[] payload = new byte[frameLen - 2];
        await ReadExactAsync(_stream, payload, cancellationToken);
        KsgStreamCipher.EncodeDecodeByValue(payload, ServerKey);
        return payload;
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            _tcpClient.Close();
            _tcpClient.Dispose();
        }
        catch
        {
        }

        return ValueTask.CompletedTask;
    }

    private static async Task ReadExactAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken ct)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[total..], ct);
            if (read == 0)
            {
                throw new EndOfStreamException("Remote endpoint closed the connection.");
            }

            total += read;
        }
    }
}
