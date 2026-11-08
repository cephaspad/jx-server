using System.Runtime.InteropServices;
using JXServer.Primitives.Accounts;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 127-byte <c>s2c_login</c> (<c>0x41</c>) Bishop Gateway authentication response packet.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct AccountLoginResultS2CPacket
{
    public const int Size = 127;
    public const ushort ExpectedHeadSize = 0x007E;

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

    public static AccountLoginResultS2CPacket Create(
        in AccountLoginC2SPacket request,
        LoginResultCode resultCode,
        uint remainingTimeSeconds = 86_400u)
    {
        AccountLoginResultS2CPacket response = MemoryMarshal.AsRef<AccountLoginResultS2CPacket>(
            MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in request, 1)));
        response.ProtocolType = ProtocolOpcodes.s2c_login;
        response.StructSize = ExpectedHeadSize;
        response.Param = (uint)resultCode;
        response.LeftTime = remainingTimeSeconds;
        return response;
    }

    public readonly string GetUserName()
    {
        fixed (byte* ptr = Account)
        {
            return Tcvn3Encoding.DecodeNullTerminated(new ReadOnlySpan<byte>(ptr, 32));
        }
    }
}
