using System.Collections.Generic;
using JXServer.Primitives.Combat;
using JXServer.Primitives.WorldZones;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Pre-allocated pooled projectile instance stepped by <c>MissileSimulationTickSystem</c> at 18 Hz (<c>KMissle</c>).
/// </summary>
public sealed class ActiveMissileSlot
{
    private readonly HashSet<uint> _hitTargetNpcIds = [];

    public int SlotIndex { get; }

    public bool IsActive { get; set; }

    public uint LauncherNpcId { get; set; }

    public bool IsLauncherPlayer { get; set; }

    public EntityCamp LauncherCamp { get; set; }

    public PlayerPkMode LauncherPkMode { get; set; }

    public int SkillId { get; set; }

    public int SkillLevel { get; set; }

    public int MissileTemplateId { get; set; }

    public MissileMoveKind MoveKind { get; set; }

    public byte DirIndex { get; set; }

    public int CurrentMpsX { get; set; }

    public int CurrentMpsY { get; set; }

    public int CurrentZ { get; set; }

    public long FixedX { get; set; }

    public long FixedY { get; set; }

    public int SpeedMpsPerFrame { get; set; }

    public int CurrentLifeFrame { get; set; }

    public int MaxLifeFrames { get; set; }

    public int HitRadiusMps { get; set; }

    public int DamageRadiusMps { get; set; }

    public bool IsRangeDamage { get; set; }

    public int RemainingPierceCount { get; set; }

    public bool CollideVanish { get; set; }

    public bool AutoExplode { get; set; }

    public int SubMissileId { get; set; }

    public uint FollowTargetNpcId { get; set; }

    public int OrbitCenterMpsX { get; set; }

    public int OrbitCenterMpsY { get; set; }

    public int OrbitRadiusMps { get; set; }

    public SkillDamagePayload DamagePayload { get; set; }

    public ActiveMissileSlot(int slotIndex)
    {
        SlotIndex = slotIndex;
    }

    public bool HasHitTarget(uint targetNpcId) => _hitTargetNpcIds.Contains(targetNpcId);

    public void RecordTargetHit(uint targetNpcId) => _hitTargetNpcIds.Add(targetNpcId);

    public void Reset()
    {
        IsActive = false;
        LauncherNpcId = 0;
        IsLauncherPlayer = false;
        LauncherCamp = EntityCamp.Begin;
        LauncherPkMode = PlayerPkMode.Peace;
        SkillId = 0;
        SkillLevel = 0;
        MissileTemplateId = 0;
        MoveKind = MissileMoveKind.Stand;
        DirIndex = 0;
        CurrentMpsX = 0;
        CurrentMpsY = 0;
        CurrentZ = 0;
        FixedX = 0;
        FixedY = 0;
        SpeedMpsPerFrame = 0;
        CurrentLifeFrame = 0;
        MaxLifeFrames = 0;
        HitRadiusMps = 0;
        DamageRadiusMps = 0;
        IsRangeDamage = false;
        RemainingPierceCount = 0;
        CollideVanish = true;
        AutoExplode = false;
        SubMissileId = 0;
        FollowTargetNpcId = 0;
        OrbitCenterMpsX = 0;
        OrbitCenterMpsY = 0;
        OrbitRadiusMps = 0;
        DamagePayload = default;
        _hitTargetNpcIds.Clear();
    }
}
