using System.Collections.Generic;
using JXServer.Primitives.Characters;
using JXServer.Primitives.WorldZones;
using MemoryPack;

namespace JXServer.GameAssets.NpcTemplates;

/// <summary>
/// Immutable static NPC/Monster archetype loaded from <c>JXData Standard v1</c> (<c>data/npcs/*.npcs.json</c> or <c>settings/npcs.txt</c>).
/// </summary>
[MemoryPackable]
public sealed partial record NpcTemplate(
    int TemplateId,
    string Name,
    EntityKind Kind,
    EntityCamp Camp,
    ElementSeries Series,
    int WalkSpeed,
    int RunSpeed,
    int BaseLifeMax,
    int DialogRadius = 4,
    string Code = "",
    int AttackSpeedFrames = 18,
    int CastSpeedFrames = 18,
    int HitRecoverFrames = 6,
    int AttackRadiusMps = 96,
    int ActiveRadiusMps = 400,
    int VisionRadiusMps = 320,
    int PursuitRadiusMps = 800,
    int RespawnFrames = 180,
    int BaseExp = 80,
    int TreasureDropCount = 2,
    string LootTableCode = "default-monster-loot",
    IReadOnlyList<NpcSkillSlotTemplate>? Skills = null,
    NpcAiConfigTemplate? AiConfig = null,
    NpcCombatScalingTemplate? CombatScaling = null);
