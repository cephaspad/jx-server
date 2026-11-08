using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Ciphers;
using JXServer.Adapter.LegacyJX1.Packets;

namespace JXServer.Adapter.LegacyJX1.Framing;

/// <summary>
/// Server-side <see cref="System.IO.Pipelines"/> processor for 2-byte little-endian inclusive length-prefixed
/// JX frames (<c>wFrameLength = uPayloadLen + 2</c>), <c>s2c_accountbegin</c> (<c>0x20</c>) handshake emission,
/// and <see cref="KsgStreamCipher"/> encryption/decryption.
/// </summary>
public sealed class JxFramePipeProcessor
{
    /// <summary>
    /// Maximum allowed wire frame length (<c>65,535</c> bytes).
    /// </summary>
    public const int MaxFrameLength = 0xFFFF;

    /// <summary>
    /// Minimum allowed wire frame length (<c>3</c> bytes: 2-byte header + 1-byte opcode).
    /// </summary>
    public const int MinFrameLength = 3;

    private uint _clientKey;
    private uint _serverKey;
    private bool _isHandshakeSent;

    /// <summary>
    /// Gets the active C2S stream cipher key (<c>uClientKey</c>).
    /// </summary>
    public uint ClientKey => _clientKey;

    /// <summary>
    /// Gets the active S2C stream cipher key (<c>uServerKey</c>).
    /// </summary>
    public uint ServerKey => _serverKey;

    /// <summary>
    /// Gets a value indicating whether the <c>0x20</c> handshake has been initialized.
    /// </summary>
    public bool IsHandshakeSent => _isHandshakeSent;

    /// <summary>
    /// Initializes the per-connection <c>uClientKey</c> and <c>uServerKey</c> and writes the 44-byte unencrypted
    /// <see cref="AccountBeginS2CPacket"/> (<c>0x20</c>) handshake frame to <paramref name="writer"/>.
    /// </summary>
    public void WriteHandshakeFrame(IBufferWriter<byte> writer, uint clientKey, uint serverKey)
    {
        ArgumentNullException.ThrowIfNull(writer);

        _clientKey = clientKey;
        _serverKey = serverKey;
        _isHandshakeSent = true;

        AccountBeginS2CPacket handshake = AccountBeginS2CPacket.Create(clientKey, serverKey);
        Span<byte> dest = writer.GetSpan(AccountBeginS2CPacket.WireFrameSize);
        handshake.WriteWireFrame(dest);
        writer.Advance(AccountBeginS2CPacket.WireFrameSize);
    }

    /// <summary>
    /// Initializes keys directly (useful for unit tests or pre-negotiated sessions).
    /// </summary>
    public void InitializeKeys(uint clientKey, uint serverKey)
    {
        _clientKey = clientKey;
        _serverKey = serverKey;
        _isHandshakeSent = true;
    }

    /// <summary>
    /// Attempts to read and decrypt a complete C2S frame from <paramref name="buffer"/> using <see cref="ClientKey"/>.
    /// </summary>
    public bool TryReadC2SFrame(ref ReadOnlySequence<byte> buffer, out byte[] payload)
    {
        payload = [];
        if (!_isHandshakeSent)
        {
            throw new InvalidOperationException("Cannot read C2S frame before 0x20 handshake is initialized.");
        }

        if (buffer.Length < 2)
        {
            return false;
        }

        Span<byte> headerBytes = stackalloc byte[2];
        buffer.Slice(0, 2).CopyTo(headerBytes);
        ushort frameLength = BinaryPrimitives.ReadUInt16LittleEndian(headerBytes);

        if (frameLength < MinFrameLength)
        {
            throw new InvalidDataException($"Invalid JX frame length {frameLength} (must be >= {MinFrameLength}).");
        }

        if (buffer.Length < frameLength)
        {
            return false;
        }

        int payloadLength = frameLength - 2;
        payload = new byte[payloadLength];
        buffer.Slice(2, payloadLength).CopyTo(payload);
        buffer = buffer.Slice(frameLength);

        KsgStreamCipher.EncodeDecodeByValue(payload, _clientKey);
        return true;
    }

    /// <summary>
    /// Encrypts <paramref name="plaintextPayload"/> using <see cref="ServerKey"/> and writes the length-prefixed S2C frame to <paramref name="writer"/>.
    /// </summary>
    public void WriteEncryptedS2CFrame(IBufferWriter<byte> writer, ReadOnlySpan<byte> plaintextPayload)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (!_isHandshakeSent)
        {
            throw new InvalidOperationException("Cannot send encrypted S2C frame before 0x20 handshake is initialized.");
        }

        if (plaintextPayload.IsEmpty || plaintextPayload.Length > MaxFrameLength - 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(plaintextPayload),
                $"Payload length {plaintextPayload.Length} is out of valid range 1..65533.");
        }

        ushort frameLength = (ushort)(plaintextPayload.Length + 2);
        Span<byte> dest = writer.GetSpan(frameLength);
        BinaryPrimitives.WriteUInt16LittleEndian(dest[..2], frameLength);

        Span<byte> payloadSlice = dest.Slice(2, plaintextPayload.Length);
        plaintextPayload.CopyTo(payloadSlice);
        KsgStreamCipher.EncodeDecodeByValue(payloadSlice, _serverKey);

        writer.Advance(frameLength);
    }

    /// <summary>
    /// Serializes an unmanaged S2C packet struct and writes it as an encrypted length-prefixed S2C frame.
    /// </summary>
    public void WriteEncryptedS2CStruct<T>(IBufferWriter<byte> writer, in T packet) where T : unmanaged
    {
        int size = Marshal.SizeOf<T>();
        Span<byte> raw = size <= 512 ? stackalloc byte[size] : new byte[size];
        MemoryMarshal.Write(raw, in packet);
        WriteEncryptedS2CFrame(writer, raw);
    }
}
