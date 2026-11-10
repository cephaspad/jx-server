using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_npcjump</c> (<c>0x54</c>, <c>13</c> bytes, <c>&lt;BiiI</c>): packs <c>DestMpsY</c> at <c>+1</c>,
/// <c>DestMpsX</c> at <c>+5</c>, and <c>NpcId</c> at <c>+9</c> (`FR-007`).
/// </summary>
public readonly record struct NpcJumpS2CPacket(
    uint NpcId,
    int DestMpsX,
    int DestMpsY)
{
    public const int PayloadSize = 13;

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_npcjump;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), DestMpsY);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(5, 4), DestMpsX);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(9, 4), NpcId);
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out NpcJumpS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_npcjump)
        {
            packet = default;
            return false;
        }

        int destMpsY = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4));
        int destMpsX = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(5, 4));
        uint npcId = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(9, 4));
        packet = new NpcJumpS2CPacket(npcId, destMpsX, destMpsY);
        return true;
    }
}
