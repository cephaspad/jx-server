using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace JXServer.Adapter.LegacyJX1.Ciphers;

/// <summary>
/// Bit-exact implementation of the symmetric 5,679-key <c>KSG_DecodeEncode</c> stream cipher.
/// </summary>
public static class KsgStreamCipher
{
    /// <summary>
    /// Number of <c>uint32</c> entries in <c>g_uPublicKeys</c> (<c>5,679</c>).
    /// </summary>
    public const uint NumOfPubKeys = 5679u;

    /// <summary>
    /// Transforms a packet payload in-place using the session key passed by value
    /// (preserving the per-connection key across frames as <c>KSG_DecodeEncode</c> mutates its local copy).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EncodeDecodeByValue(Span<byte> payload, uint connectionKey)
    {
        uint workingCopy = connectionKey;
        DecodeEncode(payload, ref workingCopy, KsgPublicKeyTable.PublicKeys);
    }

    /// <summary>
    /// Bit-exact implementation of <c>KSG_DecodeEncode</c>.
    /// </summary>
    public static void DecodeEncode(Span<byte> payload, ref uint uKey, ReadOnlySpan<uint> g_uPublicKeys)
    {
        uint uSize = (uint)payload.Length;
        uint uDwordCount = uSize >> 2;
        uint uRemainCount = uSize & 3u;
        uint uWorkingKey = uKey;

        int offset = 0;

        // 1. 32-bit DWORD loop: countdown from (uDwordCount - 1) down to 0
        for (int ebx = (int)uDwordCount - 1; ebx != -1; ebx--)
        {
            uint uIndex = unchecked((uint)ebx + uWorkingKey) % NumOfPubKeys;
            uWorkingKey = unchecked(g_uPublicKeys[(int)uIndex] + 0x2E6D23C1u);

            uint plainOrCipherDword = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(offset, 4));
            BinaryPrimitives.WriteUInt32LittleEndian(payload.Slice(offset, 4), plainOrCipherDword ^ uWorkingKey);
            offset += 4;
        }

        // 2. 8-bit Tail loop: processes remaining 0..3 bytes (uRemainCount = uSize & 3)
        if (uRemainCount != 0)
        {
            uint uTailIndex = uRemainCount % NumOfPubKeys;
            uint uTailMask = g_uPublicKeys[(int)uTailIndex] ^ uWorkingKey;

            for (int i = 0; i < (int)uRemainCount; i++)
            {
                payload[offset + i] ^= (byte)(uTailMask & 0xFFu);
                uTailMask >>= 8;
            }
        }

        // 3. LCG key mutation inside function scope
        uKey = unchecked((uKey * 31u) + 0x08088405u);
    }
}
