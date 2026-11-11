using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using JXServer.Primitives.WorldZones;

namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Parses all 4 server sections (<c>OBSTACLE_SECTION</c>, <c>TRAP_SECTION</c>, <c>NPC_SECTION</c>, <c>OBJ_SECTION</c>)
/// of a Kingsoft <c>Region_S.dat</c> binary file per <c>SceneDataDef.h</c>.
/// </summary>
public static class RegionDatBinaryReader
{
    public const int CellCountPerRegion = WorldCoordinateConsts.RegionWidthCells * WorldCoordinateConsts.RegionHeightCells;
    public const int ObstacleByteLength = CellCountPerRegion * sizeof(int);

    public static bool TryParse(ReadOnlySpan<byte> regionDatBytes, int regionX, int regionY, out ParsedRegionDat? parsed)
    {
        parsed = null;
        if (regionDatBytes.Length < 4)
        {
            return false;
        }

        uint sectionCount = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes[..4]);
        if (sectionCount == 0 || sectionCount > 32)
        {
            return false;
        }

        int headerSize = 4 + ((int)sectionCount * 8);
        if (regionDatBytes.Length < headerSize)
        {
            return false;
        }

        uint sec0RawOffset = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(4, 4));
        bool offsetsAreRelative = sec0RawOffset < (uint)headerSize;

        // 0. OBSTACLE_SECTION
        int[] obstacleCells = new int[CellCountPerRegion];
        if (TryGetSectionSlice(regionDatBytes, sectionCount, headerSize, offsetsAreRelative, 0, out ReadOnlySpan<byte> obsSlice) &&
            obsSlice.Length >= ObstacleByteLength)
        {
            for (int i = 0; i < CellCountPerRegion; i++)
            {
                obstacleCells[i] = BinaryPrimitives.ReadInt32LittleEndian(obsSlice.Slice(i * 4, 4));
            }
        }

        // 1. TRAP_SECTION
        var traps = new List<ParsedTrapCellSpan>();
        if (TryGetSectionSlice(regionDatBytes, sectionCount, headerSize, offsetsAreRelative, 1, out ReadOnlySpan<byte> trapSlice) &&
            trapSlice.Length >= 12)
        {
            uint numTraps = BinaryPrimitives.ReadUInt32LittleEndian(trapSlice[..4]);
            int pos = 12 + (numTraps * 8) <= trapSlice.Length ? 12 : 4;
            for (int i = 0; i < numTraps && pos + 8 <= trapSlice.Length; i++, pos += 8)
            {
                byte cX = trapSlice[pos];
                byte cY = trapSlice[pos + 1];
                byte cNumCell = trapSlice[pos + 2];
                uint uTrapId = BinaryPrimitives.ReadUInt32LittleEndian(trapSlice.Slice(pos + 4, 4));

                if (uTrapId == 0 || cY >= WorldCoordinateConsts.RegionHeightCells || cX + cNumCell - 1 >= WorldCoordinateConsts.RegionWidthCells)
                {
                    continue;
                }

                traps.Add(new ParsedTrapCellSpan(regionX, regionY, cX, cY, Math.Max((byte)1, cNumCell), uTrapId));
            }
        }

        // 2. NPC_SECTION
        var npcs = new List<ParsedRegionNpc>();
        if (TryGetSectionSlice(regionDatBytes, sectionCount, headerSize, offsetsAreRelative, 2, out ReadOnlySpan<byte> npcSlice) &&
            npcSlice.Length >= 12)
        {
            uint numNpcs = BinaryPrimitives.ReadUInt32LittleEndian(npcSlice[..4]);
            int pos = 12;
            for (int i = 0; i < numNpcs && pos + 60 <= npcSlice.Length; i++)
            {
                ReadOnlySpan<byte> h = npcSlice.Slice(pos, 60);
                int tplId = BinaryPrimitives.ReadInt32LittleEndian(h[..4]);
                int posX = BinaryPrimitives.ReadInt32LittleEndian(h.Slice(4, 4));
                int posY = BinaryPrimitives.ReadInt32LittleEndian(h.Slice(8, 4));
                bool isSpecial = h[12] != 0;
                string customName = HybridLegacyTextDecoder.DecodeSmart(h.Slice(16, 32), preferVietnamese: true).Trim();
                short level = BinaryPrimitives.ReadInt16LittleEndian(h.Slice(48, 2));
                short curFrame = BinaryPrimitives.ReadInt16LittleEndian(h.Slice(50, 2));
                short kind = BinaryPrimitives.ReadInt16LittleEndian(h.Slice(54, 2));
                byte camp = h[56];
                byte series = h[57];
                ushort scriptLen = BinaryPrimitives.ReadUInt16LittleEndian(h.Slice(58, 2));
                pos += 60;

                if (pos + scriptLen > npcSlice.Length)
                {
                    break;
                }

                string scriptPath = scriptLen > 0
                    ? LegacyPathResolver.NormalizeEnginePath(npcSlice.Slice(pos, scriptLen))
                    : string.Empty;
                pos += scriptLen;

                if (tplId >= 0)
                {
                    npcs.Add(new ParsedRegionNpc(
                        regionX,
                        regionY,
                        tplId,
                        posX,
                        posY,
                        isSpecial,
                        customName,
                        level,
                        curFrame,
                        kind,
                        camp,
                        series,
                        scriptPath));
                }
            }
        }

        // 3. OBJ_SECTION (KObjFileHead 12 bytes + KSPObj 24-byte fixed header + szScript[nScriptNameLen])
        var objs = new List<ParsedRegionObj>();
        if (TryGetSectionSlice(regionDatBytes, sectionCount, headerSize, offsetsAreRelative, 3, out ReadOnlySpan<byte> objSlice) &&
            objSlice.Length >= 12)
        {
            uint numObjs = BinaryPrimitives.ReadUInt32LittleEndian(objSlice[..4]);
            int pos = 12;
            for (int i = 0; i < numObjs && pos + 24 <= objSlice.Length; i++)
            {
                ReadOnlySpan<byte> h = objSlice.Slice(pos, 24);
                int objDataId = BinaryPrimitives.ReadInt32LittleEndian(h[..4]);
                short state = BinaryPrimitives.ReadInt16LittleEndian(h.Slice(4, 2));
                int mpsX = BinaryPrimitives.ReadInt32LittleEndian(h.Slice(8, 4));
                int mpsY = BinaryPrimitives.ReadInt32LittleEndian(h.Slice(12, 4));
                sbyte dir = unchecked((sbyte)h[20]);
                ushort scriptLen = BinaryPrimitives.ReadUInt16LittleEndian(h.Slice(22, 2));
                pos += 24;

                if (pos + scriptLen > objSlice.Length)
                {
                    break;
                }

                string scriptPath = scriptLen > 0
                    ? LegacyPathResolver.NormalizeEnginePath(objSlice.Slice(pos, scriptLen))
                    : string.Empty;
                pos += scriptLen;

                if (objDataId >= 1)
                {
                    objs.Add(new ParsedRegionObj(
                        regionX,
                        regionY,
                        objDataId,
                        state,
                        mpsX,
                        mpsY,
                        dir,
                        scriptPath));
                }
            }
        }

        parsed = new ParsedRegionDat(regionX, regionY, obstacleCells, traps, npcs, objs);
        return true;
    }

    private static bool TryGetSectionSlice(
        ReadOnlySpan<byte> regionDatBytes,
        uint sectionCount,
        int headerSize,
        bool offsetsAreRelative,
        int sectionIndex,
        out ReadOnlySpan<byte> slice)
    {
        slice = default;
        if (sectionIndex >= sectionCount)
        {
            return false;
        }

        int descOffset = 4 + (sectionIndex * 8);
        uint rawOff = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(descOffset, 4));
        uint rawLen = BinaryPrimitives.ReadUInt32LittleEndian(regionDatBytes.Slice(descOffset + 4, 4));
        if (rawLen == 0)
        {
            return false;
        }

        int resolvedOff = offsetsAreRelative ? headerSize + (int)rawOff : (int)rawOff;
        if (resolvedOff < headerSize || resolvedOff + (int)rawLen > regionDatBytes.Length)
        {
            return false;
        }

        slice = regionDatBytes.Slice(resolvedOff, (int)rawLen);
        return true;
    }
}
