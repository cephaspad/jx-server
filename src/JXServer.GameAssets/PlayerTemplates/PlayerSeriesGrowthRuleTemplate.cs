using JXServer.Primitives.Characters;
using MemoryPack;

namespace JXServer.GameAssets.PlayerTemplates;

/// <summary>
/// Per-level and per-attribute growth rule for a Five-Elements series from <c>settings/npc/player/level_add.txt</c>.
/// </summary>
[MemoryPackable]
public sealed partial record PlayerSeriesGrowthRuleTemplate(
    ElementSeries Series,
    int LifePerLevel,
    int ManaPerLevel,
    int StaminaPerLevel,
    int LifePerVitality,
    int StaminaPerVitality,
    int ManaPerEnergy,
    int AttackRatingPerDexterity = 4,
    int DefensePerDexterity = 1,
    int AttributePointsPerLevel = 5,
    int SkillPointsPerLevel = 1);
