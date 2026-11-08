using System.Buffers.Binary;

namespace JXServer.GameAssets.Archives;

/// <summary>
/// Kingsoft <c>.pak</c> (<c>XPackFile</c>) archive reader supporting <see cref="PakFilePathHasher"/> lookup
/// and <see cref="UclNrv2bDecompressor"/> decompression.
/// </summary>
public sealed class XPackArchiveReader
{
    /// <summary>
    /// 4-byte magic signature (<c>"PACK"</c> = <c>0x4B434150</c>).
    /// </summary>
    public const uint PackMagic = 0x4B434150u;

    private readonly byte[] _pakBytes;
    private readonly Dictionary<uint, (uint Offset, int UncompressedSize, uint CompressSizeFlag)> _indexByHash;

    private XPackArchiveReader(
        byte[] pakBytes,
        Dictionary<uint, (uint Offset, int UncompressedSize, uint CompressSizeFlag)> indexByHash)
    {
        _pakBytes = pakBytes;
        _indexByHash = indexByHash;
    }

    /// <summary>
    /// Gets the number of indexed files inside the archive.
    /// </summary>
    public int EntryCount => _indexByHash.Count;

    /// <summary>
    /// Opens and indexes a <c>.pak</c> archive file, or returns <see langword="null"/> if the file does not exist or is invalid.
    /// </summary>
    public static XPackArchiveReader? TryOpen(string pakFilePath)
    {
        if (!File.Exists(pakFilePath))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(pakFilePath);
        if (bytes.Length < 32)
        {
            return null;
        }

        uint sig = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4));
        if (sig != PackMagic)
        {
            return null;
        }

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));
        uint indexOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8, 4));
        if (count == 0 || indexOffset + (count * 16UL) > (ulong)bytes.Length)
        {
            return null;
        }

        var map = new Dictionary<uint, (uint Offset, int UncompressedSize, uint CompressSizeFlag)>((int)count);
        int cursor = (int)indexOffset;
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> entry = bytes.AsSpan(cursor, 16);
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(entry[..4]);
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(4, 4));
            int size = BinaryPrimitives.ReadInt32LittleEndian(entry.Slice(8, 4));
            uint flag = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(12, 4));
            map[id] = (offset, size, flag);
            cursor += 16;
        }

        return new XPackArchiveReader(bytes, map);
    }

    /// <summary>
    /// Attempts to read and decompress a file from the archive using its raw GBK path bytes.
    /// </summary>
    public bool TryReadFile(ReadOnlySpan<byte> rawGbkPathBytes, out byte[] uncompressedData)
    {
        uint id = PakFilePathHasher.ComputeHash(rawGbkPathBytes);
        return TryReadFileById(id, out uncompressedData);
    }

    /// <summary>
    /// Attempts to read and decompress a file from the archive using its 32-bit hash ID.
    /// </summary>
    public bool TryReadFileById(uint id, out byte[] uncompressedData)
    {
        uncompressedData = [];
        if (!_indexByHash.TryGetValue(id, out var entry) || entry.UncompressedSize <= 0)
        {
            return false;
        }

        int compressedSize = (int)(entry.CompressSizeFlag & 0x00FFFFFFu);
        uint method = entry.CompressSizeFlag & 0xFF000000u;

        if (entry.Offset + (ulong)compressedSize > (ulong)_pakBytes.Length)
        {
            return false;
        }

        if (method == 0u || compressedSize == entry.UncompressedSize)
        {
            if (entry.Offset + (ulong)entry.UncompressedSize > (ulong)_pakBytes.Length)
            {
                return false;
            }

            uncompressedData = _pakBytes.AsSpan((int)entry.Offset, entry.UncompressedSize).ToArray();
            return true;
        }

        if (method is 0x01000000u or 0x04000000u or 0x20000000u)
        {
            try
            {
                byte[] dst = new byte[entry.UncompressedSize];
                ReadOnlySpan<byte> src = _pakBytes.AsSpan((int)entry.Offset, compressedSize);
                int written = UclNrv2bDecompressor.Decompress8(src, dst);
                if (written > 0)
                {
                    uncompressedData = dst;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        return false;
    }
}
