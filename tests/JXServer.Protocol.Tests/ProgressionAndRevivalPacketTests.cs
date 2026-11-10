using JXServer.Adapter.LegacyJX1.Framing;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Combat;
using Xunit;

namespace JXServer.Protocol.Tests;

public sealed class ProgressionAndRevivalPacketTests
{
    [Fact]
    public void PlayerAddBaseAttributeAndApplySkillPointC2SPackets_ShouldRoundTripExactWireBytes()
    {
        var attrReq = new PlayerAddBaseAttributeC2SPacket(BaseAttributeKind.Vitality, 5);
        byte[] attrBuf = new byte[PlayerAddBaseAttributeC2SPacket.PayloadSize];
        attrReq.WritePayload(attrBuf);

        Assert.Equal(14, attrBuf.Length);
        Assert.Equal(ProtocolOpcodes.c2s_playeraddbaseattribute, attrBuf[0]);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(attrBuf));
        Assert.True(PlayerAddBaseAttributeC2SPacket.TryParse(attrBuf, out PlayerAddBaseAttributeC2SPacket parsedAttr));
        Assert.Equal(BaseAttributeKind.Vitality, parsedAttr.Attribute);
        Assert.Equal(5, parsedAttr.PointsToAdd);

        var skillReq = new PlayerApplyAddSkillPointC2SPacket(SkillId: 53, PointsToAdd: 2);
        byte[] skillBuf = new byte[PlayerApplyAddSkillPointC2SPacket.PayloadSize];
        skillReq.WritePayload(skillBuf);

        Assert.Equal(17, skillBuf.Length);
        Assert.Equal(ProtocolOpcodes.c2s_playeraddskillpoint, skillBuf[0]);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(skillBuf));
        Assert.True(PlayerApplyAddSkillPointC2SPacket.TryParse(skillBuf, out PlayerApplyAddSkillPointC2SPacket parsedSkill));
        Assert.Equal(53, parsedSkill.SkillId);
        Assert.Equal(2, parsedSkill.PointsToAdd);
    }

    [Fact]
    public void PlayerReviveC2SAndNpcDeathReviveS2CPackets_ShouldRoundTripExactWireBytes()
    {
        var reviveReq = new PlayerReviveC2SPacket(ReviveType: 0);
        byte[] reviveReqBuf = new byte[PlayerReviveC2SPacket.PayloadSize];
        reviveReq.WritePayload(reviveReqBuf);

        Assert.Equal(13, reviveReqBuf.Length);
        Assert.Equal(ProtocolOpcodes.c2s_npcrevive, reviveReqBuf[0]);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(reviveReqBuf));
        Assert.True(PlayerReviveC2SPacket.TryParse(reviveReqBuf, out PlayerReviveC2SPacket parsedReviveReq));
        Assert.Equal(0, parsedReviveReq.ReviveType);

        var deathS2C = new NpcDeathS2CPacket(NpcId: 10000u);
        byte[] deathBuf = new byte[NpcDeathS2CPacket.PayloadSize];
        deathS2C.WritePayload(deathBuf);

        Assert.Equal(5, deathBuf.Length);
        Assert.Equal(ProtocolOpcodes.s2c_npcdeath, deathBuf[0]);
        Assert.True(NpcDeathS2CPacket.TryParse(deathBuf, out NpcDeathS2CPacket parsedDeath));
        Assert.Equal(10000u, parsedDeath.NpcId);

        var reviveS2C = new NpcReviveS2CPacket(NpcId: 10000u, ReviveType: 0);
        byte[] reviveS2CBuf = new byte[NpcReviveS2CPacket.PayloadSize];
        reviveS2C.WritePayload(reviveS2CBuf);

        Assert.Equal(6, reviveS2CBuf.Length);
        Assert.Equal(ProtocolOpcodes.s2c_playerrevive, reviveS2CBuf[0]);
        Assert.True(NpcReviveS2CPacket.TryParse(reviveS2CBuf, out NpcReviveS2CPacket parsedReviveS2C));
        Assert.Equal(10000u, parsedReviveS2C.NpcId);
        Assert.Equal(0, parsedReviveS2C.ReviveType);
    }

    [Fact]
    public void PlayerExpLevelUpAttributeAndSkillLevelS2CPackets_ShouldRoundTripExactWireBytes()
    {
        var expS2C = new PlayerExpS2CPacket(CurrentExp: 250);
        byte[] expBuf = new byte[PlayerExpS2CPacket.PayloadSize];
        expS2C.WritePayload(expBuf);

        Assert.Equal(5, expBuf.Length);
        Assert.Equal(ProtocolOpcodes.s2c_playerexp, expBuf[0]);
        Assert.True(PlayerExpS2CPacket.TryParse(expBuf, out PlayerExpS2CPacket parsedExp));
        Assert.Equal(250, parsedExp.CurrentExp);

        var lvlS2C = new PlayerLevelUpS2CPacket(NewLevel: 2, UnallocatedAttrPoints: 5, UnallocatedSkillPoints: 1, CurrentExp: 50);
        byte[] lvlBuf = new byte[PlayerLevelUpS2CPacket.PayloadSize];
        lvlS2C.WritePayload(lvlBuf);

        Assert.Equal(17, lvlBuf.Length);
        Assert.Equal(ProtocolOpcodes.s2c_playerlevelup, lvlBuf[0]);
        Assert.True(PlayerLevelUpS2CPacket.TryParse(lvlBuf, out PlayerLevelUpS2CPacket parsedLvl));
        Assert.Equal(2, parsedLvl.NewLevel);
        Assert.Equal(5, parsedLvl.UnallocatedAttrPoints);
        Assert.Equal(1, parsedLvl.UnallocatedSkillPoints);
        Assert.Equal(50, parsedLvl.CurrentExp);

        var attrS2C = new PlayerAttributeS2CPacket(BaseAttributeKind.Vitality, BaseValue: 30, CurrentValue: 35, UnallocatedAttrPoints: 10);
        byte[] attrBuf = new byte[PlayerAttributeS2CPacket.PayloadSize];
        attrS2C.WritePayload(attrBuf);

        Assert.Equal(14, attrBuf.Length);
        Assert.Equal(ProtocolOpcodes.s2c_playerattributesync, attrBuf[0]);
        Assert.True(PlayerAttributeS2CPacket.TryParse(attrBuf, out PlayerAttributeS2CPacket parsedAttrS2C));
        Assert.Equal(BaseAttributeKind.Vitality, parsedAttrS2C.Attribute);
        Assert.Equal(30, parsedAttrS2C.BaseValue);
        Assert.Equal(35, parsedAttrS2C.CurrentValue);
        Assert.Equal(10, parsedAttrS2C.UnallocatedAttrPoints);

        var skillS2C = new PlayerSkillLevelS2CPacket(SkillId: 53, SkillLevel: 3, SkillExp: 0, UnallocatedSkillPoints: 2);
        byte[] skillBuf = new byte[PlayerSkillLevelS2CPacket.PayloadSize];
        skillS2C.WritePayload(skillBuf);

        Assert.Equal(15, skillBuf.Length);
        Assert.Equal(ProtocolOpcodes.s2c_playerskilllevel, skillBuf[0]);
        Assert.True(PlayerSkillLevelS2CPacket.TryParse(skillBuf, out PlayerSkillLevelS2CPacket parsedSkillS2C));
        Assert.Equal(53, parsedSkillS2C.SkillId);
        Assert.Equal(3, parsedSkillS2C.SkillLevel);
        Assert.Equal(2, parsedSkillS2C.UnallocatedSkillPoints);
    }
}
