using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace JXServer.Adapter.LegacyJX1.Ciphers;

/// <summary>
/// Implements the 32-bit handshake key obfuscation and deobfuscation used in the 42-byte
/// <c>s2c_accountbegin</c> (<c>0x20</c>) handshake payload (<c>uClientKey</c> at <c>+0x08</c> and <c>uServerKey</c> at <c>+0x11</c>).
/// </summary>
public static class HandshakeKeyObfuscator
{
    /// <summary>
    /// Total wire length of the <c>s2c_accountbegin</c> frame including 2-byte length header (<c>44</c> bytes).
    /// </summary>
    public const int HandshakeFrameWireLength = 44;

    /// <summary>
    /// Payload length of the <c>s2c_accountbegin</c> packet (<c>42</c> bytes).
    /// </summary>
    public const int HandshakePayloadLength = 42;

    /// <summary>
    /// Opcode of <c>s2c_accountbegin</c> (<c>0x20</c>).
    /// </summary>
    public const byte HandshakeOpcode = 0x20;

    /// <summary>
    /// Obfuscates a plaintext 32-bit session key so that <see cref="DeobfuscateKey"/> recovers the exact original value:
    /// swaps outer bytes 0 and 3, applies bitwise NOT, adds <c>0x2E6D2399u</c>, and XORs with <c>0x2E6D23CFu</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ObfuscateKey(uint plainKey)
    {
        uint swapped = ((plainKey >> 24) & 0x000000FFu)
                     | (plainKey & 0x00FFFF00u)
                     | ((plainKey << 24) & 0xFF000000u);
        uint inverted = ~swapped;
        return unchecked(inverted + 0x2E6D2399u) ^ 0x2E6D23CFu;
    }

    /// <summary>
    /// Deobfuscates a 32-bit handshake key:
    /// <c>tmp = ~((obfuscated ^ 0x2E6D23CFu) - 0x2E6D2399u)</c>, then swaps outer bytes 0 and 3.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint DeobfuscateKey(uint obfuscatedKey)
    {
        uint tmp = unchecked(~((obfuscatedKey ^ 0x2E6D23CFu) - 0x2E6D2399u));
        return ((tmp >> 24) & 0x000000FFu) | (tmp & 0x00FFFF00u) | ((tmp << 24) & 0xFF000000u);
    }

    /// <summary>
    /// Extracts and deobfuscates <c>uClientKey</c> (<c>+0x08</c>) and <c>uServerKey</c> (<c>+0x11</c>) from a 42-byte <c>0x20</c> payload.
    /// </summary>
    public static (uint ClientKey, uint ServerKey) ExtractHandshakeKeys(ReadOnlySpan<byte> payload42Bytes)
    {
        if (payload42Bytes.Length < HandshakePayloadLength || payload42Bytes[0] != HandshakeOpcode)
        {
            throw new InvalidDataException(
                $"Invalid s2c_accountbegin handshake payload (length={payload42Bytes.Length}, opcode=0x{(payload42Bytes.IsEmpty ? 0 : payload42Bytes[0]):X2}).");
        }

        uint obfuscatedClientKey = BinaryPrimitives.ReadUInt32LittleEndian(payload42Bytes.Slice(0x08, 4));
        uint obfuscatedServerKey = BinaryPrimitives.ReadUInt32LittleEndian(payload42Bytes.Slice(0x11, 4));

        return (DeobfuscateKey(obfuscatedClientKey), DeobfuscateKey(obfuscatedServerKey));
    }
}
