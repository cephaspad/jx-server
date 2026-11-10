using System;
using System.Collections.Generic;
using JXServer.GameAssets.NpcTemplates;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.Combat;

namespace JXServer.ZoneEngine.NpcEntities;

/// <summary>
/// In-memory 18-Hz zone simulation state for a static non-player character or monster (`NpcId` in <c>1..9999</c>).
/// </summary>
public sealed class NpcZoneState
{
    public uint NpcId { get; }

    public int TemplateId { get; }

    public string Name { get; }

    public short Level { get; }

    public EntityKind Kind { get; }

    public EntityCamp Camp { get; set; }

    public ElementSeries Series { get; }

    public bool IsSpecialNpc { get; }

    public ushort MapId { get; }

    public int SpawnMpsX { get; }

    public int SpawnMpsY { get; }

    public int MpsX { get; set; }

    public int MpsY { get; set; }

    public int TargetMpsX { get; set; }

    public int TargetMpsY { get; set; }

    public int RegionX => MpsX / WorldCoordinateConsts.RegionWidthMps;

    public int RegionY => MpsY / WorldCoordinateConsts.RegionHeightMps;

    public EntityDoingPosture Doing { get; set; } = EntityDoingPosture.Stand;

    public byte Dir { get; set; }

    public int WalkSpeed { get; }

    public int RunSpeed { get; }

    public uint CurLife { get; set; }

    public uint MaxLife { get; }

    public string ScriptPath { get; }

    // Monster AI & Combat template properties
    public MonsterAiStateKind AiState { get; set; } = MonsterAiStateKind.IdlePatrol;

    public uint AggroTargetNpcId { get; set; }

    public long NextAiActionTick { get; set; }

    public long NextAttackReadyTick { get; set; }

    public long RespawnReadyTick { get; set; }

    public int ActiveRadiusMps { get; set; } = 400;

    public int VisionRadiusMps { get; set; } = 320;

    public int PursuitRadiusMps { get; set; } = 800;

    public int AttackRadiusMps { get; set; } = 96;

    public int AttackSpeedFrames { get; set; } = 18;

    public int HitRecoverFrames { get; set; } = 6;

    public int RespawnFrames { get; set; } = 180;

    public int ExpReward { get; set; } = 80;

    public int TreasureDropCount { get; set; } = 2;

    public string LootTableCode { get; set; } = "default-monster-loot";

    public int AttackRating { get; set; } = 60;

    public int Defense { get; set; } = 25;

    public int PhysicsMinDamage { get; set; } = 8;

    public int PhysicsMaxDamage { get; set; } = 14;

    public int PhysicsResistPercent { get; set; }

    public int ColdResistPercent { get; set; }

    public int FireResistPercent { get; set; }

    public int LightResistPercent { get; set; }

    public int PoisonResistPercent { get; set; }

    public IReadOnlyList<NpcSkillSlotTemplate> Skills { get; set; } = [];

    public EntityStatusEffectState StatusEffects { get; } = new();

    public int EffectiveWalkSpeed => StatusEffects.IsFrozen ? Math.Max(3, WalkSpeed / 2) : WalkSpeed;

    public int EffectiveRunSpeed => StatusEffects.IsFrozen ? Math.Max(6, RunSpeed / 2) : RunSpeed;

    /// <summary>
    /// Packed <c>NpcSettingIdx</c> for <c>s2c_syncnpc</c> (<c>0x4C</c>): <c>((uint)(ushort)Level &lt;&lt; 16) | (uint)(ushort)TemplateId</c>.
    /// </summary>
    public uint PackedNpcSettingIdx => ((uint)(ushort)Math.Max((short)1, Level) << 16) | (uint)(ushort)TemplateId;

    public byte LifePercent => MaxLife == 0
        ? (byte)100
        : (CurLife == 0 ? (byte)0 : (byte)Math.Clamp((CurLife * 100u) / MaxLife, 1u, 100u));

    public NpcZoneState(
        uint npcId,
        int templateId,
        string name,
        short level,
        EntityKind kind,
        EntityCamp camp,
        ElementSeries series,
        bool isSpecialNpc,
        ushort mapId,
        int mpsX,
        int mpsY,
        int walkSpeed,
        int runSpeed,
        uint maxLife,
        string scriptPath,
        NpcTemplate? template = null)
    {
        NpcId = npcId;
        TemplateId = templateId;
        Name = name ?? string.Empty;
        Level = Math.Max((short)1, level);
        Kind = kind;
        Camp = camp;
        Series = series;
        IsSpecialNpc = isSpecialNpc;
        MapId = mapId;
        SpawnMpsX = mpsX;
        SpawnMpsY = mpsY;
        MpsX = mpsX;
        MpsY = mpsY;
        TargetMpsX = mpsX;
        TargetMpsY = mpsY;
        WalkSpeed = walkSpeed;
        RunSpeed = runSpeed;
        MaxLife = Math.Max(1u, maxLife);
        CurLife = MaxLife;
        ScriptPath = scriptPath ?? string.Empty;

        if (template is not null)
        {
            ApplyTemplateCombatSettings(template);
        }
    }

    public void ApplyTemplateCombatSettings(NpcTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        ActiveRadiusMps = Math.Max(64, template.ActiveRadiusMps);
        VisionRadiusMps = Math.Max(64, template.VisionRadiusMps);
        PursuitRadiusMps = Math.Max(128, template.PursuitRadiusMps);
        AttackRadiusMps = Math.Max(48, template.AttackRadiusMps);
        AttackSpeedFrames = Math.Max(6, template.AttackSpeedFrames);
        HitRecoverFrames = Math.Max(0, template.HitRecoverFrames);
        RespawnFrames = Math.Max(18, template.RespawnFrames);
        TreasureDropCount = Math.Max(0, template.TreasureDropCount);
        LootTableCode = string.IsNullOrWhiteSpace(template.LootTableCode) ? "default-monster-loot" : template.LootTableCode;
        Skills = template.Skills ?? [];

        NpcCombatScalingTemplate scaling = template.CombatScaling ?? new NpcCombatScalingTemplate();
        ExpReward = scaling.EvaluateExpReward(template.BaseExp, Level);
        AttackRating = scaling.EvaluateAttackRating(Level);
        Defense = scaling.EvaluateDefense(Level);
        (int minDmg, int maxDmg) = scaling.EvaluatePhysicsDamage(Level);
        PhysicsMinDamage = minDmg;
        PhysicsMaxDamage = maxDmg;
        PhysicsResistPercent = scaling.PhysicsResist;
        ColdResistPercent = scaling.ColdResist;
        FireResistPercent = scaling.FireResist;
        LightResistPercent = scaling.LightResist;
        PoisonResistPercent = scaling.PoisonResist;
    }
}
