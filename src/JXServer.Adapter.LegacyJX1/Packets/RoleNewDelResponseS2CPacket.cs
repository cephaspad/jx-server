using System.Runtime.InteropServices;
using JXServer.Primitives.Encodings;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 42-byte <c>s2c_rolenewdelresponse</c> (<c>0x8C</c>) packet returned after character creation (<c>0x45</c>) or deletion (<c>0x3A</c>).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct RoleNewDelResponseS2CPacket
{
    public const int Size = 42;

    public byte ProtocolType;
    public uint Identity;
    public uint NativePlaceId;
    public fixed byte szRoleName[32];
    public byte Succeeded;

    public static RoleNewDelResponseS2CPacket Create(
        string roleNameUnicode,
        bool succeeded,
        uint nativePlaceId = 0,
        uint identity = 0)
    {
        RoleNewDelResponseS2CPacket pkt = default;
        pkt.ProtocolType = ProtocolOpcodes.s2c_rolenewdelresponse;
        pkt.Identity = identity;
        pkt.NativePlaceId = nativePlaceId;
        pkt.Succeeded = succeeded ? (byte)1 : (byte)0;
        Tcvn3Encoding.WriteFixedString(roleNameUnicode, new Span<byte>(pkt.szRoleName, 32));
        return pkt;
    }

    public readonly string GetRoleName()
    {
        fixed (byte* ptr = szRoleName)
        {
            return Tcvn3Encoding.DecodeNullTerminated(new ReadOnlySpan<byte>(ptr, 32));
        }
    }
}
