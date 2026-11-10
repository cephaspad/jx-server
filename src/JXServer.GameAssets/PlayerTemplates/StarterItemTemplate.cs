using JXServer.Primitives.Items;
using MemoryPack;

namespace JXServer.GameAssets.PlayerTemplates;

/// <summary>
/// Starter item entry seeded into a newly created character's inventory (`FR-010`).
/// </summary>
[MemoryPackable]
public sealed partial record StarterItemTemplate(
    string ItemCode,
    ItemGenreKind Genre,
    short DetailType,
    short ParticularType,
    byte Level,
    ushort StackCount,
    ItemContainerPlace ContainerPlace,
    byte GridX,
    byte GridY);
