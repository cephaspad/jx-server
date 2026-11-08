using System.Collections.Frozen;

namespace JXServer.Primitives.Encodings;

/// <summary>
/// Zero-allocation transcoder between standard .NET Unicode (UTF-16/UTF-8) and TCVN3 (ABC / VN3)
/// fixed-length byte buffers used by JX Linux packets and settings files.
/// </summary>
public static class Tcvn3Encoding
{
    private static readonly char[] ByteToCharTable = CreateByteToCharTable();
    private static readonly FrozenDictionary<char, byte> CharToByteMap = CreateCharToByteMap();

    /// <summary>
    /// Decodes a null-terminated TCVN3/ASCII byte span into a Unicode string.
    /// </summary>
    public static string DecodeNullTerminated(ReadOnlySpan<byte> source)
    {
        int nullIdx = source.IndexOf((byte)0);
        if (nullIdx >= 0)
        {
            source = source[..nullIdx];
        }

        if (source.IsEmpty)
        {
            return string.Empty;
        }

        Span<char> chars = source.Length <= 256 ? stackalloc char[source.Length] : new char[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            chars[i] = ByteToCharTable[source[i]];
        }

        return new string(chars);
    }

    /// <summary>
    /// Encodes a Unicode string into a TCVN3 byte array (without null terminator).
    /// </summary>
    public static byte[] EncodeBytes(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return [];
        }

        byte[] buffer = new byte[text.Length];
        int written = EncodeToSpan(text, buffer);
        return written == buffer.Length ? buffer : buffer[..written];
    }

    /// <summary>
    /// Encodes a Unicode string into a fixed-size destination span and null-pads the remainder.
    /// Returns the number of non-null content bytes written.
    /// </summary>
    public static int WriteFixedString(ReadOnlySpan<char> text, Span<byte> destination)
    {
        destination.Clear();
        if (text.IsEmpty || destination.IsEmpty)
        {
            return 0;
        }

        int maxBytes = destination.Length - 1;
        int count = Math.Min(text.Length, maxBytes);
        for (int i = 0; i < count; i++)
        {
            char ch = text[i];
            if (CharToByteMap.TryGetValue(ch, out byte tcvnByte))
            {
                destination[i] = tcvnByte;
            }
            else if (ch < 0x80)
            {
                destination[i] = (byte)ch;
            }
            else
            {
                destination[i] = (byte)'?';
            }
        }

        return count;
    }

    /// <summary>
    /// Computes the exact TCVN3 byte length of a Unicode string.
    /// </summary>
    public static int GetByteCount(ReadOnlySpan<char> text) => text.Length;

    private static int EncodeToSpan(ReadOnlySpan<char> text, Span<byte> destination)
    {
        int count = Math.Min(text.Length, destination.Length);
        for (int i = 0; i < count; i++)
        {
            char ch = text[i];
            if (CharToByteMap.TryGetValue(ch, out byte b))
            {
                destination[i] = b;
            }
            else if (ch < 0x80)
            {
                destination[i] = (byte)ch;
            }
            else
            {
                destination[i] = (byte)'?';
            }
        }

        return count;
    }

    private static char[] CreateByteToCharTable()
    {
        char[] table = new char[256];
        for (int i = 0; i < 256; i++)
        {
            table[i] = (char)i;
        }

        (byte Code, char Unicode)[] mappings =
        [
            (0xA1, 'ă'), (0xA2, 'â'), (0xA3, 'ê'), (0xA4, 'ô'), (0xA5, 'ơ'), (0xA6, 'ư'), (0xA7, 'đ'),
            (0xA8, 'Ă'), (0xA9, 'Â'), (0xAA, 'Ê'), (0xAB, 'Ô'), (0xAC, 'Ơ'), (0xAD, 'Ư'), (0xAE, 'Đ'),
            (0xB5, 'à'), (0xB6, 'ả'), (0xB7, 'ã'), (0xB8, 'á'), (0xB9, 'ạ'),
            (0xBB, 'ằ'), (0xBC, 'ẳ'), (0xBD, 'ẵ'), (0xBE, 'ắ'), (0xC6, 'ặ'),
            (0xC7, 'ầ'), (0xC8, 'ẩ'), (0xC9, 'ẫ'), (0xCA, 'ấ'), (0xCB, 'ậ'),
            (0xCC, 'è'), (0xCE, 'ẻ'), (0xCF, 'ẽ'), (0xD0, 'é'), (0xD1, 'ẹ'),
            (0xD2, 'ề'), (0xD3, 'ể'), (0xD4, 'ễ'), (0xD5, 'ế'), (0xD6, 'ệ'),
            (0xD7, 'ì'), (0xD8, 'ỉ'), (0xDC, 'ĩ'), (0xDD, 'í'), (0xDE, 'ị'),
            (0xDF, 'ò'), (0xE1, 'ỏ'), (0xE2, 'õ'), (0xE3, 'ó'), (0xE4, 'ọ'),
            (0xE5, 'ồ'), (0xE6, 'ổ'), (0xE7, 'ỗ'), (0xE8, 'ố'), (0xE9, 'ộ'),
            (0xEA, 'ờ'), (0xEB, 'ở'), (0xEC, 'ỡ'), (0xED, 'ớ'), (0xEE, 'ợ'),
            (0xEF, 'ù'), (0xF1, 'ủ'), (0xF2, 'ũ'), (0xF3, 'ú'), (0xF4, 'ụ'),
            (0xF5, 'ừ'), (0xF6, 'ử'), (0xF7, 'ữ'), (0xF8, 'ứ'), (0xF9, 'ự'),
            (0xFA, 'ỳ'), (0xFB, 'ỷ'), (0xFC, 'ỹ'), (0xFD, 'ý'), (0xFE, 'ỵ')
        ];

        foreach (var (code, unicode) in mappings)
        {
            table[code] = unicode;
        }

        return table;
    }

    private static FrozenDictionary<char, byte> CreateCharToByteMap()
    {
        var dict = new Dictionary<char, byte>();
        for (int i = 0x80; i < 256; i++)
        {
            char ch = ByteToCharTable[i];
            if (ch != (char)i)
            {
                dict[ch] = (byte)i;
            }
        }

        return dict.ToFrozenDictionary();
    }
}
