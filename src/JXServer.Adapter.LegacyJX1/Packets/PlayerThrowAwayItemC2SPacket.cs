using System;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_playerthrowawayitem</c> (<c>0x5E</c>, <c>1</c> byte): drops the item currently held in <c>pos_hand</c> onto the ground (`FR-012`).
/// </summary>
public readonly record struct PlayerThrowAwayItemC2SPacket
{
    public const int PayloadSize = 1;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerThrowAwayItemC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_playerthrowawayitem)
        {
            packet = default;
            return false;
        }

        packet = default;
        return true;
    }

    public void WritePayload(Span<byte> destination) =>
        destination[0] = ProtocolOpcodes.c2s_playerthrowawayitem;
}
