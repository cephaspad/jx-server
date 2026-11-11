using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using JXServer.Primitives.WorldZones;

namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Encodes and decodes a <c>16 * 32 = 512</c> region cell obstacle matrix as a Base64 sequence of
/// 6-byte Little-Endian RLE pairs (<c>UInt16 runLength</c> in <c>1..512</c> + <c>Int32 cellValue</c>)
/// per <c>JXData Standard v1</c> (<c>map.schema.json</c>).
/// </summary>
public static class ObstacleRleCodec
{
    public const int RegionCellCount = WorldCoordinateConsts.RegionWidthCells * WorldCoordinateConsts.RegionHeightCells;

    /// <summary>
    /// Encodes 512 cell obstacle values in <c>(cellX * 32 + cellY)</c> order into Base64,
    /// returning <see langword="null"/> when all 512 cells are <c>0</c> (<c>Obstacle_NULL</c>).
    /// </summary>
    public static string? EncodeCellsToBase64(ReadOnlySpan<int> cells)
    {
        if (cells.Length != RegionCellCount)
        {
            throw new ArgumentException($"Expected exactly {RegionCellCount} cells.", nameof(cells));
        }

        bool anyNonZero = false;
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] != 0)
            {
                anyNonZero = true;
                break;
            }
        }

        if (!anyNonZero)
        {
            return null;
        }

        var runs = new List<(ushort RunLength, int Value)>(32);
        int currentVal = cells[0];
        ushort runLen = 1;
        for (int i = 1; i < cells.Length; i++)
        {
            if (cells[i] == currentVal)
            {
                runLen++;
            }
            else
            {
                runs.Add((runLen, currentVal));
                currentVal = cells[i];
                runLen = 1;
            }
        }

        runs.Add((runLen, currentVal));

        byte[] bytes = new byte[runs.Count * 6];
        for (int r = 0; r < runs.Count; r++)
        {
            int offset = r * 6;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), runs[r].RunLength);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 2, 4), runs[r].Value);
        }

        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Decodes a Base64 6-byte LE RLE string back into a <c>512</c>-element <see cref="int"/> array.
    /// </summary>
    public static int[] DecodeBase64ToCells(string obstacleRleBase64)
    {
        int[] cells = new int[RegionCellCount];
        if (string.IsNullOrWhiteSpace(obstacleRleBase64))
        {
            return cells;
        }

        byte[] raw = Convert.FromBase64String(obstacleRleBase64);
        if (raw.Length % 6 != 0)
        {
            throw new FormatException($"Invalid obstacleRleBase64 byte length {raw.Length}; must be a multiple of 6.");
        }

        int pos = 0;
        for (int offset = 0; offset < raw.Length; offset += 6)
        {
            ushort runLength = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(offset, 2));
            int cellValue = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(offset + 2, 4));
            if (runLength == 0 || pos + runLength > RegionCellCount)
            {
                throw new FormatException($"Invalid RLE runLength {runLength} at cell index {pos}.");
            }

            cells.AsSpan(pos, runLength).Fill(cellValue);
            pos += runLength;
        }

        if (pos != RegionCellCount)
        {
            throw new FormatException($"Decoded RLE total cell count was {pos}, expected {RegionCellCount}.");
        }

        return cells;
    }
}
