using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Raw byte-level <c>KTabFile</c> reader preserving exact physical 0-based line indices (<c>lineIdx</c>),
/// raw byte cells, and <c>Row 2</c> (<c>lineIdx == 1</c>) default scalar inheritance (`JXData Standard v1`).
/// </summary>
public sealed class KTabFileTable
{
    private readonly byte[] _buffer;
    private readonly (int Offset, int Length)[][] _rows;
    private readonly Dictionary<string, int> _headerMap;

    /// <summary>
    /// Total physical lines in the table (including header row <c>0</c>, excluding only a trailing empty EOF line after <c>\n</c>).
    /// </summary>
    public int LineCount => _rows.Length;

    /// <summary>
    /// Number of data rows (<c>LineCount - 1</c>, corresponding to <c>lineIdx = 1 .. LineCount - 1</c>).
    /// </summary>
    public int DataRowCount => Math.Max(0, _rows.Length - 1);

    private KTabFileTable(byte[] buffer, (int Offset, int Length)[][] rows, Dictionary<string, int> headerMap)
    {
        _buffer = buffer;
        _rows = rows;
        _headerMap = headerMap;
    }

    public static KTabFileTable Load(string filePath)
    {
        byte[] raw = File.ReadAllBytes(filePath);
        return Parse(raw);
    }

    public static KTabFileTable Parse(byte[] rawBytes)
    {
        ArgumentNullException.ThrowIfNull(rawBytes);

        var lineRanges = new List<(int Offset, int Length)>();
        int start = 0;
        for (int i = 0; i <= rawBytes.Length; i++)
        {
            if (i == rawBytes.Length || rawBytes[i] == (byte)'\n')
            {
                int len = i - start;
                if (len > 0 && rawBytes[start + len - 1] == (byte)'\r')
                {
                    len--;
                }

                lineRanges.Add((start, len));
                start = i + 1;
            }
        }

        // Only trim the final empty slice produced by a trailing newline at EOF
        if (lineRanges.Count > 0 && lineRanges[^1].Length == 0 && rawBytes.Length > 0 && rawBytes[^1] == (byte)'\n')
        {
            lineRanges.RemoveAt(lineRanges.Count - 1);
        }

        var rows = new (int Offset, int Length)[lineRanges.Count][];
        for (int r = 0; r < lineRanges.Count; r++)
        {
            (int lineOff, int lineLen) = lineRanges[r];
            rows[r] = SplitTabCells(rawBytes, lineOff, lineLen);
        }

        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (rows.Length > 0)
        {
            for (int c = 0; c < rows[0].Length; c++)
            {
                string headerGbk = HybridLegacyTextDecoder.DecodeSmart(GetRawCellStatic(rawBytes, rows, 0, c), preferVietnamese: false).Trim();
                if (headerGbk.Length > 0)
                {
                    headerMap.TryAdd(headerGbk, c);
                }

                string headerAscii = Encoding.ASCII.GetString(GetRawCellStatic(rawBytes, rows, 0, c)).Trim();
                if (headerAscii.Length > 0)
                {
                    headerMap.TryAdd(headerAscii, c);
                }
            }
        }

        return new KTabFileTable(rawBytes, rows, headerMap);
    }

    public int FindColumn(string columnName, int fallback = -1) =>
        _headerMap.TryGetValue(columnName, out int idx) ? idx : fallback;

    public int GetColumnCount(int lineIdx) =>
        (uint)lineIdx < (uint)_rows.Length ? _rows[lineIdx].Length : 0;

    public bool IsRowCompletelyBlankOrTabsOnly(int lineIdx)
    {
        if ((uint)lineIdx >= (uint)_rows.Length)
        {
            return true;
        }

        (int Offset, int Length)[] row = _rows[lineIdx];
        for (int c = 0; c < row.Length; c++)
        {
            ReadOnlySpan<byte> cell = _buffer.AsSpan(row[c].Offset, row[c].Length);
            for (int i = 0; i < cell.Length; i++)
            {
                if (cell[i] != (byte)' ' && cell[i] != (byte)'\t' && cell[i] != 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public ReadOnlySpan<byte> GetRawCell(int lineIdx, int colIdx, bool inheritRow2Default = false)
    {
        ReadOnlySpan<byte> span = GetRawCellStatic(_buffer, _rows, lineIdx, colIdx);
        if (!span.IsEmpty || !inheritRow2Default || lineIdx <= 1)
        {
            return span;
        }

        return GetRawCellStatic(_buffer, _rows, 1, colIdx);
    }

    public string GetString(int lineIdx, int colIdx, bool preferVietnamese = true, bool inheritRow2Default = false)
    {
        ReadOnlySpan<byte> raw = GetRawCell(lineIdx, colIdx, inheritRow2Default);
        return raw.IsEmpty ? string.Empty : HybridLegacyTextDecoder.DecodeSmart(raw, preferVietnamese).Trim();
    }

    public string GetEnginePath(int lineIdx, int colIdx, bool inheritRow2Default = false)
    {
        ReadOnlySpan<byte> raw = GetRawCell(lineIdx, colIdx, inheritRow2Default);
        return raw.IsEmpty ? string.Empty : LegacyPathResolver.NormalizeEnginePath(raw);
    }

    public int GetInt(int lineIdx, int colIdx, int defaultValue = 0, bool inheritRow2Default = false)
    {
        ReadOnlySpan<byte> raw = GetRawCell(lineIdx, colIdx, inheritRow2Default);
        if (raw.IsEmpty)
        {
            return defaultValue;
        }

        string text = Encoding.Latin1.GetString(raw).Trim();
        if (text.EndsWith('%'))
        {
            text = text[..^1].Trim();
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int val))
        {
            return val;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double dVal))
        {
            return (int)dVal;
        }

        return defaultValue;
    }

    public long GetLong(int lineIdx, int colIdx, long defaultValue = 0L, bool inheritRow2Default = false)
    {
        ReadOnlySpan<byte> raw = GetRawCell(lineIdx, colIdx, inheritRow2Default);
        if (raw.IsEmpty)
        {
            return defaultValue;
        }

        string text = Encoding.Latin1.GetString(raw).Trim();
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long val) ? val : defaultValue;
    }

    public double GetDouble(int lineIdx, int colIdx, double defaultValue = 0.0, bool inheritRow2Default = false)
    {
        ReadOnlySpan<byte> raw = GetRawCell(lineIdx, colIdx, inheritRow2Default);
        if (raw.IsEmpty)
        {
            return defaultValue;
        }

        string text = Encoding.Latin1.GetString(raw).Trim();
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ? val : defaultValue;
    }

    private static ReadOnlySpan<byte> GetRawCellStatic(byte[] buffer, (int Offset, int Length)[][] rows, int lineIdx, int colIdx)
    {
        if ((uint)lineIdx >= (uint)rows.Length)
        {
            return default;
        }

        (int Offset, int Length)[] row = rows[lineIdx];
        if ((uint)colIdx >= (uint)row.Length)
        {
            return default;
        }

        (int off, int len) = row[colIdx];
        return len <= 0 ? default : buffer.AsSpan(off, len);
    }

    private static (int Offset, int Length)[] SplitTabCells(byte[] buffer, int lineOffset, int lineLength)
    {
        if (lineLength <= 0)
        {
            return [(lineOffset, 0)];
        }

        int tabs = 0;
        int end = lineOffset + lineLength;
        for (int i = lineOffset; i < end; i++)
        {
            if (buffer[i] == (byte)'\t')
            {
                tabs++;
            }
        }

        var cells = new (int Offset, int Length)[tabs + 1];
        int cellIdx = 0;
        int cellStart = lineOffset;
        for (int i = lineOffset; i <= end; i++)
        {
            if (i == end || buffer[i] == (byte)'\t')
            {
                cells[cellIdx++] = (cellStart, i - cellStart);
                cellStart = i + 1;
            }
        }

        return cells;
    }
}
