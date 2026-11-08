using System.Runtime.InteropServices;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 5-byte <c>s2c_ping</c> (<c>0x82</c>) packet emitted periodically by the server.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PingS2CPacket
{
    public const int Size = 5;

    public byte ProtocolType;
    public uint ServerTimestamp;

    public static PingS2CPacket Create(uint serverTimestamp) => new()
    {
        ProtocolType = ProtocolOpcodes.s2c_ping,
        ServerTimestamp = serverTimestamp
    };
}
