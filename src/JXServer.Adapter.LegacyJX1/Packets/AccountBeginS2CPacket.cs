using System.Buffers.Binary;
using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Ciphers;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 42-byte unencrypted <c>s2c_accountbegin</c> (<c>0x20</c>) handshake payload (44-byte wire frame including 2-byte length header).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct AccountBeginS2CPacket
{
    /// <summary>
    /// Fixed payload size (<c>42</c> bytes).
    /// </summary>
    public const int PayloadSize = 42;

    /// <summary>
    /// Fixed wire size including 2-byte length header (<c>44</c> bytes).
    /// </summary>
    public const int WireFrameSize = 44;

    /// <summary>
    /// Raw 42-byte payload buffer.
    /// </summary>
    public fixed byte RawPayload[PayloadSize];

    /// <summary>
    /// Builds a 42-byte <see cref="AccountBeginS2CPacket"/> containing obfuscated <paramref name="clientKey"/> at <c>+0x08</c>
    /// and obfuscated <paramref name="serverKey"/> at <c>+0x11</c>.
    /// </summary>
    public static AccountBeginS2CPacket Create(uint clientKey, uint serverKey)
    {
        AccountBeginS2CPacket packet = default;
        Span<byte> span = new(packet.RawPayload, PayloadSize);
        span.Clear();
        span[0] = ProtocolOpcodes.s2c_accountbegin;

        uint obfuscatedClientKey = HandshakeKeyObfuscator.ObfuscateKey(clientKey);
        uint obfuscatedServerKey = HandshakeKeyObfuscator.ObfuscateKey(serverKey);

        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0x08, 4), obfuscatedClientKey);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0x11, 4), obfuscatedServerKey);
        return packet;
    }

    /// <summary>
    /// Writes the 44-byte unencrypted wire frame (<c>0x002C</c> header + 42-byte payload) into <paramref name="destination"/>.
    /// </summary>
    public readonly void WriteWireFrame(Span<byte> destination)
    {
        if (destination.Length < WireFrameSize)
        {
            throw new ArgumentException($"Destination must be at least {WireFrameSize} bytes.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt16LittleEndian(destination[..2], PayloadSize + 2);
        fixed (byte* ptr = RawPayload)
        {
            new ReadOnlySpan<byte>(ptr, PayloadSize).CopyTo(destination.Slice(2, PayloadSize));
        }
    }
}
