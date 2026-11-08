using System.Text;

namespace JXServer.Primitives.Encodings;

/// <summary>
/// Provides GBK (Code Page 936) text decoding and encoding for legacy Kingsoft <c>.ini</c>, <c>.txt</c>, and <c>.pak</c> paths.
/// </summary>
public static class GbkEncoding
{
    private static readonly Encoding GbkInstance = CreateGbkEncoding();

    /// <summary>
    /// Gets the GBK (CP936) <see cref="Encoding"/> instance.
    /// </summary>
    public static Encoding Instance => GbkInstance;

    /// <summary>
    /// Decodes a null-terminated GBK byte span into a Unicode string.
    /// </summary>
    public static string DecodeNullTerminated(ReadOnlySpan<byte> source)
    {
        int nullIdx = source.IndexOf((byte)0);
        if (nullIdx >= 0)
        {
            source = source[..nullIdx];
        }

        return source.IsEmpty ? string.Empty : GbkInstance.GetString(source);
    }

    /// <summary>
    /// Encodes a Unicode string into GBK bytes.
    /// </summary>
    public static byte[] EncodeBytes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == 0 ? [] : GbkInstance.GetBytes(text);
    }

    private static Encoding CreateGbkEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(936);
    }
}
