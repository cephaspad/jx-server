using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_npcdeath</c> (<c>0x57</c>, <c>5</c> bytes): broadcasts an entity's death to its <c>3x3</c> region AOI.
/// </summary>
public readonly record struct NpcDeathS2CPacket(uint NpcId)
{
    public const int PayloadSize = 5;

    public static bool TryParse(ReadOnlySpan<byte> payload, out NpcDeathS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_npcdeath)
        {
            packet = default;
            return false;
        }

        packet = new NpcDeathS2CPacket(BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_npcdeath;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(1, 4), NpcId);
    }
}
