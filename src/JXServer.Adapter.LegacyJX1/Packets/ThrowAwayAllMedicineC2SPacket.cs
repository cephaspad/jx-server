using System;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_ThrowAwayAllMedicine</c> (<c>0x83</c>, <c>4</c> bytes): drops all medicine items from the player's inventory onto the ground (`FR-012`).
/// </summary>
public readonly record struct ThrowAwayAllMedicineC2SPacket(byte Param0 = 0, byte Param1 = 0, byte Param2 = 0)
{
    public const int PayloadSize = 4;

    public static bool TryParse(ReadOnlySpan<byte> payload, out ThrowAwayAllMedicineC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_ThrowAwayAllMedicine)
        {
            packet = default;
            return false;
        }

        packet = new ThrowAwayAllMedicineC2SPacket(payload[1], payload[2], payload[3]);
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_ThrowAwayAllMedicine;
        destination[1] = Param0;
        destination[2] = Param1;
        destination[3] = Param2;
    }
}
