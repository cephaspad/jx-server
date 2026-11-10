using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_chatsetchannel</c> (<c>0x60</c>, <c>5</c> bytes): updates the player's subscribed chat channel bitmask (`FR-011`).
/// </summary>
public readonly record struct ChatSetChannelC2SPacket(uint SubscribedChannelMask)
{
    public const int PayloadSize = 5;

    public static bool TryParse(ReadOnlySpan<byte> payload, out ChatSetChannelC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_chatsetchannel)
        {
            packet = default;
            return false;
        }

        uint mask = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(1, 4));
        packet = new ChatSetChannelC2SPacket(mask);
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_chatsetchannel;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(1, 4), SubscribedChannelMask);
    }
}
