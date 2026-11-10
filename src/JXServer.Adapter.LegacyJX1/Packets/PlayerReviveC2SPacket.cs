using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_playerrevive</c> (<c>0x74</c>, <c>13</c> bytes): requests reviving a defeated player at their saved revival point (<c>ReviveType = 0</c> Return to Town).
/// </summary>
public readonly record struct PlayerReviveC2SPacket(
    int ReviveType,
    uint Reserved1 = 0,
    uint Reserved2 = 0)
{
    public const int PayloadSize = 13;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerReviveC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_npcrevive)
        {
            packet = default;
            return false;
        }

        packet = new PlayerReviveC2SPacket(
            ReviveType: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)),
            Reserved1: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(5, 4)),
            Reserved2: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(9, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_npcrevive;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), ReviveType);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(5, 4), Reserved1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(9, 4), Reserved2);
    }
}
