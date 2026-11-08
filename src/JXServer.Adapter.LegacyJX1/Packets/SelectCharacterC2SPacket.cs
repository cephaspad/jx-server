using System.Buffers.Binary;
using System.Runtime.InteropServices;
using JXServer.Adapter.LegacyJX1.Ciphers;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 41-byte <c>c2s_dbplayerselect</c> (<c>0x64</c>) packet with the <c>0x38BC</c> second-layer 16-bit XOR cipher
/// applied to the first <c>nNameLen</c> (<c>strlen(szRoleName) - 1</c>) bytes at offset <c>+0x09</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct SelectCharacterC2SPacket
{
    public const int Size = 41;

    public byte ProtocolType;
    public uint Identity;
    public int NameLen;
    public fixed byte szRoleName[32];

    public static SelectCharacterC2SPacket Create(string roleNameUnicode, uint identity = 0)
    {
        byte[] tcvnBytes = Tcvn3Encoding.EncodeBytes(roleNameUnicode);
        if (tcvnBytes.Length is < 2 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(roleNameUnicode),
                $"TCVN3 role name byte length must be 2..31 (got {tcvnBytes.Length}).");
        }

        SelectCharacterC2SPacket pkt = default;
        pkt.ProtocolType = ProtocolOpcodes.c2s_dbplayerselect;
        pkt.Identity = identity;
        pkt.NameLen = tcvnBytes.Length - 1;

        Span<byte> nameSpan = new(pkt.szRoleName, 32);
        nameSpan.Clear();
        tcvnBytes.CopyTo(nameSpan);
        SecondLayerXorCipher.TransformSelectRoleName(nameSpan, pkt.NameLen);
        return pkt;
    }

    public readonly bool TryDecodeRoleName(out string roleNameUnicode)
    {
        roleNameUnicode = string.Empty;
        if (NameLen is < 1 or > 31)
        {
            return false;
        }

        Span<byte> copy = stackalloc byte[32];
        fixed (byte* ptr = szRoleName)
        {
            new ReadOnlySpan<byte>(ptr, 32).CopyTo(copy);
        }

        SecondLayerXorCipher.TransformSelectRoleName(copy, NameLen);
        roleNameUnicode = Tcvn3Encoding.DecodeNullTerminated(copy[..(NameLen + 1)]);
        return !string.IsNullOrWhiteSpace(roleNameUnicode);
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out SelectCharacterC2SPacket packet)
    {
        if (payload.Length != Size || payload[0] != ProtocolOpcodes.c2s_dbplayerselect)
        {
            packet = default;
            return false;
        }

        packet = MemoryMarshal.Read<SelectCharacterC2SPacket>(payload);
        return packet.NameLen is >= 1 and <= 31;
    }
}
