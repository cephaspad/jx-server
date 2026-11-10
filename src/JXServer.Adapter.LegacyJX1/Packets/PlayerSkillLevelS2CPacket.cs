using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_playerskilllevel</c> (<c>0x5E</c>, <c>15</c> bytes): synchronizes an updated skill level and remaining unallocated skill points.
/// </summary>
public readonly record struct PlayerSkillLevelS2CPacket(
    int SkillId,
    int SkillLevel,
    int SkillExp,
    short UnallocatedSkillPoints)
{
    public const int PayloadSize = 15;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerSkillLevelS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_playerskilllevel)
        {
            packet = default;
            return false;
        }

        packet = new PlayerSkillLevelS2CPacket(
            SkillId: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)),
            SkillLevel: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(5, 4)),
            SkillExp: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(9, 4)),
            UnallocatedSkillPoints: BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(13, 2)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_playerskilllevel;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), SkillId);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(5, 4), SkillLevel);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(9, 4), SkillExp);
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(13, 2), UnallocatedSkillPoints);
    }
}
