using System.Runtime.InteropServices;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 14-byte <c>s2c_synccurplayernormal</c> (<c>0x46</c>, <c>tagCURPLAYER_NORMAL_SYNC</c>) packet containing live Stamina, Mana, and Life.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SyncCurPlayerNormalS2CPacket
{
    public const int Size = 14;

    public byte ProtocolType;
    public ushort Stamina;
    public int Mana;
    public int Life;
    public ushort Angry;
    public byte TeamData;

    public static SyncCurPlayerNormalS2CPacket Create(PlayerZoneState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new SyncCurPlayerNormalS2CPacket
        {
            ProtocolType = ProtocolOpcodes.s2c_synccurplayernormal,
            Stamina = state.CurrentStamina,
            Mana = state.CurrentMana,
            Life = state.CurrentLife,
            Angry = 0,
            TeamData = 0
        };
    }
}
