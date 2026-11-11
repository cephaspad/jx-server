namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Outbound wire port for combat broadcasts, vital bars, PK flags, death/revival notifications,
/// and experience/level/attribute/skill progression sync (<c>FR-010</c>).
/// </summary>
public interface ICombatOutboundSink
{
    void SendNormalVitalSync(PlayerZoneState playerState) { }

    void SendSkillCastBroadcast(
        uint casterNpcId,
        uint skillId,
        uint skillLevel,
        int targetMpsX,
        int targetMpsYOrNpcId,
        bool castDirectly = false) { }

    void SendHurtBroadcast(uint npcId, int mpsX, int mpsY, int hurtFrames) { }

    void SendPkNormalFlagSync(byte pkMode) { }

    void SendNpcDeathBroadcast(uint npcId) { }

    void SendNpcReviveBroadcast(uint npcId, byte reviveType = 0) { }

    void SendPlayerExpSync(int currentExp) { }

    void SendPlayerLevelUpSync(
        int newLevel,
        int unallocatedAttrPoints,
        int unallocatedSkillPoints,
        int currentExp) { }

    void SendPlayerAttributeSync(
        byte attributeKind,
        int baseValue,
        int currentValue,
        int unallocatedAttrPoints) { }

    void SendPlayerSkillLevelSync(
        int skillId,
        int skillLevel,
        int skillExp,
        short unallocatedSkillPoints) { }
}
