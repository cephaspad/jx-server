using System.Runtime.InteropServices;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 9-byte <c>c2s_ping</c> (<c>0x70</c>) reply packet echoing the server's timestamp and carrying the client's timestamp.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PingReplyC2SPacket
{
    public const int Size = 9;

    public byte ProtocolType;
    public uint EchoedServerTimestamp;
    public uint ClientTimestamp;

    public static PingReplyC2SPacket Create(uint echoedServerTimestamp, uint clientTimestamp) => new()
    {
        ProtocolType = ProtocolOpcodes.c2s_ping,
        EchoedServerTimestamp = echoedServerTimestamp,
        ClientTimestamp = clientTimestamp
    };

    public static bool TryParse(ReadOnlySpan<byte> payload, out PingReplyC2SPacket packet)
    {
        if (payload.Length != Size || payload[0] != ProtocolOpcodes.c2s_ping)
        {
            packet = default;
            return false;
        }

        packet = MemoryMarshal.Read<PingReplyC2SPacket>(payload);
        return true;
    }
}
