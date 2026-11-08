using System.Runtime.InteropServices;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 5-byte <c>s2c_pingreply</c> (<c>0x99</c>) packet echoing the client's timestamp from <c>0x70</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PingReplyS2CPacket
{
    public const int Size = 5;

    public byte ProtocolType;
    public uint EchoedClientTimestamp;

    public static PingReplyS2CPacket Create(uint echoedClientTimestamp) => new()
    {
        ProtocolType = ProtocolOpcodes.s2c_pingreply,
        EchoedClientTimestamp = echoedClientTimestamp
    };
}
