using MemoryPack;

namespace JXServer.GameAssets.Skills;

/// <summary>
/// Represents a single control point in a piecewise skill parameter scaling curve.
/// </summary>
[MemoryPackable]
public sealed partial record SkillCurveControlPoint(int Level, int Value);
