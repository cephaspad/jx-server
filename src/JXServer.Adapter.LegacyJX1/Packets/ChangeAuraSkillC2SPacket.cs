using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_changeauraskill</c> (<c>0x6F</c>, <c>5</c> bytes): selects the active right-hand aura skill.
/// </summary>
public readonly record struct ChangeAuraSkillC2SPacket(int AuraSkillId)
{
    public const int PayloadSize = 5;

    public static bool TryParse(ReadOnlySpan<byte> payload, out ChangeAuraSkillC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_changeauraskill)
        {
            packet = default;
            return false;
        }

        packet = new ChangeAuraSkillC2SPacket(BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_changeauraskill;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), AuraSkillId);
    }
}
