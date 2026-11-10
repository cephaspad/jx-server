using MemoryPack;

namespace JXServer.GameAssets.NpcTemplates;

/// <summary>
/// AI behavior parameters for an NPC/Monster template.
/// </summary>
[MemoryPackable]
public sealed partial record NpcAiConfigTemplate(
    int AiMode = 1,
    int AiParam1 = 0,
    int AiParam2 = 0,
    int AiParam3 = 0,
    int AiMaxTimeFrames = 180);
