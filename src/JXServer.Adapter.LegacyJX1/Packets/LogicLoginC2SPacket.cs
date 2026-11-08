using System.Runtime.InteropServices;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 17-byte <c>c2s_logiclogin</c> (<c>0x42</c>) packet presenting the 16-byte single-use handoff <c>GUID</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct LogicLoginC2SPacket
{
    public const int Size = 17;

    public byte ProtocolType;
    public fixed byte Guid[16];

    public static LogicLoginC2SPacket Create(Guid ticketGuid)
    {
        LogicLoginC2SPacket pkt = default;
        pkt.ProtocolType = ProtocolOpcodes.c2s_logiclogin;
        ticketGuid.TryWriteBytes(new Span<byte>(pkt.Guid, 16));
        return pkt;
    }

    public readonly Guid GetTicketGuid()
    {
        fixed (byte* ptr = Guid)
        {
            return new Guid(new ReadOnlySpan<byte>(ptr, 16));
        }
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out LogicLoginC2SPacket packet)
    {
        if (payload.Length != Size || payload[0] != ProtocolOpcodes.c2s_logiclogin)
        {
            packet = default;
            return false;
        }

        packet = MemoryMarshal.Read<LogicLoginC2SPacket>(payload);
        return true;
    }
}
