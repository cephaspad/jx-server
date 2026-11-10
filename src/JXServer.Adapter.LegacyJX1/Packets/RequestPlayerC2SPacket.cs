using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_requestplayer</c> (<c>0x48</c>, <c>5</c> bytes): requests full <c>0x4C</c> appearance sync for a player entity (`FR-003`).
/// </summary>
public readonly record struct RequestPlayerC2SPacket(uint TargetNpcId)
{
    public const int PayloadSize = 5;

    public static bool TryParse(ReadOnlySpan<byte> payload, out RequestPlayerC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_requestplayer)
        {
            packet = default;
            return false;
        }

        uint targetId = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4));
        packet = new RequestPlayerC2SPacket(targetId);
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_requestplayer;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(1, 4), TargetNpcId);
    }
}
