namespace JXServer.Primitives.Combat;

/// <summary>
/// Martial arts skill execution style (<c>SKILL_STYLE</c> in <c>SkillDef.h</c> / <c>skill-catalog.schema.json</c>).
/// </summary>
public enum SkillStyleKind : byte
{
    Melee = 0,
    Projectile = 1,
    SelfBuff = 2,
    TargetBuff = 3,
    Aura = 4,
    Passive = 5,
    SummonNpc = 6,
    Jump = 7,
    JumpAndAttack = 8,
    RunAndAttack = 9,
    MultiAttack = 10,
    CorpseExplode = 11,
    StrideObstacle = 12,
    Steal = 13,
}
