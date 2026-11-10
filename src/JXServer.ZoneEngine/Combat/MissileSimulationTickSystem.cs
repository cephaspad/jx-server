using System;
using System.Collections.Generic;
using JXServer.GameAssets.Skills;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.NpcEntities;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.SpatialGrid;

namespace JXServer.ZoneEngine.Combat;

/// <summary>
/// Deterministic 18-Hz projectile pool and trajectory simulation tick system (<c>KMissle</c> &amp; <c>KSkill</c>).
/// Pre-allocates <see cref="ActiveMissileSlot"/> instances at zone startup for zero-allocation tick stepping.
/// </summary>
public sealed class MissileSimulationTickSystem
{
    private readonly ActiveMissileSlot[] _pool;
    private readonly Stack<int> _freeSlotIndices;
    private readonly List<int> _activeSlotIndices;
    private readonly SpatialRegionSlot[] _aoiSlots = new SpatialRegionSlot[9];
    private readonly Random _random;

    public int ActiveMissileCount => _activeSlotIndices.Count;

    public int PoolCapacity => _pool.Length;

    public MissileSimulationTickSystem(int poolCapacity = CombatConsts.MaxActiveMissilesPerZone, int? randomSeed = null)
    {
        int cap = Math.Max(32, poolCapacity);
        _pool = new ActiveMissileSlot[cap];
        _freeSlotIndices = new Stack<int>(cap);
        _activeSlotIndices = new List<int>(cap);
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();

        for (int i = cap - 1; i >= 0; i--)
        {
            _pool[i] = new ActiveMissileSlot(i);
            _freeSlotIndices.Push(i);
        }
    }

    public ActiveMissileSlot GetSlot(int slotIndex) => _pool[slotIndex];

    /// <summary>
    /// Spawns one or more projectiles for a skill cast (<c>line</c>, <c>spread</c>, <c>wall</c>, <c>circle</c>, or <c>atTarget</c>).
    /// </summary>
    public int SpawnSkillMissiles(
        uint launcherNpcId,
        bool isLauncherPlayer,
        EntityCamp launcherCamp,
        PlayerPkMode launcherPkMode,
        int originMpsX,
        int originMpsY,
        int targetMpsX,
        int targetMpsY,
        uint targetNpcId,
        SkillTemplate skill,
        MissileTemplate missileTemplate,
        in SkillDamagePayload damagePayload)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(missileTemplate);

        int rawDir = SixtyFourDirectionTrigTable.GetDirectionIndex(originMpsX, originMpsY, targetMpsX, targetMpsY);
        byte baseDir = rawDir >= 0 ? (byte)(rawDir & 63) : (byte)0;

        string form = skill.MissileForm ?? "line";
        int childCount = Math.Max(1, skill.ChildSkillCount);

        if (string.Equals(form, "circle", StringComparison.OrdinalIgnoreCase))
        {
            int count = childCount > 1 ? childCount : 8;
            int step = 64 / Math.Max(1, count);
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                byte dir = (byte)((baseDir + (i * step)) & 63);
                if (TryAllocateSlot(
                    launcherNpcId,
                    isLauncherPlayer,
                    launcherCamp,
                    launcherPkMode,
                    originMpsX,
                    originMpsY,
                    dir,
                    targetNpcId,
                    skill,
                    missileTemplate,
                    in damagePayload))
                {
                    spawned++;
                }
            }

            return spawned;
        }

        if (string.Equals(form, "spread", StringComparison.OrdinalIgnoreCase))
        {
            int count = childCount > 1 ? childCount : 3;
            int half = count / 2;
            int spawned = 0;
            for (int i = -half; i <= half; i++)
            {
                byte dir = (byte)((baseDir + (i * 2) + 64) & 63);
                if (TryAllocateSlot(
                    launcherNpcId,
                    isLauncherPlayer,
                    launcherCamp,
                    launcherPkMode,
                    originMpsX,
                    originMpsY,
                    dir,
                    targetNpcId,
                    skill,
                    missileTemplate,
                    in damagePayload))
                {
                    spawned++;
                }
            }

            return spawned;
        }

        if (string.Equals(form, "wall", StringComparison.OrdinalIgnoreCase))
        {
            int count = childCount > 1 ? childCount : 3;
            int perpDir = (baseDir + 16) & 63;
            int perpCos = SixtyFourDirectionTrigTable.DirCos(perpDir, 64);
            int perpSin = SixtyFourDirectionTrigTable.DirSin(perpDir, 64);
            int half = count / 2;
            int spawned = 0;
            for (int i = -half; i <= half; i++)
            {
                int offsetX = (i * 32 * perpCos) >> 10;
                int offsetY = (i * 32 * perpSin) >> 10;
                if (TryAllocateSlot(
                    launcherNpcId,
                    isLauncherPlayer,
                    launcherCamp,
                    launcherPkMode,
                    originMpsX + offsetX,
                    originMpsY + offsetY,
                    baseDir,
                    targetNpcId,
                    skill,
                    missileTemplate,
                    in damagePayload))
                {
                    spawned++;
                }
            }

            return spawned;
        }

        if (string.Equals(form, "atTarget", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(form, "zone", StringComparison.OrdinalIgnoreCase))
        {
            return TryAllocateSlot(
                launcherNpcId,
                isLauncherPlayer,
                launcherCamp,
                launcherPkMode,
                targetMpsX,
                targetMpsY,
                baseDir,
                targetNpcId,
                skill,
                missileTemplate,
                in damagePayload) ? 1 : 0;
        }

        // Default: "line" / single projectile
        return TryAllocateSlot(
            launcherNpcId,
            isLauncherPlayer,
            launcherCamp,
            launcherPkMode,
            originMpsX,
            originMpsY,
            baseDir,
            targetNpcId,
            skill,
            missileTemplate,
            in damagePayload) ? 1 : 0;
    }

    /// <summary>
    /// Steps all active missiles by one 18-Hz frame, checking map obstacle collisions and hostile entity hits.
    /// </summary>
    public void StepTick(
        ushort mapId,
        IMapCatalog? mapCatalog,
        SpatialRegionGrid regionGrid,
        IReadOnlyDictionary<uint, PlayerZoneState> playersByNpcId,
        IReadOnlyDictionary<uint, NpcZoneState> npcsById,
        Action<ActiveMissileSlot, PlayerZoneState, int>? onPlayerHit = null,
        Action<ActiveMissileSlot, NpcZoneState, int>? onNpcHit = null)
    {
        ArgumentNullException.ThrowIfNull(regionGrid);
        ArgumentNullException.ThrowIfNull(playersByNpcId);
        ArgumentNullException.ThrowIfNull(npcsById);

        for (int idx = _activeSlotIndices.Count - 1; idx >= 0; idx--)
        {
            int slotIndex = _activeSlotIndices[idx];
            ActiveMissileSlot slot = _pool[slotIndex];
            if (!slot.IsActive)
            {
                RemoveActiveAt(idx);
                continue;
            }

            slot.CurrentLifeFrame++;
            if (slot.CurrentLifeFrame > slot.MaxLifeFrames)
            {
                RecycleSlot(slot, idx);
                continue;
            }

            StepTrajectory(slot, playersByNpcId, npcsById);

            // Check obstacle collision (1 = Obstacle_Full, 3 = Obstacle_Fly; 2 = Obstacle_Jump allows projectiles to fly over)
            if (slot.CurrentMpsX < 0 || slot.CurrentMpsY < 0)
            {
                RecycleSlot(slot, idx);
                continue;
            }

            if (mapCatalog is not null)
            {
                int obstacle = mapCatalog.GetCellObstacle(mapId, slot.CurrentMpsX, slot.CurrentMpsY);
                if (obstacle is 1 or 3)
                {
                    RecycleSlot(slot, idx);
                    continue;
                }
            }

            // Check 3x3 region hostile target collisions
            int rx = slot.CurrentMpsX / WorldCoordinateConsts.RegionWidthMps;
            int ry = slot.CurrentMpsY / WorldCoordinateConsts.RegionHeightMps;
            int regionCount = regionGrid.GetSurroundingRegions(rx, ry, _aoiSlots);
            bool vanished = false;

            for (int r = 0; r < regionCount && !vanished; r++)
            {
                SpatialRegionSlot region = _aoiSlots[r];

                // Check NPCs/Monsters
                for (int nIdx = 0; nIdx < region.Npcs.Count && !vanished; nIdx++)
                {
                    NpcZoneState npc = region.Npcs[nIdx];
                    if (npc.CurLife == 0 || npc.AiState == MonsterAiStateKind.DeadRespawnWait || slot.HasHitTarget(npc.NpcId))
                    {
                        continue;
                    }

                    EntityRelationKind rel = CombatDamageCalculator.EvaluateRelation(
                        slot.LauncherNpcId,
                        slot.IsLauncherPlayer ? EntityKind.Player : EntityKind.Monster,
                        slot.LauncherCamp,
                        attackerFightMode: 1,
                        slot.LauncherPkMode,
                        npc.NpcId,
                        npc.Kind,
                        npc.Camp,
                        defenderFightMode: 1,
                        defenderPkMode: PlayerPkMode.Fight);

                    if (rel != EntityRelationKind.Enemy)
                    {
                        continue;
                    }

                    int dist = SixtyFourDirectionTrigTable.GetDistance(slot.CurrentMpsX, slot.CurrentMpsY, npc.MpsX, npc.MpsY);
                    if (dist <= slot.HitRadiusMps)
                    {
                        slot.RecordTargetHit(npc.NpcId);
                        SkillDamagePayload payload = slot.DamagePayload;

                        if (CombatDamageCalculator.CheckHitTarget(
                            payload.UseAttackRating,
                            payload.AttackRatingScore,
                            npc.Defense,
                            _random))
                        {
                            int dmg = CombatDamageCalculator.ResolveHitAndApplyDamage(
                                slot.LauncherNpcId,
                                isPvp: false,
                                in payload,
                                npc.Series,
                                npc.PhysicsResistPercent,
                                npc.ColdResistPercent,
                                npc.FireResistPercent,
                                npc.LightResistPercent,
                                npc.PoisonResistPercent,
                                npc.StatusEffects,
                                _random);

                            onNpcHit?.Invoke(slot, npc, dmg);
                        }

                        slot.RemainingPierceCount--;
                        if (slot.CollideVanish && slot.RemainingPierceCount <= 0)
                        {
                            RecycleSlot(slot, idx);
                            vanished = true;
                        }
                    }
                }

                // Check Players
                for (int pIdx = 0; pIdx < region.Players.Count && !vanished; pIdx++)
                {
                    PlayerZoneState targetPlayer = region.Players[pIdx];
                    if (targetPlayer.CurrentLife <= 0 ||
                        targetPlayer.Doing == EntityDoingPosture.Death ||
                        slot.HasHitTarget(targetPlayer.RuntimeNpcId))
                    {
                        continue;
                    }

                    EntityRelationKind rel = CombatDamageCalculator.EvaluateRelation(
                        slot.LauncherNpcId,
                        slot.IsLauncherPlayer ? EntityKind.Player : EntityKind.Monster,
                        slot.LauncherCamp,
                        attackerFightMode: 1,
                        slot.LauncherPkMode,
                        targetPlayer.RuntimeNpcId,
                        EntityKind.Player,
                        targetPlayer.Camp,
                        targetPlayer.FightMode,
                        targetPlayer.PkState);

                    if (rel != EntityRelationKind.Enemy)
                    {
                        continue;
                    }

                    int dist = SixtyFourDirectionTrigTable.GetDistance(
                        slot.CurrentMpsX,
                        slot.CurrentMpsY,
                        targetPlayer.CurrentMpsX,
                        targetPlayer.CurrentMpsY);

                    if (dist <= slot.HitRadiusMps)
                    {
                        slot.RecordTargetHit(targetPlayer.RuntimeNpcId);
                        SkillDamagePayload payload = slot.DamagePayload;

                        if (CombatDamageCalculator.CheckHitTarget(
                            payload.UseAttackRating,
                            payload.AttackRatingScore,
                            targetPlayer.EffectiveDefense,
                            _random))
                        {
                            int dmg = CombatDamageCalculator.ResolveHitAndApplyDamage(
                                slot.LauncherNpcId,
                                isPvp: slot.IsLauncherPlayer,
                                in payload,
                                targetPlayer.Series,
                                targetPlayer.PhysicsResistPercent,
                                targetPlayer.ColdResistPercent,
                                targetPlayer.FireResistPercent,
                                targetPlayer.LightResistPercent,
                                targetPlayer.PoisonResistPercent,
                                targetPlayer.StatusEffects,
                                _random);

                            onPlayerHit?.Invoke(slot, targetPlayer, dmg);
                        }

                        slot.RemainingPierceCount--;
                        if (slot.CollideVanish && slot.RemainingPierceCount <= 0)
                        {
                            RecycleSlot(slot, idx);
                            vanished = true;
                        }
                    }
                }
            }
        }
    }

    private bool TryAllocateSlot(
        uint launcherNpcId,
        bool isLauncherPlayer,
        EntityCamp launcherCamp,
        PlayerPkMode launcherPkMode,
        int startMpsX,
        int startMpsY,
        byte dirIndex,
        uint followTargetNpcId,
        SkillTemplate skill,
        MissileTemplate missileTemplate,
        in SkillDamagePayload damagePayload)
    {
        if (_freeSlotIndices.Count == 0)
        {
            return false;
        }

        int slotIndex = _freeSlotIndices.Pop();
        ActiveMissileSlot slot = _pool[slotIndex];
        slot.Reset();
        slot.IsActive = true;
        slot.LauncherNpcId = launcherNpcId;
        slot.IsLauncherPlayer = isLauncherPlayer;
        slot.LauncherCamp = launcherCamp;
        slot.LauncherPkMode = launcherPkMode;
        slot.SkillId = skill.Id;
        slot.SkillLevel = damagePayload.SkillLevel;
        slot.MissileTemplateId = missileTemplate.Id;
        slot.MoveKind = missileTemplate.MoveKind;
        slot.DirIndex = (byte)(dirIndex & 63);
        slot.CurrentMpsX = startMpsX;
        slot.CurrentMpsY = startMpsY;
        slot.CurrentZ = missileTemplate.HeightMps;
        slot.FixedX = (long)startMpsX << SixtyFourDirectionTrigTable.FixedPointScaleShift;
        slot.FixedY = (long)startMpsY << SixtyFourDirectionTrigTable.FixedPointScaleShift;
        slot.SpeedMpsPerFrame = Math.Max(0, missileTemplate.SpeedMpsPerFrame);
        slot.CurrentLifeFrame = 0;
        slot.MaxLifeFrames = Math.Max(1, missileTemplate.LifeFrames);
        slot.HitRadiusMps = Math.Max(32, missileTemplate.HitRadiusMps);
        slot.DamageRadiusMps = Math.Max(32, missileTemplate.DamageRadiusCells * WorldCoordinateConsts.CellWidthMps);
        slot.IsRangeDamage = missileTemplate.IsRangeDamage;
        slot.RemainingPierceCount = Math.Max(1, missileTemplate.PierceCount);
        slot.CollideVanish = missileTemplate.CollideVanish;
        slot.AutoExplode = missileTemplate.AutoExplode;
        slot.SubMissileId = missileTemplate.SubMissileId;
        slot.FollowTargetNpcId = followTargetNpcId;
        slot.OrbitCenterMpsX = startMpsX;
        slot.OrbitCenterMpsY = startMpsY;
        slot.OrbitRadiusMps = missileTemplate.Param1 > 0 ? missileTemplate.Param1 : 96;
        slot.DamagePayload = damagePayload;

        _activeSlotIndices.Add(slotIndex);
        return true;
    }

    private static void StepTrajectory(
        ActiveMissileSlot slot,
        IReadOnlyDictionary<uint, PlayerZoneState> playersByNpcId,
        IReadOnlyDictionary<uint, NpcZoneState> npcsById)
    {
        if (slot.MoveKind == MissileMoveKind.Stand || slot.SpeedMpsPerFrame <= 0)
        {
            return;
        }

        if (slot.MoveKind == MissileMoveKind.Follow && slot.FollowTargetNpcId != 0)
        {
            if (npcsById.TryGetValue(slot.FollowTargetNpcId, out NpcZoneState? targetNpc) && targetNpc.CurLife > 0)
            {
                int newDir = SixtyFourDirectionTrigTable.GetDirectionIndex(
                    slot.CurrentMpsX,
                    slot.CurrentMpsY,
                    targetNpc.MpsX,
                    targetNpc.MpsY);
                if (newDir >= 0)
                {
                    slot.DirIndex = (byte)(newDir & 63);
                }
            }
            else if (playersByNpcId.TryGetValue(slot.FollowTargetNpcId, out PlayerZoneState? targetPlayer) && targetPlayer.CurrentLife > 0)
            {
                int newDir = SixtyFourDirectionTrigTable.GetDirectionIndex(
                    slot.CurrentMpsX,
                    slot.CurrentMpsY,
                    targetPlayer.CurrentMpsX,
                    targetPlayer.CurrentMpsY);
                if (newDir >= 0)
                {
                    slot.DirIndex = (byte)(newDir & 63);
                }
            }
        }
        else if (slot.MoveKind is MissileMoveKind.Circle or MissileMoveKind.Spiral)
        {
            slot.DirIndex = (byte)((slot.DirIndex + 2) & 63);
            int radius = slot.MoveKind == MissileMoveKind.Spiral
                ? slot.OrbitRadiusMps + (slot.CurrentLifeFrame * 4)
                : slot.OrbitRadiusMps;
            int cos = SixtyFourDirectionTrigTable.DirCos(slot.DirIndex, 64);
            int sin = SixtyFourDirectionTrigTable.DirSin(slot.DirIndex, 64);
            slot.CurrentMpsX = slot.OrbitCenterMpsX + ((radius * cos) >> 10);
            slot.CurrentMpsY = slot.OrbitCenterMpsY + ((radius * sin) >> 10);
            slot.FixedX = (long)slot.CurrentMpsX << 10;
            slot.FixedY = (long)slot.CurrentMpsY << 10;
            return;
        }

        int dirCos = SixtyFourDirectionTrigTable.DirCos(slot.DirIndex, 64);
        int dirSin = SixtyFourDirectionTrigTable.DirSin(slot.DirIndex, 64);

        slot.FixedX += (long)slot.SpeedMpsPerFrame * dirCos;
        slot.FixedY += (long)slot.SpeedMpsPerFrame * dirSin;
        slot.CurrentMpsX = (int)(slot.FixedX >> SixtyFourDirectionTrigTable.FixedPointScaleShift);
        slot.CurrentMpsY = (int)(slot.FixedY >> SixtyFourDirectionTrigTable.FixedPointScaleShift);
    }

    private void RecycleSlot(ActiveMissileSlot slot, int activeListIdx)
    {
        int slotIdx = slot.SlotIndex;
        slot.Reset();
        RemoveActiveAt(activeListIdx);
        _freeSlotIndices.Push(slotIdx);
    }

    private void RemoveActiveAt(int activeListIdx)
    {
        int lastIdx = _activeSlotIndices.Count - 1;
        if (activeListIdx < lastIdx)
        {
            _activeSlotIndices[activeListIdx] = _activeSlotIndices[lastIdx];
        }

        _activeSlotIndices.RemoveAt(lastIdx);
    }
}
