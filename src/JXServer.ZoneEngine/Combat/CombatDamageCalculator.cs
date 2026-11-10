using System;
using JXServer.GameAssets.Skills;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.NpcEntities;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Implements canonical combat relation checks (<c>KNpcSet::GetRelation</c>), skill damage payload assembly
/// (<c>KNpc::AppendSkillEffect</c>), hit rating resolution (<c>KNpc::CheckHitTarget</c>), and 6-channel Five-Elements
/// damage + status ailment application (<c>KNpc::ReceiveDamage</c> and <c>KNpc::CalcDamage</c>).
/// </summary>
public static class CombatDamageCalculator
{
    private static readonly byte[,] CampRelationTable = BuildCampRelationTable();

    private static byte[,] BuildCampRelationTable()
    {
        var table = new byte[9, 9];
        for (int s = 0; s < 9; s++)
        {
            for (int t = 0; t < 9; t++)
            {
                if (s == (int)EntityCamp.Event || t == (int)EntityCamp.Event)
                {
                    table[s, t] = (byte)EntityRelationKind.None;
                }
                else if (s == (int)EntityCamp.Begin || t == (int)EntityCamp.Begin)
                {
                    // Novice camp (camp_begin = 0) is friendly to other players/good camps, hostile to Evil/Animal/Killer
                    table[s, t] = (s is (int)EntityCamp.Evil or (int)EntityCamp.Animal or (int)EntityCamp.Killer ||
                                   t is (int)EntityCamp.Evil or (int)EntityCamp.Animal or (int)EntityCamp.Killer)
                        ? (byte)EntityRelationKind.Enemy
                        : (byte)EntityRelationKind.Friend;
                }
                else if (s == (int)EntityCamp.Animal || t == (int)EntityCamp.Animal)
                {
                    table[s, t] = s == t ? (byte)EntityRelationKind.Friend : (byte)EntityRelationKind.Enemy;
                }
                else if (s == (int)EntityCamp.Killer || t == (int)EntityCamp.Killer)
                {
                    table[s, t] = (byte)EntityRelationKind.Enemy;
                }
                else if (s == t)
                {
                    table[s, t] = (byte)EntityRelationKind.Friend;
                }
                else
                {
                    table[s, t] = (byte)EntityRelationKind.Enemy;
                }
            }
        }

        return table;
    }

    /// <summary>
    /// Evaluates the relationship between two entities matching <c>KNpcSet::GetRelation</c>.
    /// </summary>
    public static EntityRelationKind EvaluateRelation(
        uint attackerNpcId,
        EntityKind attackerKind,
        EntityCamp attackerCamp,
        byte attackerFightMode,
        PlayerPkMode attackerPkMode,
        uint defenderNpcId,
        EntityKind defenderKind,
        EntityCamp defenderCamp,
        byte defenderFightMode,
        PlayerPkMode defenderPkMode)
    {
        if (attackerNpcId == defenderNpcId)
        {
            return EntityRelationKind.Self;
        }

        if (attackerKind == EntityKind.DialogNpc || defenderKind == EntityKind.DialogNpc)
        {
            return EntityRelationKind.None;
        }

        if (attackerKind != EntityKind.Player && defenderKind != EntityKind.Player)
        {
            int s = Math.Clamp((int)attackerCamp, 0, 8);
            int t = Math.Clamp((int)defenderCamp, 0, 8);
            return (EntityRelationKind)CampRelationTable[s, t];
        }

        if (attackerKind != EntityKind.Player || defenderKind != EntityKind.Player)
        {
            byte playerFightMode = attackerKind == EntityKind.Player ? attackerFightMode : defenderFightMode;
            if (playerFightMode == 0)
            {
                return EntityRelationKind.None;
            }

            int s = Math.Clamp((int)attackerCamp, 0, 8);
            int t = Math.Clamp((int)defenderCamp, 0, 8);
            return (EntityRelationKind)CampRelationTable[s, t];
        }

        // Player vs Player
        if (attackerFightMode == 0 || defenderFightMode == 0)
        {
            return EntityRelationKind.Friend;
        }

        if (attackerCamp == EntityCamp.Begin || defenderCamp == EntityCamp.Begin)
        {
            return EntityRelationKind.Friend;
        }

        if (attackerPkMode == PlayerPkMode.Peace || defenderPkMode == PlayerPkMode.Peace)
        {
            return EntityRelationKind.Friend;
        }

        if (attackerPkMode == PlayerPkMode.Slaughter || defenderPkMode == PlayerPkMode.Slaughter)
        {
            return EntityRelationKind.Enemy;
        }

        // Fight mode (1) vs Fight mode (1): check camp relation
        int ps = Math.Clamp((int)attackerCamp, 0, 8);
        int pt = Math.Clamp((int)defenderCamp, 0, 8);
        return (EntityRelationKind)CampRelationTable[ps, pt];
    }

    /// <summary>
    /// Builds a <see cref="SkillDamagePayload"/> for a player casting <paramref name="skill"/> at <paramref name="skillLevel"/>.
    /// </summary>
    public static SkillDamagePayload BuildPlayerSkillDamagePayload(
        PlayerZoneState attacker,
        SkillTemplate skill,
        int skillLevel)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(skill);

        int clampedLevel = Math.Clamp(skillLevel, 1, Math.Max(1, skill.MaxLevel));
        int basePhysMin = attacker.WeaponMinDamage > 0
            ? attacker.WeaponMinDamage + attacker.BonusPhysicsDamage
            : Math.Max(2, attacker.EffectiveStrength / CombatConsts.UnarmedStrengthDivisor) + attacker.BonusPhysicsDamage;
        int basePhysMax = attacker.WeaponMaxDamage > 0
            ? Math.Max(basePhysMin, attacker.WeaponMaxDamage + attacker.BonusPhysicsDamage)
            : Math.Max(basePhysMin + 2, (attacker.EffectiveStrength / CombatConsts.UnarmedStrengthDivisor) + 4 + attacker.BonusPhysicsDamage);

        int physMin = skill.IsPhysical ? basePhysMin : 0;
        int physMax = skill.IsPhysical ? basePhysMax : 0;
        int coldMin = 0;
        int coldMax = 0;
        int coldDur = 0;
        int fireMin = 0;
        int fireMax = 0;
        int lightMin = 0;
        int lightMax = 0;
        int lightDur = 0;
        int poisonDmg = 0;
        int poisonDur = 0;
        int poisonInterval = 18;
        int magicMin = 0;
        int magicMax = 0;
        int arPercentBonus = 0;

        foreach (SkillEffectScalingTemplate eff in skill.EffectScaling)
        {
            int p0 = eff.Param0?.Evaluate(clampedLevel) ?? 0;
            int p1 = eff.Param1?.Evaluate(clampedLevel) ?? 0;
            int p2 = eff.Param2?.Evaluate(clampedLevel) ?? p0;

            string code = eff.AttributeCode ?? string.Empty;
            if (code.Contains("physics_damage_v", StringComparison.OrdinalIgnoreCase))
            {
                physMin += p0;
                physMax += Math.Max(p0, p2);
            }
            else if (code.Contains("physics_damage_p", StringComparison.OrdinalIgnoreCase))
            {
                physMin += (basePhysMin * p0) / 100;
                physMax += (basePhysMax * Math.Max(p0, p2)) / 100;
            }
            else if (code.Contains("cold_damage_v", StringComparison.OrdinalIgnoreCase))
            {
                coldMin += p0;
                coldMax += Math.Max(p0, p2);
                coldDur = Math.Max(coldDur, p1 > 0 ? p1 : 36);
            }
            else if (code.Contains("fire_damage_v", StringComparison.OrdinalIgnoreCase))
            {
                fireMin += p0;
                fireMax += Math.Max(p0, p2);
            }
            else if (code.Contains("lighting_damage_v", StringComparison.OrdinalIgnoreCase) ||
                     code.Contains("light_damage_v", StringComparison.OrdinalIgnoreCase))
            {
                lightMin += p0;
                lightMax += Math.Max(p0, p2);
                lightDur = Math.Max(lightDur, p1 > 0 ? p1 : 18);
            }
            else if (code.Contains("poison_damage_v", StringComparison.OrdinalIgnoreCase))
            {
                poisonDmg += Math.Max(1, p0);
                poisonDur = Math.Max(poisonDur, p1 > 0 ? p1 : 90);
                poisonInterval = p2 > 0 ? p2 : 18;
            }
            else if (code.Contains("magic_damage_v", StringComparison.OrdinalIgnoreCase))
            {
                magicMin += p0;
                magicMax += Math.Max(p0, p2);
            }
            else if (code.Contains("attackrating_p", StringComparison.OrdinalIgnoreCase))
            {
                arPercentBonus += p0;
            }
        }

        int finalAr = attacker.EffectiveAttackRating;
        if (arPercentBonus != 0)
        {
            finalAr = Math.Max(10, finalAr + ((finalAr * arPercentBonus) / 100));
        }

        return new SkillDamagePayload(
            SkillId: skill.Id,
            SkillLevel: clampedLevel,
            ElementSeries: skill.ElementSeries,
            IsPhysical: skill.IsPhysical,
            IsMelee: skill.IsMelee,
            UseAttackRating: skill.UseAttackRating,
            DoHurtAnimation: skill.DoHurtAnimation,
            AttackRatingScore: finalAr,
            PhysicsMinDamage: physMin,
            PhysicsMaxDamage: Math.Max(physMin, physMax),
            ColdMinDamage: coldMin,
            ColdMaxDamage: Math.Max(coldMin, coldMax),
            ColdFreezeDurationFrames: coldDur,
            FireMinDamage: fireMin,
            FireMaxDamage: Math.Max(fireMin, fireMax),
            LightMinDamage: lightMin,
            LightMaxDamage: Math.Max(lightMin, lightMax),
            LightStunDurationFrames: lightDur,
            PoisonDamagePerTick: poisonDmg,
            PoisonDurationFrames: poisonDur,
            PoisonIntervalFrames: poisonInterval,
            MagicMinDamage: magicMin,
            MagicMaxDamage: Math.Max(magicMin, magicMax));
    }

    /// <summary>
    /// Builds a <see cref="SkillDamagePayload"/> for an NPC/monster casting <paramref name="skill"/> at <paramref name="skillLevel"/>.
    /// </summary>
    public static SkillDamagePayload BuildNpcSkillDamagePayload(
        NpcZoneState attacker,
        SkillTemplate? skill,
        int skillLevel)
    {
        ArgumentNullException.ThrowIfNull(attacker);

        if (skill is null)
        {
            return new SkillDamagePayload(
                SkillId: 29,
                SkillLevel: Math.Max(1, skillLevel),
                ElementSeries: attacker.Series,
                IsPhysical: true,
                IsMelee: true,
                UseAttackRating: true,
                DoHurtAnimation: true,
                AttackRatingScore: attacker.AttackRating,
                PhysicsMinDamage: attacker.PhysicsMinDamage,
                PhysicsMaxDamage: Math.Max(attacker.PhysicsMinDamage, attacker.PhysicsMaxDamage),
                ColdMinDamage: 0,
                ColdMaxDamage: 0,
                ColdFreezeDurationFrames: 0,
                FireMinDamage: 0,
                FireMaxDamage: 0,
                LightMinDamage: 0,
                LightMaxDamage: 0,
                LightStunDurationFrames: 0,
                PoisonDamagePerTick: 0,
                PoisonDurationFrames: 0,
                PoisonIntervalFrames: 18,
                MagicMinDamage: 0,
                MagicMaxDamage: 0);
        }

        int clampedLevel = Math.Clamp(skillLevel, 1, Math.Max(1, skill.MaxLevel));
        int physMin = skill.IsPhysical ? attacker.PhysicsMinDamage : 0;
        int physMax = skill.IsPhysical ? Math.Max(attacker.PhysicsMinDamage, attacker.PhysicsMaxDamage) : 0;
        int coldMin = 0;
        int coldMax = 0;
        int coldDur = 0;
        int fireMin = 0;
        int fireMax = 0;
        int lightMin = 0;
        int lightMax = 0;
        int lightDur = 0;
        int poisonDmg = 0;
        int poisonDur = 0;
        int poisonInterval = 18;
        int magicMin = 0;
        int magicMax = 0;

        foreach (SkillEffectScalingTemplate eff in skill.EffectScaling)
        {
            int p0 = eff.Param0?.Evaluate(clampedLevel) ?? 0;
            int p1 = eff.Param1?.Evaluate(clampedLevel) ?? 0;
            int p2 = eff.Param2?.Evaluate(clampedLevel) ?? p0;

            string code = eff.AttributeCode ?? string.Empty;
            if (code.Contains("physics_damage_v", StringComparison.OrdinalIgnoreCase))
            {
                physMin += p0;
                physMax += Math.Max(p0, p2);
            }
            else if (code.Contains("cold_damage_v", StringComparison.OrdinalIgnoreCase))
            {
                coldMin += p0;
                coldMax += Math.Max(p0, p2);
                coldDur = Math.Max(coldDur, p1 > 0 ? p1 : 36);
            }
            else if (code.Contains("fire_damage_v", StringComparison.OrdinalIgnoreCase))
            {
                fireMin += p0;
                fireMax += Math.Max(p0, p2);
            }
            else if (code.Contains("lighting_damage_v", StringComparison.OrdinalIgnoreCase) ||
                     code.Contains("light_damage_v", StringComparison.OrdinalIgnoreCase))
            {
                lightMin += p0;
                lightMax += Math.Max(p0, p2);
                lightDur = Math.Max(lightDur, p1 > 0 ? p1 : 18);
            }
            else if (code.Contains("poison_damage_v", StringComparison.OrdinalIgnoreCase))
            {
                poisonDmg += Math.Max(1, p0);
                poisonDur = Math.Max(poisonDur, p1 > 0 ? p1 : 90);
                poisonInterval = p2 > 0 ? p2 : 18;
            }
            else if (code.Contains("magic_damage_v", StringComparison.OrdinalIgnoreCase))
            {
                magicMin += p0;
                magicMax += Math.Max(p0, p2);
            }
        }

        return new SkillDamagePayload(
            SkillId: skill.Id,
            SkillLevel: clampedLevel,
            ElementSeries: skill.ElementSeries,
            IsPhysical: skill.IsPhysical,
            IsMelee: skill.IsMelee,
            UseAttackRating: skill.UseAttackRating,
            DoHurtAnimation: skill.DoHurtAnimation,
            AttackRatingScore: attacker.AttackRating,
            PhysicsMinDamage: physMin,
            PhysicsMaxDamage: Math.Max(physMin, physMax),
            ColdMinDamage: coldMin,
            ColdMaxDamage: Math.Max(coldMin, coldMax),
            ColdFreezeDurationFrames: coldDur,
            FireMinDamage: fireMin,
            FireMaxDamage: Math.Max(fireMin, fireMax),
            LightMinDamage: lightMin,
            LightMaxDamage: Math.Max(lightMin, lightMax),
            LightStunDurationFrames: lightDur,
            PoisonDamagePerTick: poisonDmg,
            PoisonDurationFrames: poisonDur,
            PoisonIntervalFrames: poisonInterval,
            MagicMinDamage: magicMin,
            MagicMaxDamage: Math.Max(magicMin, magicMax));
    }

    /// <summary>
    /// Computes the hit percentage clamped to <c>[5, 95]</c> (<c>KNpc::CheckHitTarget</c>).
    /// </summary>
    public static int ComputeHitPercent(int attackRating, int defense)
    {
        if (attackRating <= 0)
        {
            return CombatConsts.MinHitPercent;
        }

        int safeDef = Math.Max(0, defense);
        int denom = attackRating + safeDef;
        if (denom <= 0)
        {
            return CombatConsts.MinHitPercent;
        }

        int pct = (attackRating * 100) / denom;
        return Math.Clamp(pct, CombatConsts.MinHitPercent, CombatConsts.MaxHitPercent);
    }

    /// <summary>
    /// Checks whether an attack hits the target using <see cref="ComputeHitPercent"/> when <paramref name="useAttackRating"/> is <see langword="true"/>.
    /// </summary>
    public static bool CheckHitTarget(bool useAttackRating, int attackRating, int defense, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (!useAttackRating)
        {
            return true;
        }

        int hitPct = ComputeHitPercent(attackRating, defense);
        return random.Next(100) < hitPct;
    }

    /// <summary>
    /// Resolves 6-channel damage and status effects against a defender, returning the total immediate HP damage dealt.
    /// </summary>
    public static int ResolveHitAndApplyDamage(
        uint attackerNpcId,
        bool isPvp,
        in SkillDamagePayload payload,
        ElementSeries defenderSeries,
        int defenderPhysicsResist,
        int defenderColdResist,
        int defenderFireResist,
        int defenderLightResist,
        int defenderPoisonResist,
        EntityStatusEffectState defenderStatusEffects,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(defenderStatusEffects);
        ArgumentNullException.ThrowIfNull(random);

        int seriesMult = FiveElementsSeriesMatrix.GetSeriesDamageMultiplierPercent(payload.ElementSeries, defenderSeries);
        int totalDamage = 0;

        // 0. Physics
        if (payload.PhysicsMaxDamage > 0)
        {
            int raw = RollRange(random, payload.PhysicsMinDamage, payload.PhysicsMaxDamage);
            int reduced = ApplyResistanceAndSeries(raw, defenderPhysicsResist, seriesMult);
            totalDamage += reduced;
        }

        // 1. Cold (with freeze/slow duration scaled by cold resist)
        if (payload.ColdMaxDamage > 0)
        {
            int raw = RollRange(random, payload.ColdMinDamage, payload.ColdMaxDamage);
            int reduced = ApplyResistanceAndSeries(raw, defenderColdResist, seriesMult);
            totalDamage += reduced;

            if (payload.ColdFreezeDurationFrames > 0)
            {
                int clampedRes = Math.Clamp(defenderColdResist, -100, CombatConsts.DefaultMaxResistPercent);
                int freezeFrames = (payload.ColdFreezeDurationFrames * (100 - clampedRes)) / 100;
                if (freezeFrames > 0)
                {
                    defenderStatusEffects.ApplyColdFreeze(freezeFrames);
                }
            }
        }

        // 2. Fire
        if (payload.FireMaxDamage > 0)
        {
            int raw = RollRange(random, payload.FireMinDamage, payload.FireMaxDamage);
            int reduced = ApplyResistanceAndSeries(raw, defenderFireResist, seriesMult);
            totalDamage += reduced;
        }

        // 3. Lightning (with stun duration scaled by light resist)
        if (payload.LightMaxDamage > 0)
        {
            int raw = RollRange(random, payload.LightMinDamage, payload.LightMaxDamage);
            int reduced = ApplyResistanceAndSeries(raw, defenderLightResist, seriesMult);
            totalDamage += reduced;

            if (payload.LightStunDurationFrames > 0)
            {
                int clampedRes = Math.Clamp(defenderLightResist, -100, CombatConsts.DefaultMaxResistPercent);
                int stunFrames = (payload.LightStunDurationFrames * (100 - clampedRes)) / 100;
                if (stunFrames > 0)
                {
                    defenderStatusEffects.ApplyLightStun(stunFrames);
                }
            }
        }

        // 4. Poison (weighted DoT blending)
        if (payload.PoisonDamagePerTick > 0 && payload.PoisonDurationFrames > 0)
        {
            int poisonTickDmg = ApplyResistanceAndSeries(payload.PoisonDamagePerTick, defenderPoisonResist, seriesMult);
            int clampedPoisonRes = Math.Clamp(defenderPoisonResist, -100, CombatConsts.DefaultMaxResistPercent);
            int poisonDur = Math.Max(18, (payload.PoisonDurationFrames * (100 - clampedPoisonRes)) / 100);
            if (isPvp)
            {
                poisonTickDmg = Math.Max(1, (poisonTickDmg * CombatConsts.DefaultPkDamageRatePercent) / 100);
            }

            defenderStatusEffects.BlendPoisonDamageOverTime(
                poisonTickDmg,
                poisonDur,
                payload.PoisonIntervalFrames,
                attackerNpcId);
            totalDamage += poisonTickDmg;
        }

        // 5. Magic (unresisted)
        if (payload.MagicMaxDamage > 0)
        {
            int raw = RollRange(random, payload.MagicMinDamage, payload.MagicMaxDamage);
            totalDamage += Math.Max(1, (raw * seriesMult) / 100);
        }

        if (isPvp && totalDamage > 0)
        {
            totalDamage = Math.Max(1, (totalDamage * CombatConsts.DefaultPkDamageRatePercent) / 100);
        }

        return Math.Max(1, totalDamage);
    }

    private static int RollRange(Random random, int min, int max)
    {
        if (max <= min)
        {
            return Math.Max(0, min);
        }

        return random.Next(Math.Max(0, min), max + 1);
    }

    private static int ApplyResistanceAndSeries(int rawDamage, int resistPercent, int seriesMultiplierPercent)
    {
        if (rawDamage <= 0)
        {
            return 0;
        }

        int clampedRes = Math.Clamp(resistPercent, -100, CombatConsts.DefaultMaxResistPercent);
        long scaled = (long)rawDamage * (100 - clampedRes) * seriesMultiplierPercent;
        return (int)Math.Max(1L, scaled / 10000L);
    }
}
