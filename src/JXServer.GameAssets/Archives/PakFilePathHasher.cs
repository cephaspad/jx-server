using JXServer.Primitives.Encodings;

namespace JXServer.GameAssets.Archives;

/// <summary>
/// Implements the exact Kingsoft <c>g_FileName2Id</c> (<c>0x08217fb0</c>) path hashing algorithm
/// with signed-char promotion and final <c>^ 0x12345678u</c> mask.
/// </summary>
public static class PakFilePathHasher
{
    /// <summary>
    /// Computes the 32-bit <c>g_FileName2Id</c> hash of a raw GBK/ASCII internal archive path.
    /// </summary>
    public static uint ComputeHash(ReadOnlySpan<byte> rawPathBytes)
    {
        uint id = 0;
        for (int i = 0; i < rawPathBytes.Length && rawPathBytes[i] != 0; i++)
        {
            byte c = rawPathBytes[i];
            if (c is >= (byte)'A' and <= (byte)'Z')
            {
                c = (byte)(c + 0x20);
            }
            else if (c == (byte)'/')
            {
                c = (byte)'\\';
            }

            int signedChar = (sbyte)c;
            id = unchecked(((id + (uint)((i + 1) * signedChar)) % 0x8000000Bu) * 0xFFFFFFEFu);
        }

        return id ^ 0x12345678u;
    }

    /// <summary>
    /// Encodes a string path using GBK (CP936) and computes its <c>g_FileName2Id</c> hash.
    /// </summary>
    public static uint ComputeHash(string gbkPath)
    {
        ArgumentNullException.ThrowIfNull(gbkPath);
        byte[] bytes = GbkEncoding.EncodeBytes(gbkPath);
        return ComputeHash(bytes);
    }
}
