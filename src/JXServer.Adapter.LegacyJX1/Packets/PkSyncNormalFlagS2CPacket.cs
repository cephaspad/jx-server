using System;
using JXServer.Primitives.Combat;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_pksyncnormalflag</c> (<c>0x90</c>, <c>2</c> bytes): synchronizes the player's active normal PK mode.
/// </summary>
public readonly record struct PkSyncNormalFlagS2CPacket(PlayerPkMode PkMode)
{
    public const int PayloadSize = 2;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PkSyncNormalFlagS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_pksyncnormalflag)
        {
            packet = default;
            return false;
        }

        byte flag = payload[1];
        PlayerPkMode mode = flag <= (byte)PlayerPkMode.Slaughter ? (PlayerPkMode)flag : PlayerPkMode.Peace;
        packet = new PkSyncNormalFlagS2CPacket(mode);
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_pksyncnormalflag;
        destination[1] = (byte)PkMode;
    }
}
