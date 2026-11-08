using System.Runtime.InteropServices;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 129-byte <c>c2s_roleserver_deleteplayer</c> (<c>0x3A</c>) packet for deleting an existing character (`FR-006`).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct DeleteCharacterC2SPacket
{
    public const int Size = 129;

    public byte ProtocolType;
    public uint Identity;
    public fixed byte AccountName[32];
    public fixed byte PasswordHash[64];
    public fixed byte RoleName[28];

    public static DeleteCharacterC2SPacket Create(
        string accountName,
        string passwordOrMd5Hex,
        string roleNameUnicode,
        uint identity = 0)
    {
        DeleteCharacterC2SPacket pkt = default;
        pkt.ProtocolType = ProtocolOpcodes.c2s_roleserver_deleteplayer;
        pkt.Identity = identity;

        Tcvn3Encoding.WriteFixedString(accountName, new Span<byte>(pkt.AccountName, 32));
        if (!string.IsNullOrWhiteSpace(passwordOrMd5Hex))
        {
            string md5 = KsgPasswordHasher.NormalizeClientPasswordOrHash(passwordOrMd5Hex);
            Tcvn3Encoding.WriteFixedString(md5, new Span<byte>(pkt.PasswordHash, 64));
        }

        Tcvn3Encoding.WriteFixedString(roleNameUnicode, new Span<byte>(pkt.RoleName, 28));
        return pkt;
    }

    public readonly string GetAccountName()
    {
        fixed (byte* ptr = AccountName)
        {
            return Tcvn3Encoding.DecodeNullTerminated(new ReadOnlySpan<byte>(ptr, 32));
        }
    }

    public readonly string GetPasswordHash()
    {
        fixed (byte* ptr = PasswordHash)
        {
            return Tcvn3Encoding.DecodeNullTerminated(new ReadOnlySpan<byte>(ptr, 64));
        }
    }

    public readonly string GetRoleName()
    {
        fixed (byte* ptr = RoleName)
        {
            return Tcvn3Encoding.DecodeNullTerminated(new ReadOnlySpan<byte>(ptr, 28));
        }
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out DeleteCharacterC2SPacket packet)
    {
        if (payload.Length != Size || payload[0] != ProtocolOpcodes.c2s_roleserver_deleteplayer)
        {
            packet = default;
            return false;
        }

        packet = MemoryMarshal.Read<DeleteCharacterC2SPacket>(payload);
        return true;
    }
}
