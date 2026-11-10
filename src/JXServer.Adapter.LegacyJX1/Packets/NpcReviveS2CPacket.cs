using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_npcrevive</c> (<c>0x89</c>, <c>6</c> bytes): broadcasts an entity's revival to its <c>3x3</c> region AOI.
/// </summary>
public readonly record struct NpcReviveS2CPacket(uint NpcId, byte ReviveType = 0)
{
    public const int PayloadSize = 6;

    public static bool TryParse(ReadOnlySpan<byte> payload, out NpcReviveS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_playerrevive)
        {
            packet = default;
            return false;
        }

        packet = new NpcReviveS2CPacket(
            NpcId: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4)),
            ReviveType: payload[5]);
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_playerrevive;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(1, 4), NpcId);
        destination[5] = ReviveType;
    }
}
