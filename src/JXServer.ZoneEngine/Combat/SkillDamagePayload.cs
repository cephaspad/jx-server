using JXServer.Primitives.Characters;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Evaluated 6-channel damage and status-ailment payload for a skill cast or projectile (<c>KMagicAttrib m_DamageAttribs</c>).
/// </summary>
public readonly record struct SkillDamagePayload(
    int SkillId,
    int SkillLevel,
    ElementSeries ElementSeries,
    bool IsPhysical,
    bool IsMelee,
    bool UseAttackRating,
    bool DoHurtAnimation,
    int AttackRatingScore,
    int PhysicsMinDamage,
    int PhysicsMaxDamage,
    int ColdMinDamage,
    int ColdMaxDamage,
    int ColdFreezeDurationFrames,
    int FireMinDamage,
    int FireMaxDamage,
    int LightMinDamage,
    int LightMaxDamage,
    int LightStunDurationFrames,
    int PoisonDamagePerTick,
    int PoisonDurationFrames,
    int PoisonIntervalFrames,
    int MagicMinDamage,
    int MagicMaxDamage);
