using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_playerapplyaddskillpoint</c> (<c>0x58</c>, <c>17</c> bytes): allocates unspent skill points to a learned martial arts skill.
/// </summary>
public readonly record struct PlayerApplyAddSkillPointC2SPacket(
    int SkillId,
    int PointsToAdd,
    uint Reserved1 = 0,
    uint Reserved2 = 0)
{
    public const int PayloadSize = 17;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerApplyAddSkillPointC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_playeraddskillpoint)
        {
            packet = default;
            return false;
        }

        packet = new PlayerApplyAddSkillPointC2SPacket(
            SkillId: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)),
            PointsToAdd: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(5, 4)),
            Reserved1: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(9, 4)),
            Reserved2: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(13, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_playeraddskillpoint;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), SkillId);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(5, 4), PointsToAdd);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(9, 4), Reserved1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(13, 4), Reserved2);
    }
}
