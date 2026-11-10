using System.Buffers.Binary;
using JXServer.Primitives.WorldZones;

namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Immutable <c>16 * 32</c> cell trap script hash matrix (<c>m_dwTrap[16][32]</c>) for a single map region
/// parsed from <c>Region_S.dat</c> Section 1 (<c>TRAP_SECTION</c>) (`FR-004`, `FR-008`).
/// </summary>
public sealed class RegionTrapGrid
{
    private readonly uint[] _cellTrapIds;

    public int RegionX { get; }

    public int RegionY { get; }

    public ReadOnlyMemory<uint> CellTrapIds => _cellTrapIds;

    public RegionTrapGrid(int regionX, int regionY, uint[] cellTrapIds)
    {
        ArgumentNullException.ThrowIfNull(cellTrapIds);
        if (cellTrapIds.Length != WorldCoordinateConsts.RegionWidthCells * WorldCoordinateConsts.RegionHeightCells)
        {
            throw new ArgumentException("Cell trap array must have exactly 512 elements.", nameof(cellTrapIds));
        }

        RegionX = regionX;
        RegionY = regionY;
        _cellTrapIds = cellTrapIds;
    }

    /// <summary>
    /// Returns the 32-bit <c>uTrapId</c> (<c>g_FileName2Id</c> hash) at local region cell coordinates
    /// (<c>cellX</c> in <c>0..15</c>, <c>cellY</c> in <c>0..31</c>), or <c>0</c> if no trap is present.
    /// </summary>
    public uint GetTrapId(int cellX, int cellY)
    {
        if ((uint)cellX >= WorldCoordinateConsts.RegionWidthCells ||
            (uint)cellY >= WorldCoordinateConsts.RegionHeightCells)
        {
            return 0;
        }

        return _cellTrapIds[(cellX * WorldCoordinateConsts.RegionHeightCells) + cellY];
    }

    /// <summary>
    /// Attempts to parse Section 1 (<c>TRAP_SECTION</c>: 12-byte <c>KTrapFileHead</c> + 8-byte <c>KSPTrap</c> records)
    /// from a raw <c>Region_S.dat</c> payload.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> regionDatBytes, int regionX, int regionY, out RegionTrapGrid? trapGrid)
    {
        trapGrid = null;
        if (regionDatBytes.Length < 20)
        {
            return false;
        }

        uint sectionCount = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes[..4]);
        if (sectionCount < 2 || sectionCount > 32)
        {
            return false;
        }

        int header8Size = 4 + (int)(sectionCount * 8);
        if (regionDatBytes.Length < header8Size)
        {
            return false;
        }

        // Section 0 offset determines whether section table offsets are relative to header8Size (0) or absolute (>= header8Size)
        uint section0Offset = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(4, 4));
        bool offsetsAreRelative = section0Offset == 0;

        // Section 1 (TRAP_SECTION) descriptor at 4 + (1 * 8) = 12
        uint rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(12, 4));
        uint rawLength = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(16, 4));
        if (rawLength < 12)
        {
            return false;
        }

        int resolvedOffset = offsetsAreRelative ? header8Size + (int)rawOffset : (int)rawOffset;
        if (resolvedOffset < header8Size || resolvedOffset + (int)rawLength > regionDatBytes.Length)
        {
            return false;
        }

        ReadOnlySpan<byte> trapSection = regionDatBytes.Slice(resolvedOffset, (int)rawLength);
        uint numTraps = BinaryPrimitives.ReadUInt32LittleEndian(trapSection[..4]);
        if (numTraps == 0)
        {
            return false;
        }

        int recordOffset;
        if (12 + (numTraps * 8) <= trapSection.Length)
        {
            recordOffset = 12;
        }
        else if (4 + (numTraps * 8) <= trapSection.Length)
        {
            recordOffset = 4;
        }
        else
        {
            return false;
        }

        uint[] cells = new uint[WorldCoordinateConsts.RegionWidthCells * WorldCoordinateConsts.RegionHeightCells];
        for (int i = 0; i < numTraps; i++, recordOffset += 8)
        {
            byte cX = trapSection[recordOffset];
            byte cY = trapSection[recordOffset + 1];
            byte cNumCell = trapSection[recordOffset + 2];
            uint uTrapId = BinaryPrimitives.ReadUInt32LittleEndian(trapSection.Slice(recordOffset + 4, 4));

            if (uTrapId == 0 || cY >= WorldCoordinateConsts.RegionHeightCells)
            {
                continue;
            }

            int count = Math.Max((int)cNumCell, 1);
            for (int j = 0; j < count; j++)
            {
                int targetCellX = cX + j;
                if ((uint)targetCellX < WorldCoordinateConsts.RegionWidthCells)
                {
                    cells[(targetCellX * WorldCoordinateConsts.RegionHeightCells) + cY] = uTrapId;
                }
            }
        }

        trapGrid = new RegionTrapGrid(regionX, regionY, cells);
        return true;
    }
}
