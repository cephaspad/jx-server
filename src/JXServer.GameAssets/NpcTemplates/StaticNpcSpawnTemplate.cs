using JXServer.Primitives.Characters;
using JXServer.Primitives.WorldZones;
using MemoryPack;

namespace JXServer.GameAssets.NpcTemplates;

/// <summary>
/// Immutable static NPC placement record loaded from <c>Region_S.dat</c> Section 2 (<c>NPC_SECTION</c>)
/// or <c>script/global/autoexec.lua</c> (<c>adddialognpc</c>) (`FR-004`).
/// </summary>
[MemoryPackable]
public sealed partial record StaticNpcSpawnTemplate(
    int TemplateId,
    ushort MapId,
    int SpawnMpsX,
    int SpawnMpsY,
    short Level,
    EntityKind Kind,
    EntityCamp Camp,
    ElementSeries Series,
    bool IsSpecialNpc,
    string DisplayName,
    string ScriptPath);
