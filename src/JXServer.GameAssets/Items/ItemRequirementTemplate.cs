using MemoryPack;

namespace JXServer.GameAssets.Items;

/// <summary>
/// Represents a stat, level, series, gender, or faction requirement on an item template.
/// </summary>
[MemoryPackable]
public sealed partial record ItemRequirementTemplate(
    int RequirementType,
    string RequirementCode,
    int MinValue);
