using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Ciphers;

/// <summary>
/// Implements the second-layer 16-bit rolling XOR ciphers used by Bishop Gateway packets:
/// <list type="bullet">
///   <item><description><c>0x3042</c> cipher for <c>s2c_roleserver_getrolelist_result</c> (<c>0x37</c>) payload at <c>+0x0A</c>.</description></item>
///   <item><description><c>0x38BC</c> cipher for <c>c2s_dbplayerselect</c> (<c>0x64</c>) role name at <c>+0x09</c>.</description></item>
/// </list>
/// </summary>
public static class SecondLayerXorCipher
{
    /// <summary>
    /// Encrypts or decrypts (symmetric) the <c>nDataLen</c> role-list buffer at offset <c>+0x0A</c> of
    /// <c>s2c_roleserver_getrolelist_result</c> (<c>0x37</c>) using the <c>0x3042</c> 16-bit rolling XOR cipher.
    /// </summary>
    public static void TransformRoleListPayload(Span<byte> roleListBuffer)
    {
        int nDataLen = roleListBuffer.Length;
        uint key = (uint)nDataLen & 0xFFFFu;
        uint rem = key & 1u;
        int words = nDataLen >> 1;
        int offset = 0;

        while (words != 0)
        {
            words--;
            key = (((uint)words + 0x3042u + key) ^ (((uint)words << (int)rem) & 0xFFFFu)) & 0xFFFFu;
            ushort w = BinaryPrimitives.ReadUInt16LittleEndian(roleListBuffer.Slice(offset, 2));
            BinaryPrimitives.WriteUInt16LittleEndian(roleListBuffer.Slice(offset, 2), (ushort)(w ^ key));
            offset += 2;
        }

        key ^= rem;
        while (rem != 0)
        {
            rem--;
            roleListBuffer[offset] ^= (byte)(key & 0xFEu);
            offset++;
            key >>= 8;
        }
    }

    /// <summary>
    /// Encrypts or decrypts (symmetric) the first <paramref name="nNameLen"/> bytes (where <c>nNameLen = strlen - 1</c>)
    /// of <c>szRoleName</c> at offset <c>+0x09</c> of <c>c2s_dbplayerselect</c> (<c>0x64</c>) using the <c>0x38BC</c> 16-bit rolling XOR cipher.
    /// </summary>
    public static void TransformSelectRoleName(Span<byte> roleNameSpan, int nNameLen)
    {
        if (nNameLen <= 0 || nNameLen > roleNameSpan.Length)
        {
            return;
        }

        uint key = (uint)nNameLen & 0xFFFFu;
        uint rem = key & 1u;
        int words = nNameLen >> 1;
        int offset = 0;

        while (words != 0)
        {
            words--;
            key = ((uint)words + 0x38BCu + key) & 0xFFFFu;
            ushort w = BinaryPrimitives.ReadUInt16LittleEndian(roleNameSpan.Slice(offset, 2));
            BinaryPrimitives.WriteUInt16LittleEndian(roleNameSpan.Slice(offset, 2), (ushort)(w ^ key));
            offset += 2;
        }

        key ^= rem;
        while (rem != 0)
        {
            rem--;
            roleNameSpan[offset] ^= (byte)(key & 0xEFu);
            offset++;
            key >>= 8;
        }
    }
}
