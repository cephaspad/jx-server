using System.Collections.Generic;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using MemoryPack;

namespace JXServer.GameAssets.Skills;

/// <summary>
/// Immutable skill template loaded from <c>JXData Standard v1</c> (<c>data/skills/*.skills.json</c> or <c>artifacts/skills.pack</c>).
/// </summary>
[MemoryPackable]
public sealed partial record SkillTemplate(
    int Id,
    string Code,
    string Name,
    SkillStyleKind Style,
    ElementSeries ElementSeries,
    bool IsOffensive,
    bool IsPhysical,
    bool IsMelee,
    bool IsAura,
    bool IsWeaponSkill,
    bool IsExpSkill,
    bool UseAttackRating,
    bool DoHurtAnimation,
    bool PeaceCanUse,
    int ReqLevel,
    int MaxLevel,
    int EquipLimit,
    int HorseLimit,
    int CastRangeMps,
    int WaitFrames,
    int MissileId,
    string MissileForm,
    int ChildSkillId,
    int ChildSkillLevel,
    int ChildSkillCount,
    SkillCostScalingTemplate CostScaling,
    IReadOnlyList<SkillEffectScalingTemplate> EffectScaling);
