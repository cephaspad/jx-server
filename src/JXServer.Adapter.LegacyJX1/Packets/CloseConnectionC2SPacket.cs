using System.Runtime.InteropServices;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 1-byte <c>c2sCloseConnection</c> (<c>0x9B</c>) packet for clean player logout (`FR-011`).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CloseConnectionC2SPacket
{
    public const int Size = 1;

    public byte ProtocolType;

    public static CloseConnectionC2SPacket Create() => new()
    {
        ProtocolType = ProtocolOpcodes.c2sCloseConnection
    };

    public static bool TryParse(ReadOnlySpan<byte> payload, out CloseConnectionC2SPacket packet)
    {
        if (payload.Length != Size || payload[0] != ProtocolOpcodes.c2sCloseConnection)
        {
            packet = default;
            return false;
        }

        packet = MemoryMarshal.Read<CloseConnectionC2SPacket>(payload);
        return true;
    }
}
