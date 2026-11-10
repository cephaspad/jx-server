using System;
using MemoryPack;

namespace JXServer.GameAssets.NpcTemplates;

/// <summary>
/// Level-based combat stat scaling rules (<c>script/npclevelscript/*.lua</c>) for an NPC/Monster template.
/// </summary>
[MemoryPackable]
public sealed partial record NpcCombatScalingTemplate(
    int LifePerLevel = 45,
    int ExpPerLevel = 35,
    int AttackRatingBase = 50,
    int AttackRatingPerLevel = 12,
    int DefenseBase = 25,
    int DefensePerLevel = 8,
    int MinDamageBase = 8,
    int MinDamagePerLevel = 3,
    int MaxDamageBase = 14,
    int MaxDamagePerLevel = 5,
    int PhysicsResist = 0,
    int ColdResist = 0,
    int FireResist = 0,
    int LightResist = 0,
    int PoisonResist = 0)
{
    public int EvaluateMaxLife(int baseLifeMax, int level) =>
        Math.Max(100, baseLifeMax + (Math.Max(1, level) - 1) * LifePerLevel);

    public int EvaluateExpReward(int baseExp, int level) =>
        Math.Max(10, baseExp + (Math.Max(1, level) - 1) * ExpPerLevel);

    public int EvaluateAttackRating(int level) =>
        Math.Max(10, AttackRatingBase + (Math.Max(1, level) - 1) * AttackRatingPerLevel);

    public int EvaluateDefense(int level) =>
        Math.Max(0, DefenseBase + (Math.Max(1, level) - 1) * DefensePerLevel);

    public (int MinDamage, int MaxDamage) EvaluatePhysicsDamage(int level)
    {
        int lvl = Math.Max(1, level) - 1;
        int min = Math.Max(1, MinDamageBase + (lvl * MinDamagePerLevel));
        int max = Math.Max(min, MaxDamageBase + (lvl * MaxDamagePerLevel));
        return (min, max);
    }
}
