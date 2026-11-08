using System.Runtime.InteropServices;
using JXServer.Primitives.Accounts;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 127-byte <c>c2s_login</c> (<c>0x41</c>) Bishop Gateway authentication request packet.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct AccountLoginC2SPacket
{
    public const int Size = 127;
    public const ushort ExpectedHeadSize = 0x007E;
    public const uint LoginActionRequestParam = 0x00050000u;

    public byte ProtocolType;
    public ushort StructSize;
    public uint Param;
    public fixed byte Account[32];
    public fixed byte PasswordHash[64];
    public uint LeftTime;
    public uint ProtocolVersion;
    public uint KickOnline;
    public uint LimitTimeFlag;
    public uint LimitTimeOnlineSeconds;
    public uint LimitTimeOfflineSeconds;

    public static AccountLoginC2SPacket Create(
        string userName,
        string passwordOrMd5Hex,
        bool kickOnline = true,
        uint protocolVersion = AccountConsts.RequiredProtocolVersion)
    {
        AccountLoginC2SPacket pkt = default;
        pkt.ProtocolType = ProtocolOpcodes.c2s_login;
        pkt.StructSize = ExpectedHeadSize;
        pkt.Param = LoginActionRequestParam;
        pkt.ProtocolVersion = protocolVersion;
        pkt.KickOnline = kickOnline ? 1u : 0u;

        Tcvn3Encoding.WriteFixedString(userName, new Span<byte>(pkt.Account, 32));
        string md5Upper = KsgPasswordHasher.NormalizeClientPasswordOrHash(passwordOrMd5Hex);
        Tcvn3Encoding.WriteFixedString(md5Upper, new Span<byte>(pkt.PasswordHash, 64));
        return pkt;
    }

    public readonly string GetUserName()
    {
        fixed (byte* ptr = Account)
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

    public static bool TryParse(ReadOnlySpan<byte> payload, out AccountLoginC2SPacket packet)
    {
        if (payload.Length != Size || payload[0] != ProtocolOpcodes.c2s_login)
        {
            packet = default;
            return false;
        }

        packet = MemoryMarshal.Read<AccountLoginC2SPacket>(payload);
        return packet.StructSize == ExpectedHeadSize;
    }
}
