using System;
using System.Buffers.Binary;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_playerexp</c> (<c>0x5C</c>, <c>5</c> bytes): synchronizes the player's current experience total.
/// </summary>
public readonly record struct PlayerExpS2CPacket(int CurrentExp)
{
    public const int PayloadSize = 5;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerExpS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_playerexp)
        {
            packet = default;
            return false;
        }

        packet = new PlayerExpS2CPacket(BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1, 4)));
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_playerexp;
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(1, 4), CurrentExp);
    }
}
