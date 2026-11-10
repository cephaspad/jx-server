using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_removeitem</c> (<c>0x60</c>, <c>5</c> bytes): removes an item (<c>RuntimeItemId</c>) from the client's inventory (`FR-012`, `FR-013`).
/// </summary>
public readonly record struct RemoveItemS2CPacket(uint RuntimeItemId)
{
    public const int PayloadSize = 5;

    public static bool TryParse(ReadOnlySpan<byte> payload, out RemoveItemS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_removeitem)
        {
            packet = default;
            return false;
        }

        packet = new RemoveItemS2CPacket(BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_removeitem;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(1, 4), RuntimeItemId);
    }
}
