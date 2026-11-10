using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_npcrun</c> (<c>0x51</c>, <c>13</c> bytes, <c>&lt;BiIi</c>): packs <c>DestMpsY</c> at <c>+1</c>,
/// <c>NpcId</c> at <c>+5</c>, and <c>DestMpsX</c> at <c>+9</c> (`KNpc::DoRun` @ <c>0x08097060</c>, `FR-006`).
/// </summary>
public readonly record struct NpcRunS2CPacket(
    uint NpcId,
    int DestMpsX,
    int DestMpsY)
{
    public const int PayloadSize = 13;

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_npcrun;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), DestMpsY);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(5, 4), NpcId);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(9, 4), DestMpsX);
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out NpcRunS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_npcrun)
        {
            packet = default;
            return false;
        }

        int destMpsY = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4));
        uint npcId = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(5, 4));
        int destMpsX = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(9, 4));
        packet = new NpcRunS2CPacket(npcId, destMpsX, destMpsY);
        return true;
    }
}
