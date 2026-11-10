using JXServer.Primitives.Combat;
using MemoryPack;

namespace JXServer.GameAssets.Skills;

/// <summary>
/// Immutable missile trajectory and collision template loaded from <c>JXData Standard v1</c> (<c>data/skills/missiles.skills.json</c>).
/// </summary>
[MemoryPackable]
public sealed partial record MissileTemplate(
    int Id,
    string Code,
    string Name,
    MissileMoveKind MoveKind,
    string FollowKind,
    int SpeedMpsPerFrame,
    int HeightMps,
    int ZSpeedMpsPerFrame,
    int ZAccelMpsPerFrame2,
    int LifeFrames,
    int HitRadiusMps,
    int CollideRadiusCells,
    int DamageRadiusCells,
    bool IsRangeDamage,
    int DamageIntervalFrames,
    int PierceCount,
    bool CollideVanish,
    bool CanCollideFriend,
    bool AutoExplode,
    bool MustHit,
    int SubMissileId,
    int Param1 = 0,
    int Param2 = 0,
    int Param3 = 0);
