using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_npcrun</c> (<c>0x4C</c>, <c>13</c> bytes): player run command with <c>dwCheckValue</c> (`0x1D78`) (`FR-005`, `FR-006`).
/// </summary>
public readonly record struct NpcRunC2SPacket(
    int TargetMpsX,
    int TargetMpsY,
    uint CheckValue)
{
    public const int PayloadSize = 13;

    public static bool TryParse(ReadOnlySpan<byte> payload, out NpcRunC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_npcrun)
        {
            packet = default;
            return false;
        }

        packet = new NpcRunC2SPacket(
            TargetMpsX: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)),
            TargetMpsY: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(5, 4)),
            CheckValue: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(9, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_npcrun;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), TargetMpsX);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(5, 4), TargetMpsY);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(9, 4), CheckValue);
    }
}
