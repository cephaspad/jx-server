using System.Runtime.InteropServices;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 13-byte <c>s2c_syncnpcminplayer</c> (<c>0x4E</c>, <c>tagNPC_PLAYER_TYPE_NORMAL_SYNC</c>) packet containing self <c>MpsY</c> and <c>MpsX</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SyncNpcMinPlayerS2CPacket
{
    public const int Size = 13;

    public byte ProtocolType;
    public ushort OffY;
    public ushort OffX;
    public int MapMpsY;
    public int MapMpsX;

    public static SyncNpcMinPlayerS2CPacket Create(PlayerZoneState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var pos = new WorldPosition(state.MapId, state.CurrentMpsX, state.CurrentMpsY);
        return new SyncNpcMinPlayerS2CPacket
        {
            ProtocolType = ProtocolOpcodes.s2c_syncnpcminplayer,
            OffY = pos.OffY,
            OffX = pos.OffX,
            MapMpsY = state.CurrentMpsY,
            MapMpsX = state.CurrentMpsX
        };
    }
}
