using System;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Tracks active status ailments (Cold slow, Poison weighted DoT, Lightning stun, and Hurt recovery) on a player or NPC entity.
/// </summary>
public sealed class EntityStatusEffectState
{
    public int FreezeRemainingFrames { get; set; }

    public int PoisonDamagePerTick { get; set; }

    public int PoisonRemainingFrames { get; set; }

    public int PoisonIntervalFrames { get; set; }

    public int PoisonTicksSinceLastHit { get; set; }

    public uint LastPoisonAttackerNpcId { get; set; }

    public int StunRemainingFrames { get; set; }

    public int HurtRecoveryRemainingFrames { get; set; }

    public bool IsStunned => StunRemainingFrames > 0;

    public bool IsFrozen => FreezeRemainingFrames > 0;

    public bool IsPoisoned => PoisonRemainingFrames > 0 && PoisonDamagePerTick > 0;

    /// <summary>
    /// Applies or refreshes Cold freeze/slow duration.
    /// </summary>
    public void ApplyColdFreeze(int durationFrames)
    {
        if (durationFrames > FreezeRemainingFrames)
        {
            FreezeRemainingFrames = durationFrames;
        }
    }

    /// <summary>
    /// Applies or refreshes Lightning stun duration.
    /// </summary>
    public void ApplyLightStun(int durationFrames)
    {
        if (durationFrames > StunRemainingFrames)
        {
            StunRemainingFrames = durationFrames;
        }
    }

    /// <summary>
    /// Blends incoming Poison DoT with any active Poison DoT using the canonical weighted formula from <c>KNpc::ReceiveDamage</c>:
    /// <c>d1 = t1 * v1 / p1</c>, <c>d2 = t2 * v2 / p2</c>, <c>t_new = (d1*t1 + d2*t2)/(d1+d2)</c>, <c>v_new = (d1*v1 + d2*v2)/(d1+d2)</c>.
    /// </summary>
    public void BlendPoisonDamageOverTime(
        int incomingDamagePerTick,
        int incomingDurationFrames,
        int incomingIntervalFrames,
        uint attackerNpcId)
    {
        if (incomingDamagePerTick <= 0 || incomingDurationFrames <= 0)
        {
            return;
        }

        int p2 = Math.Max(1, incomingIntervalFrames);
        long t2 = incomingDurationFrames;
        long v2 = incomingDamagePerTick;

        if (!IsPoisoned)
        {
            PoisonDamagePerTick = incomingDamagePerTick;
            PoisonRemainingFrames = incomingDurationFrames;
            PoisonIntervalFrames = p2;
            PoisonTicksSinceLastHit = 0;
            LastPoisonAttackerNpcId = attackerNpcId;
            return;
        }

        int p1 = Math.Max(1, PoisonIntervalFrames);
        long t1 = PoisonRemainingFrames;
        long v1 = PoisonDamagePerTick;

        long d1 = Math.Max(1L, (t1 * v1) / p1);
        long d2 = Math.Max(1L, (t2 * v2) / p2);
        long sumD = d1 + d2;

        PoisonRemainingFrames = (int)Math.Max(1L, ((d1 * t1) + (d2 * t2)) / sumD);
        PoisonDamagePerTick = (int)Math.Max(1L, ((d1 * v1) + (d2 * v2)) / sumD);
        PoisonIntervalFrames = (int)Math.Max(1L, ((d1 * p1) + (d2 * p2)) / sumD);
        LastPoisonAttackerNpcId = attackerNpcId;
    }

    /// <summary>
    /// Clears all active status ailments (e.g., on entity death or revival).
    /// </summary>
    public void ClearAll()
    {
        FreezeRemainingFrames = 0;
        PoisonDamagePerTick = 0;
        PoisonRemainingFrames = 0;
        PoisonIntervalFrames = 0;
        PoisonTicksSinceLastHit = 0;
        LastPoisonAttackerNpcId = 0;
        StunRemainingFrames = 0;
        HurtRecoveryRemainingFrames = 0;
    }
}
