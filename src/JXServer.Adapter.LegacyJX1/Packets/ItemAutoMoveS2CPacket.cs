using System;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_ItemAutoMove</c> (<c>0x8E</c>, <c>1</c> byte): unlocks the client's inventory drag-and-drop lock after an item move (`FR-012`).
/// </summary>
public readonly record struct ItemAutoMoveS2CPacket
{
    public const int PayloadSize = 1;

    public static bool TryParse(ReadOnlySpan<byte> payload, out ItemAutoMoveS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_itemautomove)
        {
            packet = default;
            return false;
        }

        packet = default;
        return true;
    }

    public void WritePayload(Span<byte> destination) =>
        destination[0] = ProtocolOpcodes.s2c_itemautomove;
}
