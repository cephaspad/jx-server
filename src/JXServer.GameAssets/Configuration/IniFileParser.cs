using System.Text;
using JXServer.Primitives.Encodings;

namespace JXServer.GameAssets.Configuration;

/// <summary>
/// Parses legacy Kingsoft <c>.ini</c> files (<c>[Section]</c> and <c>Key=Value</c>), supporting both TCVN3 and GBK encodings.
/// </summary>
public static class IniFileParser
{
    /// <summary>
    /// Parses an INI byte span into a case-insensitive dictionary of sections and key-value pairs using TCVN3 decoding.
    /// </summary>
    public static Dictionary<string, Dictionary<string, string>> ParseSections(ReadOnlySpan<byte> rawBytes)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string text = Tcvn3Encoding.DecodeNullTerminated(rawBytes);
        string currentSection = string.Empty;

        foreach (string rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                if (!sections.ContainsKey(currentSection))
                {
                    sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                continue;
            }

            int eqIdx = line.IndexOf('=');
            if (eqIdx <= 0)
            {
                continue;
            }

            string key = line[..eqIdx].Trim();
            string val = line[(eqIdx + 1)..].Trim();

            if (!sections.TryGetValue(currentSection, out Dictionary<string, string>? map))
            {
                map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[currentSection] = map;
            }

            map[key] = val;
        }

        return sections;
    }

    /// <summary>
    /// Extracts raw GBK/ASCII byte values for integer-keyed entries in <c>maplist.ini</c> (preserving raw GBK bytes for <c>.pak</c> hashing).
    /// </summary>
    public static Dictionary<ushort, byte[]> ParseRawByteEntriesByUShortKey(ReadOnlySpan<byte> rawBytes)
    {
        var result = new Dictionary<ushort, byte[]>();
        int start = 0;
        while (start < rawBytes.Length)
        {
            int end = start;
            while (end < rawBytes.Length && rawBytes[end] != (byte)'\r' && rawBytes[end] != (byte)'\n')
            {
                end++;
            }

            ReadOnlySpan<byte> line = rawBytes[start..end];
            start = end + 1;
            while (start < rawBytes.Length && (rawBytes[start] == (byte)'\r' || rawBytes[start] == (byte)'\n'))
            {
                start++;
            }

            if (line.IsEmpty || line[0] == (byte)';' || line[0] == (byte)'[')
            {
                continue;
            }

            int eqIdx = line.IndexOf((byte)'=');
            if (eqIdx <= 0)
            {
                continue;
            }

            string keyStr = Encoding.ASCII.GetString(line[..eqIdx]).Trim();
            if (!ushort.TryParse(keyStr, out ushort mapId) || mapId == 0)
            {
                continue;
            }

            ReadOnlySpan<byte> valSpan = line[(eqIdx + 1)..];
            while (!valSpan.IsEmpty && (valSpan[0] == (byte)' ' || valSpan[0] == (byte)'\t'))
            {
                valSpan = valSpan[1..];
            }

            while (!valSpan.IsEmpty && (valSpan[^1] == (byte)' ' || valSpan[^1] == (byte)'\t'))
            {
                valSpan = valSpan[..^1];
            }

            if (!valSpan.IsEmpty)
            {
                result[mapId] = valSpan.ToArray();
            }
        }

        return result;
    }
}
