namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Allocation-free singleton fallback when <see cref="ICombatOutboundSink"/> is unsubscribed (<c>FR-011</c>).
/// </summary>
public sealed class NoOpCombatOutboundSink : ICombatOutboundSink
{
    public static NoOpCombatOutboundSink Instance { get; } = new();

    private NoOpCombatOutboundSink()
    {
    }

    public void SendNormalVitalSync(PlayerZoneState playerState)
    {
    }

    public void SendSkillCastBroadcast(
        uint casterNpcId,
        uint skillId,
        uint skillLevel,
        int targetMpsX,
        int targetMpsYOrNpcId,
        bool castDirectly = false)
    {
    }

    public void SendHurtBroadcast(uint npcId, int mpsX, int mpsY, int hurtFrames)
    {
    }

    public void SendPkNormalFlagSync(byte pkMode)
    {
    }

    public void SendNpcDeathBroadcast(uint npcId)
    {
    }

    public void SendNpcReviveBroadcast(uint npcId, byte reviveType = 0)
    {
    }

    public void SendPlayerExpSync(int currentExp)
    {
    }

    public void SendPlayerLevelUpSync(
        int newLevel,
        int unallocatedAttrPoints,
        int unallocatedSkillPoints,
        int currentExp)
    {
    }

    public void SendPlayerAttributeSync(
        byte attributeKind,
        int baseValue,
        int currentValue,
        int unallocatedAttrPoints)
    {
    }

    public void SendPlayerSkillLevelSync(
        int skillId,
        int skillLevel,
        int skillExp,
        short unallocatedSkillPoints)
    {
    }
}
