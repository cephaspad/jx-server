using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_npcwalk</c> (<c>0x50</c>, <c>13</c> bytes, <c>&lt;BIii</c>): packs <c>NpcId</c> at <c>+1</c>,
/// <c>DestMpsY</c> at <c>+5</c>, and <c>DestMpsX</c> at <c>+9</c> (`KNpc::DoWalk` @ <c>0x08096f40</c>, `FR-006`).
/// </summary>
public readonly record struct NpcWalkS2CPacket(
    uint NpcId,
    int DestMpsX,
    int DestMpsY)
{
    public const int PayloadSize = 13;

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_npcwalk;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(1, 4), NpcId);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(5, 4), DestMpsY);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(9, 4), DestMpsX);
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out NpcWalkS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_npcwalk)
        {
            packet = default;
            return false;
        }

        uint npcId = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4));
        int destMpsY = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(5, 4));
        int destMpsX = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(9, 4));
        packet = new NpcWalkS2CPacket(npcId, destMpsX, destMpsY);
        return true;
    }
}
