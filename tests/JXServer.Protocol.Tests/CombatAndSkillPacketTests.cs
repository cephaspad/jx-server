using JXServer.Adapter.LegacyJX1.Framing;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Combat;
using JXServer.Primitives.WorldZones;
using Xunit;

namespace JXServer.Protocol.Tests;

public sealed class CombatAndSkillPacketTests
{
    [Fact]
    public void NpcSkillC2SPacket_ShouldRoundTrip17BytesAndVerifySkillCheckKey()
    {
        uint token1 = 0x12345678u;
        uint token2 = 0x9ABCDEF0u;
        uint checkValue = MovementAntiCheatConsts.CreateCheckValue(MovementAntiCheatConsts.SkillCheckKey, token1, token2, multiplier: 3u);

        var pkt = new NpcSkillC2SPacket(
            TargetMpsYOrNpcId: 101696,
            SkillId: 53,
            TargetMpsX: 52032,
            CheckValue: checkValue);

        byte[] buf = new byte[NpcSkillC2SPacket.PayloadSize];
        pkt.WritePayload(buf);

        Assert.Equal(17, buf.Length);
        Assert.Equal(ProtocolOpcodes.c2s_npcskill, buf[0]);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(buf));
        Assert.True(NpcSkillC2SPacket.TryParse(buf, out NpcSkillC2SPacket parsed));
        Assert.Equal(101696, parsed.TargetMpsYOrNpcId);
        Assert.Equal(53, parsed.SkillId);
        Assert.Equal(52032, parsed.TargetMpsX);
        Assert.True(MovementAntiCheatConsts.IsValidCheckValue(parsed.CheckValue, token1, token2, MovementAntiCheatConsts.SkillCheckKey));
    }

    [Fact]
    public void AutoAttackNpcC2SPacket_ShouldRoundTrip26BytesWithWrappedSkillPayload()
    {
        uint token1 = 0xAABBCCDDu;
        uint token2 = 0x11223344u;
        uint checkVal = MovementAntiCheatConsts.CreateCheckValue(MovementAntiCheatConsts.SkillCheckKey, token1, token2);

        byte[] innerSkill = new byte[NpcSkillC2SPacket.PayloadSize];
        new NpcSkillC2SPacket(TargetMpsYOrNpcId: 1, SkillId: 53, TargetMpsX: -1, CheckValue: checkVal).WritePayload(innerSkill);

        var autoPkt = new AutoAttackNpcC2SPacket(AutoAttackNpcC2SPacket.SubCommandWrappedAction, innerSkill);
        byte[] buf = new byte[AutoAttackNpcC2SPacket.PayloadSize];
        autoPkt.WritePayload(buf);

        Assert.Equal(26, buf.Length);
        Assert.Equal(ProtocolOpcodes.c2s_autoattacknpc, buf[0]);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(buf));
        Assert.True(AutoAttackNpcC2SPacket.TryParse(buf, out AutoAttackNpcC2SPacket parsed));
        Assert.Equal(AutoAttackNpcC2SPacket.SubCommandWrappedAction, parsed.SubCommand);
        Assert.True(NpcSkillC2SPacket.TryParse(parsed.InnerPayload.Span.Slice(0, 17), out NpcSkillC2SPacket unpackedSkill));
        Assert.Equal(53, unpackedSkill.SkillId);
        Assert.Equal(-1, unpackedSkill.TargetMpsX);
        Assert.Equal(1, unpackedSkill.TargetMpsYOrNpcId);
    }

    [Fact]
    public void ChangeAuraSkillAndPkPackets_ShouldRoundTripExactWireBytes()
    {
        byte[] auraBuf = new byte[ChangeAuraSkillC2SPacket.PayloadSize];
        new ChangeAuraSkillC2SPacket(104).WritePayload(auraBuf);
        Assert.Equal(5, auraBuf.Length);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(auraBuf));
        Assert.True(ChangeAuraSkillC2SPacket.TryParse(auraBuf, out ChangeAuraSkillC2SPacket auraParsed));
        Assert.Equal(104, auraParsed.AuraSkillId);

        byte[] pkC2S = new byte[PkApplyChangeNormalFlagC2SPacket.PayloadSize];
        new PkApplyChangeNormalFlagC2SPacket(PlayerPkMode.Slaughter).WritePayload(pkC2S);
        Assert.Equal(2, pkC2S.Length);
        Assert.True(ProtocolSizeTable.IsValidGameServerC2SPayload(pkC2S));
        Assert.True(PkApplyChangeNormalFlagC2SPacket.TryParse(pkC2S, out PkApplyChangeNormalFlagC2SPacket pkReq));
        Assert.Equal(PlayerPkMode.Slaughter, pkReq.PkMode);

        byte[] pkS2C = new byte[PkSyncNormalFlagS2CPacket.PayloadSize];
        new PkSyncNormalFlagS2CPacket(PlayerPkMode.Fight).WritePayload(pkS2C);
        Assert.Equal(2, pkS2C.Length);
        Assert.True(PkSyncNormalFlagS2CPacket.TryParse(pkS2C, out PkSyncNormalFlagS2CPacket pkSync));
        Assert.Equal(PlayerPkMode.Fight, pkSync.PkMode);
    }

    [Fact]
    public void SkillCastDirectCastAndHurtS2CPackets_ShouldRoundTripExactWireBytes()
    {
        byte[] castBuf = new byte[SkillCastS2CPacket.PayloadSize];
        new SkillCastS2CPacket(52032, 100u, 101696, 10000u, 3u).WritePayload(castBuf);
        Assert.Equal(25, castBuf.Length);
        Assert.True(SkillCastS2CPacket.TryParse(castBuf, out SkillCastS2CPacket castParsed));
        Assert.Equal(52032, castParsed.TargetMpsX);
        Assert.Equal(100u, castParsed.SkillId);
        Assert.Equal(101696, castParsed.TargetMpsYOrNpcId);
        Assert.Equal(10000u, castParsed.CasterNpcId);
        Assert.Equal(3u, castParsed.SkillLevel);

        byte[] directBuf = new byte[CastSkillDirectlyS2CPacket.PayloadSize];
        new CastSkillDirectlyS2CPacket(-1, 53u, 5, 10001u, 1u).WritePayload(directBuf);
        Assert.Equal(25, directBuf.Length);
        Assert.True(CastSkillDirectlyS2CPacket.TryParse(directBuf, out CastSkillDirectlyS2CPacket directParsed));
        Assert.Equal(-1, directParsed.TargetMpsX);
        Assert.Equal(53u, directParsed.SkillId);
        Assert.Equal(5, directParsed.TargetMpsYOrNpcId);

        byte[] hurtBuf = new byte[BroadcastNetCommandS2CPacket.PayloadSize];
        BroadcastNetCommandS2CPacket.CreateHurt(7u, 52000, 101600, 6).WritePayload(hurtBuf);
        Assert.Equal(22, hurtBuf.Length);
        Assert.True(BroadcastNetCommandS2CPacket.TryParse(hurtBuf, out BroadcastNetCommandS2CPacket hurtParsed));
        Assert.Equal(7u, hurtParsed.NpcId);
        Assert.Equal(CombatConsts.BroadcastNetCmdHurt, hurtParsed.CmdKind);
        Assert.Equal(52000, hurtParsed.Param1MpsX);
        Assert.Equal(101600, hurtParsed.Param2MpsY);
        Assert.Equal(6, hurtParsed.Param3HurtFrames);
    }
}
