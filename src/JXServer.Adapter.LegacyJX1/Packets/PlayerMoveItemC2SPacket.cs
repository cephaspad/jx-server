using System;
using JXServer.Primitives.Items;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// <c>c2s_playermoveitem</c> (<c>0x5B</c>, <c>7</c> bytes — <c>PLAYER_MOVE_ITEM_COMMAND</c>):
/// requests moving/swapping/equipping an item between inventory containers (`FR-012`).
/// Layout: <c>+1 DownY</c>, <c>+2 UpX</c>, <c>+3 DownPos</c>, <c>+4 DownX</c>, <c>+5 UpY</c>, <c>+6 UpPos</c>.
/// </summary>
public readonly record struct PlayerMoveItemC2SPacket(
    byte DownY,
    byte UpX,
    ItemContainerPlace DownPos,
    byte DownX,
    byte UpY,
    ItemContainerPlace UpPos)
{
    public const int PayloadSize = 7;

    public static bool TryParse(ReadOnlySpan<byte> payload, out PlayerMoveItemC2SPacket packet)
    {
        if (payload.Length != PayloadSize || payload[0] != ProtocolOpcodes.c2s_playermoveitem)
        {
            packet = default;
            return false;
        }

        packet = new PlayerMoveItemC2SPacket(
            DownY: payload[1],
            UpX: payload[2],
            DownPos: (ItemContainerPlace)payload[3],
            DownX: payload[4],
            UpY: payload[5],
            UpPos: (ItemContainerPlace)payload[6]);
        return true;
    }

    public void WritePayload(Span<byte> destination)
    {
        destination[0] = ProtocolOpcodes.c2s_playermoveitem;
        destination[1] = DownY;
        destination[2] = UpX;
        destination[3] = (byte)DownPos;
        destination[4] = DownX;
        destination[5] = UpY;
        destination[6] = (byte)UpPos;
    }
}
