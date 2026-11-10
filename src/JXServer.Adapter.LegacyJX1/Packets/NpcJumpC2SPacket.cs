using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_npcjump</c> (<c>0x4E</c>, <c>13</c> bytes): player jump command (`FR-007`).
/// </summary>
public readonly record struct NpcJumpC2SPacket(
    int TargetMpsX,
    int TargetMpsY,
    uint Reserved = 0)
{
    public const int PayloadSize = 13;

    public static bool TryParse(ReadOnlySpan<byte> payload, out NpcJumpC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_npcjump)
        {
            packet = default;
            return false;
        }

        packet = new NpcJumpC2SPacket(
            TargetMpsX: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)),
            TargetMpsY: BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(5, 4)),
            Reserved: BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(9, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_npcjump;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), TargetMpsX);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(5, 4), TargetMpsY);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(9, 4), Reserved);
    }
}
