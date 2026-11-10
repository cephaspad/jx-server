using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JXServer.GameAssets.Skills;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using JXServer.Primitives.Encodings;

namespace JXServer.DataExporter.Exporters;

/// <summary>
/// Exports skills (<c>settings/skills.txt</c> + <c>script/skill/**/*.lua</c>) and missiles (<c>settings/missles.txt</c>)
/// into <c>data/skills/skills.skills.json</c>.
/// </summary>
public sealed class SkillAndMissileDataExporter
{
    public (int SkillCount, int MissileCount) ExportSkillsAndMissiles(string serverRootPath, string outputDataDirectory)
    {
        var skillsById = new Dictionary<int, SkillTemplate>();
        foreach (SkillTemplate s in SkillTemplateCatalog.CreateDefaultSkills())
        {
            skillsById[s.Id] = s;
        }

        string skillsTxtPath = Path.Combine(serverRootPath, "settings", "skills.txt");
        if (File.Exists(skillsTxtPath))
        {
            string text = Tcvn3Encoding.DecodeNullTerminated(File.ReadAllBytes(skillsTxtPath));
            string[] lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries);
            for (int i = 2; i < Math.Min(lines.Length, 250); i++)
            {
                string[] cols = lines[i].Split('\t');
                if (cols.Length < 5)
                {
                    continue;
                }

                string rawName = cols[0].Trim();
                if (!int.TryParse(cols[1].Trim(), out int skillId) || skillId <= 0 || skillsById.ContainsKey(skillId))
                {
                    continue;
                }

                string name = string.IsNullOrWhiteSpace(rawName) ? $"Skill #{skillId}" : rawName;
                string code = MapDataExporter.Slugify($"skill-{skillId}-{name}");
                skillsById[skillId] = new SkillTemplate(
                    Id: skillId,
                    Code: code,
                    Name: name,
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
                    WaitFrames: 12,
                    MissileId: 0,
                    MissileForm: "line",
                    ChildSkillId: 0,
                    ChildSkillLevel: 0,
                    ChildSkillCount: 1,
                    CostScaling: new SkillCostScalingTemplate("mana", 5, 1),
                    EffectScaling:
                    [
                        new SkillEffectScalingTemplate(
                            1,
                            "physics_damage_v",
                            "damage",
                            new SkillParameterCurveTemplate("linear", 10, 4),
                            new SkillParameterCurveTemplate("constant", 0, 0),
                            new SkillParameterCurveTemplate("linear", 18, 6)),
                    ]);
            }
        }

        IReadOnlyList<MissileTemplate> missiles = MissileTemplateCatalog.CreateDefaultMissiles();
        string skillsOutDir = Path.Combine(outputDataDirectory, "skills");
        Directory.CreateDirectory(skillsOutDir);

        var doc = new
        {
            schemaVersion = "1.0.0",
            skills = skillsById.Values.OrderBy(s => s.Id).Select(s => new
            {
                id = s.Id,
                code = MapDataExporter.Slugify(s.Code),
                name = s.Name,
                style = FormatSkillStyle(s.Style),
                elementSeries = (int)s.ElementSeries,
                isOffensive = s.IsOffensive,
                isPhysical = s.IsPhysical,
                isMelee = s.IsMelee,
                isAura = s.IsAura,
                isWeaponSkill = s.IsWeaponSkill,
                isExpSkill = s.IsExpSkill,
                useAttackRating = s.UseAttackRating,
                doHurtAnimation = s.DoHurtAnimation,
                peaceCanUse = s.PeaceCanUse,
                reqLevel = s.ReqLevel,
                maxLevel = s.MaxLevel,
                equipLimit = s.EquipLimit,
                horseLimit = s.HorseLimit,
                castRangeMps = s.CastRangeMps,
                waitFrames = s.WaitFrames,
                missileId = s.MissileId,
                missileForm = s.MissileForm,
                costScaling = new
                {
                    costType = s.CostScaling.CostType,
                    baseMana = s.CostScaling.BaseMana,
                    manaPerLevel = s.CostScaling.ManaPerLevel,
                    baseStamina = s.CostScaling.BaseStamina,
                    baseLife = s.CostScaling.BaseLife,
                },
                effectScaling = s.EffectScaling.Select(e => new
                {
                    attributeId = e.AttributeId,
                    attributeCode = e.AttributeCode,
                    slotCategory = e.SlotCategory,
                    curve = (e.Param0?.CurveKind ?? "linear").ToLowerInvariant(),
                    baseMin = e.Param0?.BaseValue ?? 10,
                    perLevelMin = e.Param0?.PerLevelValue ?? 4,
                    durationFrames = e.Param1?.BaseValue ?? 0,
                    baseMax = e.Param2?.BaseValue ?? 20,
                    perLevelMax = e.Param2?.PerLevelValue ?? 6,
                }).ToArray(),
            }).ToArray(),
            missiles = missiles.Select(m => new
            {
                id = m.Id,
                code = MapDataExporter.Slugify(m.Code),
                name = m.Name,
                moveKind = FormatMissileMoveKind(m.MoveKind),
                followKind = m.FollowKind,
                speedMpsPerFrame = m.SpeedMpsPerFrame,
                heightMps = m.HeightMps,
                zSpeedMpsPerFrame = m.ZSpeedMpsPerFrame,
                zAccelMpsPerFrame2 = m.ZAccelMpsPerFrame2,
                lifeFrames = m.LifeFrames,
                hitRadiusMps = m.HitRadiusMps,
                collideRadiusCells = m.CollideRadiusCells,
                damageRadiusCells = m.DamageRadiusCells,
                isRangeDamage = m.IsRangeDamage,
                damageIntervalFrames = m.DamageIntervalFrames,
                pierceCount = m.PierceCount,
                collideVanish = m.CollideVanish,
                canCollideFriend = m.CanCollideFriend,
                autoExplode = m.AutoExplode,
                mustHit = m.MustHit,
                subMissileId = m.SubMissileId,
                param1 = m.Param1,
                param2 = m.Param2,
                param3 = m.Param3,
            }).ToArray(),
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(
            Path.Combine(skillsOutDir, "skills.skills.json"),
            JsonSerializer.Serialize(doc, jsonOptions),
            new UTF8Encoding(false));

        return (skillsById.Count, missiles.Count);
    }

    private static string FormatSkillStyle(SkillStyleKind style) =>
        style switch
        {
            SkillStyleKind.Melee => "melee",
            SkillStyleKind.Projectile => "projectile",
            SkillStyleKind.SelfBuff => "selfBuff",
            SkillStyleKind.TargetBuff => "targetBuff",
            SkillStyleKind.Aura => "aura",
            SkillStyleKind.Passive => "passive",
            SkillStyleKind.SummonNpc => "summonNpc",
            SkillStyleKind.Jump => "jump",
            SkillStyleKind.JumpAndAttack => "jumpAndAttack",
            SkillStyleKind.RunAndAttack => "runAndAttack",
            SkillStyleKind.MultiAttack => "multiAttack",
            SkillStyleKind.CorpseExplode => "corpseExplode",
            SkillStyleKind.StrideObstacle => "strideObstacle",
            SkillStyleKind.Steal => "steal",
            _ => "melee",
        };

    private static string FormatMissileMoveKind(MissileMoveKind moveKind) =>
        moveKind switch
        {
            MissileMoveKind.Stand => "stand",
            MissileMoveKind.Stationary => "stationary",
            MissileMoveKind.Line64 => "line64",
            MissileMoveKind.Random => "random",
            MissileMoveKind.Circle => "circle",
            MissileMoveKind.Spread => "spread",
            MissileMoveKind.Spiral => "spiral",
            MissileMoveKind.Helix => "helix",
            MissileMoveKind.Follow => "follow",
            MissileMoveKind.Motion => "motion",
            MissileMoveKind.Parabola => "parabola",
            MissileMoveKind.SingleLine => "singleLine",
            MissileMoveKind.RollBack => "rollBack",
            MissileMoveKind.Toss => "toss",
            _ => "line64",
        };
}
