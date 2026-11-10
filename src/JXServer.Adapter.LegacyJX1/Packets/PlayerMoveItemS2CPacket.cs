using System;
using JXServer.Primitives.Items;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>s2c_playermoveitem</c> (<c>0x61</c>, <c>7</c> bytes — <c>PLAYER_MOVE_ITEM_SYNC</c>):
/// confirms an inventory item move/swap/equip operation (`FR-012`).
/// Layout: <c>+1 DownY</c>, <c>+2 DownPos</c>, <c>+3 DownX</c>, <c>+4 UpY</c>, <c>+5 UpX</c>, <c>+6 UpPos</c>.
/// </summary>
public readonly record struct PlayerMoveItemS2CPacket(
    byte DownY,
    ItemContainerPlace DownPos,
    byte DownX,
    byte UpY,
    byte UpX,
    ItemContainerPlace UpPos)
{
    public const int PayloadSize = 7;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerMoveItemS2CPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.s2c_playermoveitem)
        {
            packet = default;
            return false;
        }

        packet = new PlayerMoveItemS2CPacket(
            DownY: payload[1],
            DownPos: (ItemContainerPlace)payload[2],
            DownX: payload[3],
            UpY: payload[4],
            UpX: payload[5],
            UpPos: (ItemContainerPlace)payload[6]);
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.s2c_playermoveitem;
        destination[1] = DownY;
        destination[2] = (byte)DownPos;
        destination[3] = DownX;
        destination[4] = UpY;
        destination[5] = UpX;
        destination[6] = (byte)UpPos;
    }
}
