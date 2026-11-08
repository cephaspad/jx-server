using JXServer.Primitives.Encodings;

namespace JXServer.GameAssets.Configuration;

/// <summary>
/// Parses legacy tab-delimited (<c>.txt</c>) settings tables with a header row and TCVN3/ASCII columns.
/// </summary>
public static class TabFileParser
{
    /// <summary>
    /// Parses a tab-delimited byte span into a list of case-insensitive column-name to value dictionaries.
    /// </summary>
    public static List<Dictionary<string, string>> ParseRows(ReadOnlySpan<byte> rawBytes)
    {
        var rows = new List<Dictionary<string, string>>();
        string text = Tcvn3Encoding.DecodeNullTerminated(rawBytes);
        string[] lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            return rows;
        }

        string[] headers = lines[0].Split('\t');
        for (int i = 0; i < headers.Length; i++)
        {
            headers[i] = headers[i].Trim();
        }

        for (int lineIdx = 1; lineIdx < lines.Length; lineIdx++)
        {
            string line = lines[lineIdx];
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cols = line.Split('\t');
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int count = Math.Min(headers.Length, cols.Length);
            for (int colIdx = 0; colIdx < count; colIdx++)
            {
                if (!string.IsNullOrEmpty(headers[colIdx]))
                {
                    row[headers[colIdx]] = cols[colIdx].Trim();
                }
            }

            if (row.Count > 0)
            {
                rows.Add(row);
            }
        }

        return rows;
    }
}
