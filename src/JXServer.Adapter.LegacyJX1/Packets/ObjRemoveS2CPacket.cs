using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_objremove</c> (<c>0x67</c>, <c>6</c> bytes — <c>OBJ_REMOVE_SYNC</c>):
/// removes a ground object (<c>Reason = 0</c> expired/despawned, <c>Reason = 1</c> picked up by a player) (`FR-011`).
/// </summary>
public readonly record struct ObjRemoveS2CPacket(int ObjId, byte Reason)
{
    public const int PayloadSize = 6;

    public static bool TryParse(ReadOnlySpan<byte> payload, out ObjRemoveS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_objremove)
        {
            packet = default;
            return false;
        }

        packet = new ObjRemoveS2CPacket(
            ObjId: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)),
            Reason: payload[5]);
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_objremove;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), ObjId);
        destination[5] = Reason;
    }
}
