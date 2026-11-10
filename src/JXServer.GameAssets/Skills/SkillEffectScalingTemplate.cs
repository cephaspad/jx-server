using MemoryPack;

namespace JXServer.GameAssets.Skills;

/// <summary>
/// Represents a single magic attribute effect scaling slot on a skill template.
/// </summary>
[MemoryPackable]
public sealed partial record SkillEffectScalingTemplate(
    int AttributeId,
    string AttributeCode,
    string SlotCategory,
    SkillParameterCurveTemplate? Param0 = null,
    SkillParameterCurveTemplate? Param1 = null,
    SkillParameterCurveTemplate? Param2 = null);
