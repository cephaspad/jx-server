using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_objmouseclick</c> (<c>0x72</c>, <c>9</c> bytes): notifies the server that the player clicked a ground object.
/// </summary>
public readonly record struct ObjMouseClickC2SPacket(uint RegionId, int ObjId)
{
    public const int PayloadSize = 9;

    public static bool TryParse(ReadOnlySpan<byte> payload, out ObjMouseClickC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_objmouseclick)
        {
            packet = default;
            return false;
        }

        packet = new ObjMouseClickC2SPacket(
            RegionId: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4)),
            ObjId: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(5, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_objmouseclick;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(1, 4), RegionId);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(5, 4), ObjId);
    }
}
