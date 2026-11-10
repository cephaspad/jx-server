namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_npcsit</c> (<c>0x71</c>, <c>2</c> bytes): posture sit/stand toggle command (`FR-007`).
/// </summary>
public readonly record struct NpcSitC2SPacket(byte SitFlag)
{
    public const int PayloadSize = 2;

    public bool IsSitting => SitFlag != 0;

    public static bool TryParse(ReadOnlySpan<byte> payload, out NpcSitC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_npcsit)
        {
            packet = default;
            return false;
        }

        packet = new NpcSitC2SPacket(payload[1]);
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_npcsit;
        destination[1] = SitFlag;
    }
}
