using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_npcsit</c> (<c>0x53</c>, <c>5</c> bytes, <c>&lt;BI</c>): notifies <c>3x3</c> AOI observers that <c>NpcId</c> sat down (`FR-007`).
/// </summary>
public readonly record struct NpcSitS2CPacket(uint NpcId)
{
    public const int PayloadSize = 5;

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_npcsit;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(1, 4), NpcId);
    }

    public static bool TryParse(ReadOnlySpan<byte> payload, out NpcSitS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_npcsit)
        {
            packet = default;
            return false;
        }

        uint npcId = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4));
        packet = new NpcSitS2CPacket(npcId);
        return true;
    }
}
