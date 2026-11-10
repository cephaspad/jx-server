using System;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_autoattacknpc</c> (<c>0x87</c>, <c>26</c> bytes): auto-play / emergency envelope command (<c>SubCommand</c> <c>0x02</c> Town Portal or <c>0x03</c> wrapped <c>0x4B</c>/<c>0x4D</c>).
/// </summary>
public readonly record struct AutoAttackNpcC2SPacket(
    byte SubCommand,
    ReadOnlyMemory<byte> InnerPayload)
{
    public const int PayloadSize = 26;
    public const byte SubCommandEmergencyTownPortal = 0x02;
    public const byte SubCommandWrappedAction = 0x03;

    public static bool TryParse(ReadOnlySpan<byte> payload, out AutoAttackNpcC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_autoattacknpc)
        {
            packet = default;
            return false;
        }

        byte subCmd = payload[1];
        byte[] inner = payload.Slice(2, 24).ToArray();
        packet = new AutoAttackNpcC2SPacket(subCmd, inner);
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination.Slice(0, PayloadSize).Clear();
        destination[0] = ProtocolOpcodes.c2s_autoattacknpc;
        destination[1] = SubCommand;
        ReadOnlySpan<byte> span = InnerPayload.Span;
        int copyLen = Math.Min(24, span.Length);
        span.Slice(0, copyLen).CopyTo(destination.Slice(2, copyLen));
    }
}
