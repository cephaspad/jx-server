using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_playerlevelup</c> (<c>0x5B</c>, <c>17</c> bytes): synchronizes a player level-up, unallocated attribute/skill points, and current experience.
/// </summary>
public readonly record struct PlayerLevelUpS2CPacket(
    int NewLevel,
    int UnallocatedAttrPoints,
    int UnallocatedSkillPoints,
    int CurrentExp)
{
    public const int PayloadSize = 17;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerLevelUpS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_playerlevelup)
        {
            packet = default;
            return false;
        }

        packet = new PlayerLevelUpS2CPacket(
            NewLevel: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)),
            UnallocatedAttrPoints: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(5, 4)),
            UnallocatedSkillPoints: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(9, 4)),
            CurrentExp: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(13, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_playerlevelup;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), NewLevel);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(5, 4), UnallocatedAttrPoints);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(9, 4), UnallocatedSkillPoints);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(13, 4), CurrentExp);
    }
}
