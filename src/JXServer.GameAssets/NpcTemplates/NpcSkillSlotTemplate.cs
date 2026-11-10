using MemoryPack;

namespace JXServer.GameAssets.NpcTemplates;

/// <summary>
/// Represents one of the 4 combat skill slots on an NPC/Monster template.
/// </summary>
[MemoryPackable]
public sealed partial record NpcSkillSlotTemplate(
    int SkillId,
    byte BaseLevel,
    int RatePercent = 25);
