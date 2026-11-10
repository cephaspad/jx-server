using System;
using System.Buffers.Binary;
using JXServer.Primitives.Items;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_playerpickupitem</c> (<c>0x5A</c>, <c>12</c> bytes — <c>PLAYER_PICKUP_ITEM_COMMAND</c>):
/// requests picking up a ground object (<c>ObjId</c>) into a target inventory container coordinate (`FR-011`, `FR-012`).
/// </summary>
public readonly record struct PlayerPickupItemC2SPacket(
    byte TargetGridY,
    byte TargetGridX,
    ItemContainerPlace TargetPlace,
    int ObjId,
    uint Reserved = 0)
{
    public const int PayloadSize = 12;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerPickupItemC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_playerpickupitem)
        {
            packet = default;
            return false;
        }

        packet = new PlayerPickupItemC2SPacket(
            TargetGridY: payload[1],
            TargetGridX: payload[2],
            TargetPlace: (ItemContainerPlace)payload[3],
            ObjId: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(4, 4)),
            Reserved: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(8, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_playerpickupitem;
        destination[1] = TargetGridY;
        destination[2] = TargetGridX;
        destination[3] = (byte)TargetPlace;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(4, 4), ObjId);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(8, 4), Reserved);
    }
}
