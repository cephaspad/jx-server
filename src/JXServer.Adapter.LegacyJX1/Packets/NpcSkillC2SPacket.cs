using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_npcskill</c> (<c>0x4D</c>, <c>17</c> bytes): player skill cast command with <c>CheckValue</c> (<c>0x3AB9</c>).
/// </summary>
public readonly record struct NpcSkillC2SPacket(
    int TargetMpsYOrNpcId,
    int SkillId,
    int TargetMpsX,
    uint CheckValue)
{
    public const int PayloadSize = 17;

    public static bool TryParse(ReadOnlySpan<byte> payload, out NpcSkillC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_npcskill)
        {
            packet = default;
            return false;
        }

        packet = new NpcSkillC2SPacket(
            TargetMpsYOrNpcId: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)),
            SkillId: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(5, 4)),
            TargetMpsX: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(9, 4)),
            CheckValue: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(13, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_npcskill;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), TargetMpsYOrNpcId);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(5, 4), SkillId);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(9, 4), TargetMpsX);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(13, 4), CheckValue);
    }
}
