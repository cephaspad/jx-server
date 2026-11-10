using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Logging;
using JXServer.GameAssets.Snapshots;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.Skills;

/// <summary>
/// Singleton read-only catalog of skill templates loaded from <c>JXData Standard v1</c> (`FR-001`).
/// </summary>
public sealed class SkillTemplateCatalog : ISkillTemplateCatalog
{
    private readonly FrozenDictionary<int, SkillTemplate> _skillsById;
    private readonly FrozenDictionary<string, SkillTemplate> _skillsByCode;

    public int Count => _skillsById.Count;

    public IReadOnlyCollection<SkillTemplate> AllSkills => _skillsById.Values;

    public SkillTemplateCatalog(IOptions<GameAssetsOptions> options, ILogger<SkillTemplateCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        IReadOnlyList<SkillTemplate> skills;
        string sourceTier;
        if (GameAssetsSnapshotLoader.TryLoadBundle(options.Value, out GameAssetsSnapshotBundle? bundle, out sourceTier) &&
            bundle is not null &&
            bundle.Skills.Count > 0)
        {
            skills = bundle.Skills;
        }
        else
        {
            skills = CreateDefaultSkills();
            sourceTier = "BuiltInDefaults";
        }

        var byId = new Dictionary<int, SkillTemplate>();
        var byCode = new Dictionary<string, SkillTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (SkillTemplate s in skills)
        {
            byId[s.Id] = s;
            byCode[s.Code] = s;
        }

        // Ensure canonical baseline combat skills exist even in minimal test fixtures
        foreach (SkillTemplate fallback in CreateDefaultSkills())
        {
            if (!byId.ContainsKey(fallback.Id))
            {
                byId[fallback.Id] = fallback;
                byCode.TryAdd(fallback.Code, fallback);
            }
        }

        _skillsById = byId.ToFrozenDictionary();
        _skillsByCode = byCode.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        GameAssetsLogMessages.LogSkillAndMissileCatalogsLoaded(logger, _skillsById.Count, 0, sourceTier);
    }

    public bool TryGetSkill(int skillId, out SkillTemplate? skill) =>
        _skillsById.TryGetValue(skillId, out skill);

    public bool TryGetSkillByCode(string code, out SkillTemplate? skill) =>
        _skillsByCode.TryGetValue(code, out skill);

    public int GetBasicWeaponSkillId(int weaponDetailOrParticular) =>
        weaponDetailOrParticular switch
        {
            0 => 55, // Kiếm Pháp Cơ Bản
            1 => 56, // Đao Pháp Cơ Bản
            2 => 57, // Côn Pháp Cơ Bản
            3 => 58, // Thương Pháp Cơ Bản
            4 => 59, // Chùy Pháp Cơ Bản
            5 => 60, // Song Đao Cơ Bản
            _ => 53, // Quyền Pháp Cơ Bản (Unarmed)
        };

    public static IReadOnlyList<SkillTemplate> CreateDefaultSkills() =>
    [
        new SkillTemplate(
            Id: 1,
            Code: "basic-melee-attack",
            Name: "Tấn Công Cơ Bản",
            Style: SkillStyleKind.Melee,
            ElementSeries: ElementSeries.Metal,
            IsOffensive: true,
            IsPhysical: true,
            IsMelee: true,
            IsAura: false,
            IsWeaponSkill: true,
            IsExpSkill: false,
            UseAttackRating: true,
            DoHurtAnimation: true,
            PeaceCanUse: false,
            ReqLevel: 1,
            MaxLevel: 20,
            EquipLimit: -2,
            HorseLimit: 0,
            CastRangeMps: 96,
            WaitFrames: 9,
            MissileId: 0,
            MissileForm: "line",
            ChildSkillId: 0,
            ChildSkillLevel: 0,
            ChildSkillCount: 1,
            CostScaling: new SkillCostScalingTemplate("mana", 0, 0),
            EffectScaling:
            [
                new SkillEffectScalingTemplate(
                    1,
                    "physics_damage_v",
                    "damage",
                    new SkillParameterCurveTemplate("linear", 12, 4),
                    new SkillParameterCurveTemplate("constant", 0, 0),
                    new SkillParameterCurveTemplate("linear", 20, 6)),
            ]),
        new SkillTemplate(
            Id: 29,
            Code: " npc-melee-strike",
            Name: "Quái Vật Công Kích",
            Style: SkillStyleKind.Melee,
            ElementSeries: ElementSeries.Metal,
            IsOffensive: true,
            IsPhysical: true,
            IsMelee: true,
            IsAura: false,
            IsWeaponSkill: false,
            IsExpSkill: false,
            UseAttackRating: true,
            DoHurtAnimation: true,
            PeaceCanUse: false,
            ReqLevel: 1,
            MaxLevel: 20,
            EquipLimit: -2,
            HorseLimit: 0,
            CastRangeMps: 96,
            WaitFrames: 18,
            MissileId: 0,
            MissileForm: "line",
            ChildSkillId: 0,
            ChildSkillLevel: 0,
            ChildSkillCount: 1,
            CostScaling: new SkillCostScalingTemplate("mana", 0, 0),
            EffectScaling:
            [
                new SkillEffectScalingTemplate(
                    1,
                    "physics_damage_v",
                    "damage",
                    new SkillParameterCurveTemplate("linear", 8, 3),
                    new SkillParameterCurveTemplate("constant", 0, 0),
                    new SkillParameterCurveTemplate("linear", 14, 5)),
            ]),
        new SkillTemplate(
            Id: 53,
            Code: "quyen-phap-co-ban",
            Name: "Quyền Pháp Cơ Bản",
            Style: SkillStyleKind.Melee,
            ElementSeries: ElementSeries.Metal,
            IsOffensive: true,
            IsPhysical: true,
            IsMelee: true,
            IsAura: false,
            IsWeaponSkill: true,
            IsExpSkill: false,
            UseAttackRating: true,
            DoHurtAnimation: true,
            PeaceCanUse: false,
            ReqLevel: 1,
            MaxLevel: 20,
            EquipLimit: -2,
            HorseLimit: 0,
            CastRangeMps: 96,
            WaitFrames: 9,
            MissileId: 0,
            MissileForm: "line",
            ChildSkillId: 0,
            ChildSkillLevel: 0,
            ChildSkillCount: 1,
            CostScaling: new SkillCostScalingTemplate("mana", 0, 0),
            EffectScaling:
            [
                new SkillEffectScalingTemplate(
                    1,
                    "physics_damage_v",
                    "damage",
                    new SkillParameterCurveTemplate("linear", 15, 5),
                    new SkillParameterCurveTemplate("constant", 0, 0),
                    new SkillParameterCurveTemplate("linear", 25, 8)),
            ]),
        new SkillTemplate(
            Id: 55,
            Code: "kiem-phap-co-ban",
            Name: "Kiếm Pháp Cơ Bản",
            Style: SkillStyleKind.Melee,
            ElementSeries: ElementSeries.Metal,
            IsOffensive: true,
            IsPhysical: true,
            IsMelee: true,
            IsAura: false,
            IsWeaponSkill: true,
            IsExpSkill: false,
            UseAttackRating: true,
            DoHurtAnimation: true,
            PeaceCanUse: false,
            ReqLevel: 1,
            MaxLevel: 20,
            EquipLimit: -2,
            HorseLimit: 0,
            CastRangeMps: 96,
            WaitFrames: 9,
            MissileId: 0,
            MissileForm: "line",
            ChildSkillId: 0,
            ChildSkillLevel: 0,
            ChildSkillCount: 1,
            CostScaling: new SkillCostScalingTemplate("mana", 0, 0),
            EffectScaling:
            [
                new SkillEffectScalingTemplate(
                    1,
                    "physics_damage_v",
                    "damage",
                    new SkillParameterCurveTemplate("linear", 18, 6),
                    new SkillParameterCurveTemplate("constant", 0, 0),
                    new SkillParameterCurveTemplate("linear", 28, 9)),
            ]),
        new SkillTemplate(
            Id: 100,
            Code: "hoa-lien-phan-hoa",
            Name: "Hỏa Liên Phần Hoa",
            Style: SkillStyleKind.Projectile,
            ElementSeries: ElementSeries.Fire,
            IsOffensive: true,
            IsPhysical: false,
            IsMelee: false,
            IsAura: false,
            IsWeaponSkill: false,
            IsExpSkill: false,
            UseAttackRating: false,
            DoHurtAnimation: true,
            PeaceCanUse: false,
            ReqLevel: 1,
            MaxLevel: 20,
            EquipLimit: -2,
            HorseLimit: 0,
            CastRangeMps: 512,
            WaitFrames: 12,
            MissileId: 1,
            MissileForm: "line",
            ChildSkillId: 0,
            ChildSkillLevel: 0,
            ChildSkillCount: 1,
            CostScaling: new SkillCostScalingTemplate("mana", 8, 2),
            EffectScaling:
            [
                new SkillEffectScalingTemplate(
                    2,
                    "fire_damage_v",
                    "damage",
                    new SkillParameterCurveTemplate("linear", 25, 10),
                    new SkillParameterCurveTemplate("constant", 0, 0),
                    new SkillParameterCurveTemplate("linear", 40, 14)),
            ]),
        new SkillTemplate(
            Id: 101,
            Code: "bang-tung-vo-anh",
            Name: "Băng Tung Vô Ảnh",
            Style: SkillStyleKind.Projectile,
            ElementSeries: ElementSeries.Water,
            IsOffensive: true,
            IsPhysical: false,
            IsMelee: false,
            IsAura: false,
            IsWeaponSkill: false,
            IsExpSkill: false,
            UseAttackRating: false,
            DoHurtAnimation: true,
            PeaceCanUse: false,
            ReqLevel: 1,
            MaxLevel: 20,
            EquipLimit: -2,
            HorseLimit: 0,
            CastRangeMps: 512,
            WaitFrames: 12,
            MissileId: 2,
            MissileForm: "line",
            ChildSkillId: 0,
            ChildSkillLevel: 0,
            ChildSkillCount: 1,
            CostScaling: new SkillCostScalingTemplate("mana", 10, 2),
            EffectScaling:
            [
                new SkillEffectScalingTemplate(
                    3,
                    "cold_damage_v",
                    "damage",
                    new SkillParameterCurveTemplate("linear", 20, 8),
                    new SkillParameterCurveTemplate("linear", 36, 4),
                    new SkillParameterCurveTemplate("linear", 35, 12)),
            ]),
        new SkillTemplate(
            Id: 102,
            Code: "doc-sat-chuong",
            Name: "Độc Sát Chưởng",
            Style: SkillStyleKind.Projectile,
            ElementSeries: ElementSeries.Wood,
            IsOffensive: true,
            IsPhysical: false,
            IsMelee: false,
            IsAura: false,
            IsWeaponSkill: false,
            IsExpSkill: false,
            UseAttackRating: false,
            DoHurtAnimation: true,
            PeaceCanUse: false,
            ReqLevel: 1,
            MaxLevel: 20,
            EquipLimit: -2,
            HorseLimit: 0,
            CastRangeMps: 512,
            WaitFrames: 12,
            MissileId: 3,
            MissileForm: "line",
            ChildSkillId: 0,
            ChildSkillLevel: 0,
            ChildSkillCount: 1,
            CostScaling: new SkillCostScalingTemplate("mana", 10, 2),
            EffectScaling:
            [
                new SkillEffectScalingTemplate(
                    4,
                    "poison_damage_v",
                    "damage",
                    new SkillParameterCurveTemplate("linear", 10, 4),
                    new SkillParameterCurveTemplate("constant", 90, 0),
                    new SkillParameterCurveTemplate("constant", 18, 0)),
            ]),
        new SkillTemplate(
            Id: 103,
            Code: "loi-kich-thuat",
            Name: "Lôi Kích Thuật",
            Style: SkillStyleKind.Projectile,
            ElementSeries: ElementSeries.Earth,
            IsOffensive: true,
            IsPhysical: false,
            IsMelee: false,
            IsAura: false,
            IsWeaponSkill: false,
            IsExpSkill: false,
            UseAttackRating: false,
            DoHurtAnimation: true,
            PeaceCanUse: false,
            ReqLevel: 1,
            MaxLevel: 20,
            EquipLimit: -2,
            HorseLimit: 0,
            CastRangeMps: 512,
            WaitFrames: 12,
            MissileId: 4,
            MissileForm: "line",
            ChildSkillId: 0,
            ChildSkillLevel: 0,
            ChildSkillCount: 1,
            CostScaling: new SkillCostScalingTemplate("mana", 12, 2),
            EffectScaling:
            [
                new SkillEffectScalingTemplate(
                    5,
                    "lighting_damage_v",
                    "damage",
                    new SkillParameterCurveTemplate("linear", 22, 9),
                    new SkillParameterCurveTemplate("constant", 18, 0),
                    new SkillParameterCurveTemplate("linear", 38, 13)),
            ]),
        new SkillTemplate(
            Id: 104,
            Code: "la-han-tran",
            Name: "La Hán Trận",
            Style: SkillStyleKind.Aura,
            ElementSeries: ElementSeries.Metal,
            IsOffensive: false,
            IsPhysical: false,
            IsMelee: false,
            IsAura: true,
            IsWeaponSkill: false,
            IsExpSkill: false,
            UseAttackRating: false,
            DoHurtAnimation: false,
            PeaceCanUse: true,
            ReqLevel: 1,
            MaxLevel: 20,
            EquipLimit: -2,
            HorseLimit: 0,
            CastRangeMps: 320,
            WaitFrames: 9,
            MissileId: 0,
            MissileForm: "atFirer",
            ChildSkillId: 0,
            ChildSkillLevel: 0,
            ChildSkillCount: 1,
            CostScaling: new SkillCostScalingTemplate("mana", 0, 0),
            EffectScaling: []),
    ];
}
