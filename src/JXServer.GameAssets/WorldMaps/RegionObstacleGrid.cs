using System.Buffers.Binary;
using JXServer.Primitives.WorldZones;

namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Immutable <c>16 * 32</c> cell obstacle matrix for a single map region (<c>Region_S.dat</c> Section 0).
/// </summary>
public sealed class RegionObstacleGrid
{
    /// <summary>
    /// Byte size of the <c>16 * 32</c> <c>int32</c> obstacle section (<c>2,048</c> bytes).
    /// </summary>
    public const int ObstacleSectionByteSize = WorldCoordinateConsts.RegionWidthCells * WorldCoordinateConsts.RegionHeightCells * sizeof(int);

    private readonly int[] _cellObstacles;

    /// <summary>
    /// Gets the region X index.
    /// </summary>
    public int RegionX { get; }

    /// <summary>
    /// Gets the region Y index.
    /// </summary>
    public int RegionY { get; }

    /// <summary>
    /// Gets the read-only memory of <c>512</c> cell obstacle values (<c>cellX * 32 + cellY</c>).
    /// </summary>
    public ReadOnlyMemory<int> CellObstacles => _cellObstacles;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegionObstacleGrid"/> class.
    /// </summary>
    public RegionObstacleGrid(int regionX, int regionY, int[] cellObstacles)
    {
        ArgumentNullException.ThrowIfNull(cellObstacles);
        if (cellObstacles.Length != WorldCoordinateConsts.RegionWidthCells * WorldCoordinateConsts.RegionHeightCells)
        {
            throw new ArgumentException("Cell obstacle array must have exactly 512 elements.", nameof(cellObstacles));
        }

        RegionX = regionX;
        RegionY = regionY;
        _cellObstacles = cellObstacles;
    }

    /// <summary>
    /// Returns the obstacle value at local region cell coordinates (<c>cellX</c> in <c>0..15</c>, <c>cellY</c> in <c>0..31</c>).
    /// <c>0</c> indicates passable (<c>Obstacle_NULL</c>).
    /// </summary>
    public int GetObstacle(int cellX, int cellY)
    {
        if ((uint)cellX >= WorldCoordinateConsts.RegionWidthCells ||
            (uint)cellY >= WorldCoordinateConsts.RegionHeightCells)
        {
            return 1;
        }

        return _cellObstacles[(cellX * WorldCoordinateConsts.RegionHeightCells) + cellY];
    }

    /// <summary>
    /// Attempts to parse a <see cref="RegionObstacleGrid"/> from a raw <c>Region_S.dat</c> or <c>Region_C.dat</c> binary payload.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> regionDatBytes, int regionX, int regionY, out RegionObstacleGrid? grid)
    {
        grid = null;
        if (regionDatBytes.Length < 4)
        {
            return false;
        }

        uint sectionCount = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes[..4]);
        if (sectionCount == 0 || sectionCount > 32)
        {
            return false;
        }

        int obstacleDataOffset = -1;
        int header8Size = 4 + (int)(sectionCount * 8);
        if (regionDatBytes.Length >= header8Size + ObstacleSectionByteSize)
        {
            for (int i = 0; i < sectionCount; i++)
            {
                int entryOffset = 4 + (i * 8);
                uint dataOffset = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(entryOffset, 4));
                uint dataLength = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(entryOffset + 4, 4));
                if (dataLength == ObstacleSectionByteSize)
                {
                    int resolvedOffset = dataOffset < (uint)header8Size
                        ? header8Size + (int)dataOffset
                        : (int)dataOffset;
                    if (resolvedOffset + ObstacleSectionByteSize <= regionDatBytes.Length)
                    {
                        obstacleDataOffset = resolvedOffset;
                        break;
                    }
                }
            }
        }

        int header12Size = 4 + (int)(sectionCount * 12);
        if (obstacleDataOffset < 0 && regionDatBytes.Length >= header12Size)
        {
            for (int i = 0; i < sectionCount; i++)
            {
                int entryOffset = 4 + (i * 12);
                uint uType = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(entryOffset, 4));
                uint dataOffset = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(entryOffset + 4, 4));
                uint dataLength = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(entryOffset + 8, 4));

                if (uType == 1u &&
                    dataLength >= ObstacleSectionByteSize &&
                    dataOffset + ObstacleSectionByteSize <= regionDatBytes.Length)
                {
                    obstacleDataOffset = (int)dataOffset;
                    break;
                }
            }
        }

        if (obstacleDataOffset < 0)
        {
            return false;
        }

        ReadOnlySpan<byte> obstaclePayload = regionDatBytes.Slice(obstacleDataOffset, ObstacleSectionByteSize);
        int[] cells = new int[WorldCoordinateConsts.RegionWidthCells * WorldCoordinateConsts.RegionHeightCells];
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = BinaryPrimitives.ReadInt32LittleEndian(obstaclePayload.Slice(i * 4, 4));
        }

        grid = new RegionObstacleGrid(regionX, regionY, cells);
        return true;
    }
}
