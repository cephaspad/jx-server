using System;
using System.Collections.Generic;
using MemoryPack;

namespace JXServer.GameAssets.Skills;

/// <summary>
/// Represents a mathematical scaling curve (<c>Constant</c>, <c>Linear</c>, <c>Piecewise</c>, <c>Conic</c>, <c>Sqrt</c>, or <c>Table</c>)
/// evaluated for a skill parameter at a given skill level (`1..63`).
/// </summary>
[MemoryPackable]
public sealed partial record SkillParameterCurveTemplate(
    string CurveKind,
    int BaseValue,
    int PerLevelValue = 0,
    IReadOnlyList<int>? ValuesByLevel = null,
    IReadOnlyList<SkillCurveControlPoint>? Points = null)
{
    public int Evaluate(int skillLevel)
    {
        int clampedLevel = Math.Clamp(skillLevel, 1, 64);
        if (ValuesByLevel is { Count: > 0 })
        {
            int idx = Math.Clamp(clampedLevel - 1, 0, ValuesByLevel.Count - 1);
            return ValuesByLevel[idx];
        }

        if (string.Equals(CurveKind, "Linear", StringComparison.OrdinalIgnoreCase))
        {
            return BaseValue + ((clampedLevel - 1) * PerLevelValue);
        }

        if (string.Equals(CurveKind, "Conic", StringComparison.OrdinalIgnoreCase))
        {
            return BaseValue + (clampedLevel * clampedLevel * PerLevelValue / 10);
        }

        if (string.Equals(CurveKind, "Sqrt", StringComparison.OrdinalIgnoreCase))
        {
            return BaseValue + (int)(Math.Sqrt(clampedLevel) * PerLevelValue);
        }

        if (string.Equals(CurveKind, "Piecewise", StringComparison.OrdinalIgnoreCase) &&
            Points is { Count: > 0 })
        {
            if (clampedLevel <= Points[0].Level)
            {
                return Points[0].Value;
            }

            for (int i = 0; i < Points.Count - 1; i++)
            {
                SkillCurveControlPoint p0 = Points[i];
                SkillCurveControlPoint p1 = Points[i + 1];
                if (clampedLevel >= p0.Level && clampedLevel <= p1.Level)
                {
                    int span = Math.Max(1, p1.Level - p0.Level);
                    return p0.Value + ((clampedLevel - p0.Level) * (p1.Value - p0.Value) / span);
                }
            }

            return Points[^1].Value;
        }

        return BaseValue;
    }
}
