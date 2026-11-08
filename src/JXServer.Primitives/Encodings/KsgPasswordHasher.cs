using System.Security.Cryptography;
using System.Text;
using JXServer.Primitives.Accounts;

namespace JXServer.Primitives.Encodings;

/// <summary>
/// Provides 32-character uppercase hexadecimal MD5 password hashing and constant-time digest verification
/// matching <c>KSG_Password</c> (<c>char szPassword[64]</c>).
/// </summary>
public static class KsgPasswordHasher
{
    /// <summary>
    /// Computes the 32-character uppercase hexadecimal MD5 digest of a plaintext password.
    /// </summary>
    public static string HashPlaintext(ReadOnlySpan<char> plaintextPassword)
    {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(plaintextPassword.Length);
        Span<byte> utf8Bytes = maxBytes <= 256 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        int byteCount = Encoding.UTF8.GetBytes(plaintextPassword, utf8Bytes);

        Span<byte> hashBytes = stackalloc byte[MD5.HashSizeInBytes];
        MD5.HashData(utf8Bytes[..byteCount], hashBytes);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Normalizes a client-supplied password or MD5 hex string into a canonical 32-character uppercase hex digest.
    /// If the input is already a 32-character hexadecimal string, it is normalized to uppercase; otherwise its MD5 digest is computed.
    /// </summary>
    public static string NormalizeClientPasswordOrHash(ReadOnlySpan<char> passwordOrHash)
    {
        ReadOnlySpan<char> trimmed = passwordOrHash.TrimEnd('\0').Trim();
        if (IsHexDigest32(trimmed))
        {
            return trimmed.ToString().ToUpperInvariant();
        }

        return HashPlaintext(trimmed);
    }

    /// <summary>
    /// Determines whether the input span is a valid 32-character hexadecimal MD5 digest.
    /// </summary>
    public static bool IsHexDigest32(ReadOnlySpan<char> candidate)
    {
        if (candidate.Length != AccountConsts.PasswordMd5HexLength)
        {
            return false;
        }

        for (int i = 0; i < candidate.Length; i++)
        {
            if (!Uri.IsHexDigit(candidate[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Performs a constant-time comparison between a stored 32-character uppercase MD5 digest and a candidate password or hash.
    /// </summary>
    public static bool Verify(ReadOnlySpan<char> storedUpperHexDigest, ReadOnlySpan<char> candidatePasswordOrHash)
    {
        if (storedUpperHexDigest.Length != AccountConsts.PasswordMd5HexLength)
        {
            return false;
        }

        string normalizedCandidate = NormalizeClientPasswordOrHash(candidatePasswordOrHash);
        ReadOnlySpan<byte> left = MemoryMarshalAsBytes(storedUpperHexDigest);
        ReadOnlySpan<byte> right = MemoryMarshalAsBytes(normalizedCandidate.AsSpan());
        return CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static ReadOnlySpan<byte> MemoryMarshalAsBytes(ReadOnlySpan<char> chars) =>
        System.Runtime.InteropServices.MemoryMarshal.AsBytes(chars);
}
