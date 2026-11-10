using MemoryPack;

namespace JXServer.GameAssets.Items;

/// <summary>
/// Represents a base attribute range or medicine effect on an item template.
/// </summary>
[MemoryPackable]
public sealed partial record ItemBaseAttributeTemplate(
    int AttributeId,
    string AttributeCode,
    int MinValue,
    int MaxValue,
    int DurationFrames = 0);
