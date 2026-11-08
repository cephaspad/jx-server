using System.Buffers.Binary;
using System.Reflection;

namespace JXServer.Adapter.LegacyJX1.Ciphers;

/// <summary>
/// Loads and exposes the 5,679-entry <c>uint32</c> <c>g_uPublicKeys</c> table (22,716 bytes)
/// from the embedded resource <c>g_uPublicKeys.bin</c> (<c>0x853B9129</c> at index 0).
/// </summary>
public static class KsgPublicKeyTable
{
    /// <summary>
    /// Number of <c>uint32</c> entries in <c>g_uPublicKeys</c>.
    /// </summary>
    public const int EntryCount = 5679;

    /// <summary>
    /// Total byte size of <c>g_uPublicKeys.bin</c> (<c>22,716</c> bytes).
    /// </summary>
    public const int ByteSize = EntryCount * sizeof(uint);

    private static readonly uint[] Keys = LoadEmbeddedKeys();

    /// <summary>
    /// Gets the read-only span of 5,679 <c>uint32</c> public keys.
    /// </summary>
    public static ReadOnlySpan<uint> PublicKeys => Keys;

    private static uint[] LoadEmbeddedKeys()
    {
        Assembly assembly = typeof(KsgPublicKeyTable).Assembly;
        using Stream stream = assembly.GetManifestResourceStream("g_uPublicKeys.bin")
            ?? throw new InvalidOperationException("Embedded resource 'g_uPublicKeys.bin' was not found.");

        byte[] rawBytes = new byte[ByteSize];
        int totalRead = 0;
        while (totalRead < ByteSize)
        {
            int read = stream.Read(rawBytes, totalRead, ByteSize - totalRead);
            if (read == 0)
            {
                throw new InvalidDataException($"Expected {ByteSize} bytes in g_uPublicKeys.bin, but read {totalRead}.");
            }

            totalRead += read;
        }

        uint[] table = new uint[EntryCount];
        ReadOnlySpan<byte> span = rawBytes;
        for (int i = 0; i < EntryCount; i++)
        {
            table[i] = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(i * 4, 4));
        }

        return table;
    }
}
