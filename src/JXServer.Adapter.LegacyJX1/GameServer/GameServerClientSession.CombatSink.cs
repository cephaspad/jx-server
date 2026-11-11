using System.Buffers;
using JXServer.Adapter.LegacyJX1.Packets;
using JXServer.Primitives.Combat;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.Adapter.LegacyJX1.GameServer;

public sealed partial class GameServerClientSession : ICombatOutboundSink
{
    public void SendNormalVitalSync(PlayerZoneState playerState)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        SyncCurPlayerNormalS2CPacket normalSync = SyncCurPlayerNormalS2CPacket.Create(playerState);
        _frameProcessor.WriteEncryptedS2CStruct(writer, in normalSync);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendSkillCastBroadcast(
        uint casterNpcId,
        uint skillId,
        uint skillLevel,
        int targetMpsX,
        int targetMpsYOrNpcId,
        bool castDirectly = false)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        Span<byte> payload = stackalloc byte[SkillCastS2CPacket.PayloadSize];
        if (castDirectly)
        {
            var pkt = new CastSkillDirectlyS2CPacket(targetMpsX, skillId, targetMpsYOrNpcId, casterNpcId, skillLevel);
            pkt.WritePayload(payload);
        }
        else
        {
            var pkt = new SkillCastS2CPacket(targetMpsX, skillId, targetMpsYOrNpcId, casterNpcId, skillLevel);
            pkt.WritePayload(payload);
        }

        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendHurtBroadcast(uint npcId, int mpsX, int mpsY, int hurtFrames)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        BroadcastNetCommandS2CPacket hurt = BroadcastNetCommandS2CPacket.CreateHurt(npcId, mpsX, mpsY, hurtFrames);
        Span<byte> payload = stackalloc byte[BroadcastNetCommandS2CPacket.PayloadSize];
        hurt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPkNormalFlagSync(byte pkMode)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new PkSyncNormalFlagS2CPacket((PlayerPkMode)pkMode);
        Span<byte> payload = stackalloc byte[PkSyncNormalFlagS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendNpcDeathBroadcast(uint npcId)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new NpcDeathS2CPacket(npcId);
        Span<byte> payload = stackalloc byte[NpcDeathS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendNpcReviveBroadcast(uint npcId, byte reviveType = 0)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new NpcReviveS2CPacket(npcId, reviveType);
        Span<byte> payload = stackalloc byte[NpcReviveS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPlayerExpSync(int currentExp)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        var pkt = new PlayerExpS2CPacket(currentExp);
        Span<byte> payload = stackalloc byte[PlayerExpS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPlayerLevelUpSync(int newLevel, int unallocatedAttrPoints, int unallocatedSkillPoints, int currentExp)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var pkt = new PlayerLevelUpS2CPacket(newLevel, unallocatedAttrPoints, unallocatedSkillPoints, currentExp);
        Span<byte> payload = stackalloc byte[PlayerLevelUpS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPlayerAttributeSync(byte attributeKind, int baseValue, int currentValue, int unallocatedAttrPoints)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var pkt = new PlayerAttributeS2CPacket(
            (BaseAttributeKind)attributeKind,
            baseValue,
            currentValue,
            unallocatedAttrPoints);
        Span<byte> payload = stackalloc byte[PlayerAttributeS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }

    public void SendPlayerSkillLevelSync(int skillId, int skillLevel, int skillExp, short unallocatedSkillPoints)
    {
        var writer = new ArrayBufferWriter<byte>(32);
        var pkt = new PlayerSkillLevelS2CPacket(skillId, skillLevel, skillExp, unallocatedSkillPoints);
        Span<byte> payload = stackalloc byte[PlayerSkillLevelS2CPacket.PayloadSize];
        pkt.WritePayload(payload);
        _frameProcessor.WriteEncryptedS2CFrame(writer, payload);
        EnqueueOutboundWireBytes(writer.WrittenSpan.ToArray());
    }
}
