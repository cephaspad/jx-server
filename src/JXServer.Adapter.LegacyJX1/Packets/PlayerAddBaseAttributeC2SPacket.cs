using System;
using System.Buffers.Binary;
using JXServer.Primitives.Combat;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_playeraddbaseattribute</c> (<c>0x57</c>, <c>14</c> bytes): allocates unspent base attribute points (<c>0</c>=Strength, <c>1</c>=Dexterity, <c>2</c>=Vitality, <c>3</c>=Energy).
/// </summary>
public readonly record struct PlayerAddBaseAttributeC2SPacket(
    BaseAttributeKind Attribute,
    int PointsToAdd,
    uint Reserved1 = 0,
    uint Reserved2 = 0)
{
    public const int PayloadSize = 14;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerAddBaseAttributeC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_playeraddbaseattribute)
        {
            packet = default;
            return false;
        }

        byte attrByte = payload[1];
        BaseAttributeKind attr = attrByte <= (byte)BaseAttributeKind.Energy
            ? (BaseAttributeKind)attrByte
            : BaseAttributeKind.Strength;

        packet = new PlayerAddBaseAttributeC2SPacket(
            Attribute: attr,
            PointsToAdd: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(2, 4)),
            Reserved1: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(6, 4)),
            Reserved2: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(10, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_playeraddbaseattribute;
        destination[1] = (byte)Attribute;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(2, 4), PointsToAdd);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(6, 4), Reserved1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(10, 4), Reserved2);
    }
}
