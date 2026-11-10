using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_PlayerDivideItem</c> (<c>0x85</c>, <c>10</c> bytes): splits a stackable item into <c>pos_hand</c> (`FR-012`).
/// </summary>
public readonly record struct PlayerDivideItemC2SPacket(
    uint RuntimeItemId,
    int DivideCount,
    byte ContinuouslyDivide = 0)
{
    public const int PayloadSize = 10;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerDivideItemC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_PlayerDivideItem)
        {
            packet = default;
            return false;
        }

        packet = new PlayerDivideItemC2SPacket(
            RuntimeItemId: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4)),
            DivideCount: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(5, 4)),
            ContinuouslyDivide: payload[9]);
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_PlayerDivideItem;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(1, 4), RuntimeItemId);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(5, 4), DivideCount);
        destination[9] = ContinuouslyDivide;
    }
}
