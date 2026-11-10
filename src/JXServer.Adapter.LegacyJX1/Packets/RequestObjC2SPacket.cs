using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_requestobj</c> (<c>0x4A</c>, <c>5</c> bytes): requests synchronization of a ground object (<c>ObjId</c>).
/// </summary>
public readonly record struct RequestObjC2SPacket(int ObjId)
{
    public const int PayloadSize = 5;

    public static bool TryParse(ReadOnlySpan<byte> payload, out RequestObjC2SPacket packet)
    {
        if (payload.Length < PayloadSize || payload[0] != ProtocolOpcodes.c2s_requestobj)
        {
            packet = default;
            return false;
        }

        packet = new RequestObjC2SPacket(BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_requestobj;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), ObjId);
    }
}
