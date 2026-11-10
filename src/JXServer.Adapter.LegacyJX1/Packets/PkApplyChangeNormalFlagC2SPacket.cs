using System;
using JXServer.Primitives.Combat;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_pkapplychangenormalflag</c> (<c>0x76</c>, <c>2</c> bytes): requests changing the player's normal PK mode (<c>0</c>=Peace, <c>1</c>=Fight, <c>2</c>=Slaughter).
/// </summary>
public readonly record struct PkApplyChangeNormalFlagC2SPacket(PlayerPkMode PkMode)
{
    public const int PayloadSize = 2;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PkApplyChangeNormalFlagC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_pkapplychangenormalflag)
        {
            packet = default;
            return false;
        }

        byte flag = payload[1];
        PlayerPkMode mode = flag <= (byte)PlayerPkMode.Slaughter ? (PlayerPkMode)flag : PlayerPkMode.Peace;
        packet = new PkApplyChangeNormalFlagC2SPacket(mode);
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_pkapplychangenormalflag;
        destination[1] = (byte)PkMode;
    }
}
