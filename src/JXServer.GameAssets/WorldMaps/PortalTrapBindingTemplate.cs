using JXServer.Primitives.WorldZones;
using MemoryPack;

namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Immutable binding between a 32-bit region cell <c>TrapId</c> (<c>g_FileName2Id</c> hash)
/// and its server-side execution parameters parsed from <c>script/**/trap/*.lua</c> (`FR-008`, `FR-009`).
/// </summary>
[MemoryPackable]
public sealed partial record PortalTrapBindingTemplate(
    uint TrapId,
    string ScriptPath,
    PortalTrapActionKind ActionKind,
    ushort TargetMapId,
    int TargetMpsX,
    int TargetMpsY,
    byte TargetFightMode,
    int AlternateMpsX = 0,
    int AlternateMpsY = 0,
    byte AlternateFightMode = 0,
    ushort MinRequiredLevel = 1,
    int BounceBackMpsX = 0,
    int BounceBackMpsY = 0,
    string? DeniedMessage = null);
