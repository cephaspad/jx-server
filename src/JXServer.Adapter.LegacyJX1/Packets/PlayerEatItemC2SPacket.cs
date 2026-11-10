using System;
using System.Buffers.Binary;
using JXServer.Primitives.Items;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_playereatitem</c> (<c>0x59</c>, <c>16</c> bytes): requests consuming a potion or Town Portal scroll (`FR-013`).
/// </summary>
public readonly record struct PlayerEatItemC2SPacket(
    byte GridX,
    uint RuntimeItemId,
    byte GridY,
    ItemContainerPlace ContainerPlace,
    uint AntiBotToken = 0,
    uint TargetId = 0)
{
    public const int PayloadSize = 16;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerEatItemC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_playereatitem)
        {
            packet = default;
            return false;
        }

        packet = new PlayerEatItemC2SPacket(
            GridX: payload[1],
            RuntimeItemId: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(2, 4)),
            GridY: payload[6],
            ContainerPlace: (ItemContainerPlace)payload[7],
            AntiBotToken: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(8, 4)),
            TargetId: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(12, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_playereatitem;
        destination[1] = GridX;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(2, 4), RuntimeItemId);
        destination[6] = GridY;
        destination[7] = (byte)ContainerPlace;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), AntiBotToken);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(12, 4), TargetId);
    }
}
