using System.Runtime.InteropServices;
using JXServer.Primitives.Characters;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.Adapter.LegacyJX1.Packets;

/// <summary>
/// 68-byte <c>s2c_synccurplayer</c> (<c>0x44</c>, <c>tagCURPLAYER_SYNC</c>) packet.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SyncCurPlayerS2CPacket
{
    public const int Size = 68;

    public byte ProtocolType;
    public uint Id;
    public byte Level;
    public ushort LifeMax;
    public ushort StaminaMax;
    public byte Sex;
    public byte Kind;
    public byte Series;
    public ushort ManaMax;
    public uint HeadImage;
    public ushort AttributePoint;
    public ushort Energy;
    public ushort Lucky;
    public ushort SkillPoint;
    public ushort Strength;
    public ushort Dexterity;
    public ushort Vitality;
    public long ExpRemainder;
    public int LeadExp;
    public byte TeamState;
    public byte CurFaction;
    public ushort FactionAddTimes;
    public ushort ExpHigh;
    public byte FirstFaction;
    public ushort WorldStat;
    public ushort SectStat;
    public int Money2;
    public int Money1;
    public uint RankFfId;

    public static SyncCurPlayerS2CPacket Create(PlayerZoneState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new SyncCurPlayerS2CPacket
        {
            ProtocolType = ProtocolOpcodes.s2c_synccurplayer,
            Id = state.RuntimeNpcId,
            Level = state.Level,
            LifeMax = state.MaxLife,
            StaminaMax = state.MaxStamina,
            Sex = (byte)state.Gender,
            Kind = (byte)EntityKind.Player,
            Series = (byte)state.Series,
            ManaMax = state.MaxMana,
            HeadImage = 0,
            AttributePoint = state.UnallocatedAttributePoints,
            Energy = state.Energy,
            Lucky = state.Lucky,
            SkillPoint = state.UnallocatedSkillPoints,
            Strength = state.Strength,
            Dexterity = state.Dexterity,
            Vitality = state.Vitality,
            ExpRemainder = state.Experience % CharacterConsts.ExpHighDivisor,
            LeadExp = state.LeadExperience,
            TeamState = 0,
            CurFaction = state.CurrentFactionId,
            FactionAddTimes = state.FactionJoinCount,
            ExpHigh = (ushort)(state.Experience / CharacterConsts.ExpHighDivisor),
            FirstFaction = state.FirstFactionId,
            WorldStat = 0,
            SectStat = 0,
            Money2 = state.RepositoryMoney,
            Money1 = state.InventoryMoney,
            RankFfId = 0
        };
    }
}
