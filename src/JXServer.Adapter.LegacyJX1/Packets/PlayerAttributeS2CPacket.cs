using System;
using System.Buffers.Binary;
using JXServer.Primitives.Combat;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_playerattribute</c> (<c>0x5D</c>, <c>14</c> bytes): synchronizes updated base attribute values and remaining unallocated attribute points.
/// </summary>
public readonly record struct PlayerAttributeS2CPacket(
    BaseAttributeKind Attribute,
    int BaseValue,
    int CurrentValue,
    int UnallocatedAttrPoints)
{
    public const int PayloadSize = 14;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerAttributeS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_playerattributesync)
        {
            packet = default;
            return false;
        }

        byte attrByte = payload[1];
        BaseAttributeKind attr = attrByte <= (byte)BaseAttributeKind.Energy
            ? (BaseAttributeKind)attrByte
            : BaseAttributeKind.Strength;

        packet = new PlayerAttributeS2CPacket(
            Attribute: attr,
            BaseValue: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(2, 4)),
            CurrentValue: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(6, 4)),
            UnallocatedAttrPoints: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(10, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_playerattributesync;
        destination[1] = (byte)Attribute;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(2, 4), BaseValue);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(6, 4), CurrentValue);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(10, 4), UnallocatedAttrPoints);
    }
}
