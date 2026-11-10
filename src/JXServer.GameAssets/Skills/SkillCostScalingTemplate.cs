using System;
using System.Collections.Generic;
using MemoryPack;

namespace JXServer.GameAssets.Skills;

/// <summary>
/// Represents the resource cost scaling curve (<c>mana</c>, <c>stamina</c>, or <c>life</c>) for a skill across levels.
/// </summary>
[MemoryPackable]
public sealed partial record SkillCostScalingTemplate(
    string CostType,
    int BaseMana,
    int ManaPerLevel = 0,
    int BaseStamina = 0,
    int BaseLife = 0,
    IReadOnlyList<int>? ValuesByLevel = null)
{
    public int EvaluateCost(int skillLevel)
    {
        int clampedLevel = Math.Clamp(skillLevel, 1, 63);
        if (ValuesByLevel is { Count: > 0 })
        {
            int idx = Math.Clamp(clampedLevel - 1, 0, ValuesByLevel.Count - 1);
            return Math.Max(0, ValuesByLevel[idx]);
        }

        if (string.Equals(CostType, "stamina", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(0, BaseStamina);
        }

        if (string.Equals(CostType, "life", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(0, BaseLife);
        }

        return Math.Max(0, BaseMana + ((clampedLevel - 1) * ManaPerLevel));
    }
}
