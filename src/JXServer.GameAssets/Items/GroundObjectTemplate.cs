using JXServer.Primitives.Items;
using MemoryPack;

namespace JXServer.GameAssets.Items;

/// <summary>
/// Immutable ground object visual/behavior template from <c>settings/obj/objdata.txt</c>.
/// </summary>
[MemoryPackable]
public sealed partial record GroundObjectTemplate(
    int Id,
    string Code,
    string Name,
    GroundObjectKind Kind,
    int LifeFrames,
    int ObstacleKind = 0,
    string? ScriptPath = null);
