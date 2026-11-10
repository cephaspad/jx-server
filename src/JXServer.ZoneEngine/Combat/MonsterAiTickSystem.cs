using System;
using System.Collections.Generic;
using JXServer.GameAssets.NpcTemplates;
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
/// Deterministic 18-Hz Monster AI state machine tick system (<c>IdlePatrol</c> -&gt; <c>Pursuit</c> -&gt; <c>Attack</c> -&gt; <c>LeashReturn</c> -&gt; <c>DeadRespawnWait</c>) (`FR-006`).
/// </summary>
public sealed class MonsterAiTickSystem
{
    private readonly SpatialRegionSlot[] _aoiSlots = new SpatialRegionSlot[9];
    private readonly Random _random;

    public MonsterAiTickSystem(int? randomSeed = null)
    {
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
    }

    public void StepTick(
        long currentTick,
        ushort mapId,
        IMapCatalog? mapCatalog,
        ISkillTemplateCatalog? skillCatalog,
        IMissileTemplateCatalog? missileCatalog,
        MissileSimulationTickSystem missileSystem,
        SpatialRegionGrid regionGrid,
        IReadOnlyDictionary<uint, PlayerZoneState> playersByNpcId,
        IReadOnlyDictionary<uint, NpcZoneState> npcsById,
        Action<NpcZoneState>? onNpcRespawned = null,
        Action<NpcZoneState>? onNpcMoved = null,
        Action<NpcZoneState, PlayerZoneState, int, int, int>? onNpcCastSkill = null,
        Action<uint, PlayerZoneState, int, bool>? onApplyDamageToPlayer = null)
    {
        ArgumentNullException.ThrowIfNull(missileSystem);
        ArgumentNullException.ThrowIfNull(regionGrid);
        ArgumentNullException.ThrowIfNull(playersByNpcId);
        ArgumentNullException.ThrowIfNull(npcsById);

        foreach (NpcZoneState npc in npcsById.Values)
        {
            if (npc.Kind == EntityKind.DialogNpc)
            {
                continue;
            }

            if (npc.AiState == MonsterAiStateKind.DeadRespawnWait || npc.CurLife == 0)
            {
                if (currentTick >= npc.RespawnReadyTick)
                {
                    RespawnMonster(npc, regionGrid, onNpcRespawned);
                }

                continue;
            }

            if (npc.StatusEffects.IsStunned || npc.StatusEffects.HurtRecoveryRemainingFrames > 0)
            {
                continue;
            }

            switch (npc.AiState)
            {
                case MonsterAiStateKind.IdlePatrol:
                    StepIdlePatrol(currentTick, mapId, mapCatalog, regionGrid, npc, onNpcMoved);
                    break;

                case MonsterAiStateKind.Pursuit:
                    StepPursuit(
                        currentTick,
                        mapId,
                        mapCatalog,
                        regionGrid,
                        playersByNpcId,
                        npc,
                        onNpcMoved);
                    break;

                case MonsterAiStateKind.Attack:
                    StepAttack(
                        currentTick,
                        skillCatalog,
                        missileCatalog,
                        missileSystem,
                        playersByNpcId,
                        npc,
                        onNpcCastSkill,
                        onApplyDamageToPlayer);
                    break;

                case MonsterAiStateKind.LeashReturn:
                    StepLeashReturn(mapId, mapCatalog, regionGrid, npc, onNpcMoved);
                    break;
            }
        }
    }

    private void StepIdlePatrol(
        long currentTick,
        ushort mapId,
        IMapCatalog? mapCatalog,
        SpatialRegionGrid regionGrid,
        NpcZoneState npc,
        Action<NpcZoneState>? onNpcMoved)
    {
        // 1. Scan 3x3 regions for hostile players within VisionRadiusMps
        PlayerZoneState? target = FindNearestHostilePlayerInVision(regionGrid, npc);
        if (target is not null)
        {
            npc.AggroTargetNpcId = target.RuntimeNpcId;
            npc.AiState = MonsterAiStateKind.Pursuit;
            return;
        }

        // 2. Step ongoing patrol walk or pick a new random destination within ActiveRadiusMps
        if (npc.Doing == EntityDoingPosture.Walk)
        {
            if (StepNpcTowardsTarget(mapId, mapCatalog, regionGrid, npc, npc.TargetMpsX, npc.TargetMpsY, npc.EffectiveWalkSpeed))
            {
                onNpcMoved?.Invoke(npc);
            }

            if (npc.MpsX == npc.TargetMpsX && npc.MpsY == npc.TargetMpsY)
            {
                npc.Doing = EntityDoingPosture.Stand;
                npc.NextAiActionTick = currentTick + _random.Next(36, 90);
            }

            return;
        }

        if (currentTick < npc.NextAiActionTick)
        {
            return;
        }

        int maxOffset = Math.Max(32, Math.Min(160, npc.ActiveRadiusMps / 2));
        int destX = npc.SpawnMpsX + _random.Next(-maxOffset, maxOffset + 1);
        int destY = npc.SpawnMpsY + _random.Next(-maxOffset, maxOffset + 1);
        if (destX > 0 && destY > 0 && !IsNpcCellBlocked(mapId, mapCatalog, destX, destY))
        {
            npc.TargetMpsX = destX;
            npc.TargetMpsY = destY;
            npc.Doing = EntityDoingPosture.Walk;
        }
        else
        {
            npc.NextAiActionTick = currentTick + 36;
        }
    }

    private void StepPursuit(
        long currentTick,
        ushort mapId,
        IMapCatalog? mapCatalog,
        SpatialRegionGrid regionGrid,
        IReadOnlyDictionary<uint, PlayerZoneState> playersByNpcId,
        NpcZoneState npc,
        Action<NpcZoneState>? onNpcMoved)
    {
        if (!TryGetValidAggroTarget(playersByNpcId, npc, out PlayerZoneState? target) || target is null)
        {
            BeginLeashReturn(npc);
            return;
        }

        int distFromSpawn = SixtyFourDirectionTrigTable.GetDistance(npc.SpawnMpsX, npc.SpawnMpsY, npc.MpsX, npc.MpsY);
        int targetDistFromSpawn = SixtyFourDirectionTrigTable.GetDistance(npc.SpawnMpsX, npc.SpawnMpsY, target.CurrentMpsX, target.CurrentMpsY);
        if (distFromSpawn > npc.PursuitRadiusMps || targetDistFromSpawn > npc.PursuitRadiusMps)
        {
            BeginLeashReturn(npc);
            return;
        }

        int distToTarget = SixtyFourDirectionTrigTable.GetDistance(npc.MpsX, npc.MpsY, target.CurrentMpsX, target.CurrentMpsY);
        if (distToTarget <= npc.AttackRadiusMps)
        {
            npc.Doing = EntityDoingPosture.Stand;
            npc.TargetMpsX = npc.MpsX;
            npc.TargetMpsY = npc.MpsY;
            npc.AiState = MonsterAiStateKind.Attack;
            return;
        }

        npc.Doing = EntityDoingPosture.Walk;
        npc.TargetMpsX = target.CurrentMpsX;
        npc.TargetMpsY = target.CurrentMpsY;

        if (StepNpcTowardsTarget(mapId, mapCatalog, regionGrid, npc, target.CurrentMpsX, target.CurrentMpsY, npc.EffectiveRunSpeed))
        {
            onNpcMoved?.Invoke(npc);
        }

        _ = currentTick;
    }

    private void StepAttack(
        long currentTick,
        ISkillTemplateCatalog? skillCatalog,
        IMissileTemplateCatalog? missileCatalog,
        MissileSimulationTickSystem missileSystem,
        IReadOnlyDictionary<uint, PlayerZoneState> playersByNpcId,
        NpcZoneState npc,
        Action<NpcZoneState, PlayerZoneState, int, int, int>? onNpcCastSkill,
        Action<uint, PlayerZoneState, int, bool>? onApplyDamageToPlayer)
    {
        if (!TryGetValidAggroTarget(playersByNpcId, npc, out PlayerZoneState? target) || target is null)
        {
            BeginLeashReturn(npc);
            return;
        }

        int distToTarget = SixtyFourDirectionTrigTable.GetDistance(npc.MpsX, npc.MpsY, target.CurrentMpsX, target.CurrentMpsY);
        if (distToTarget > npc.AttackRadiusMps + 32)
        {
            npc.AiState = MonsterAiStateKind.Pursuit;
            return;
        }

        if (currentTick < npc.NextAttackReadyTick)
        {
            return;
        }

        npc.NextAttackReadyTick = currentTick + Math.Max(9, npc.AttackSpeedFrames);
        int dirIdx = SixtyFourDirectionTrigTable.GetDirectionIndex(npc.MpsX, npc.MpsY, target.CurrentMpsX, target.CurrentMpsY);
        if (dirIdx >= 0)
        {
            npc.Dir = (byte)(dirIdx & 63);
        }

        (int skillId, int skillLevel) = ChooseMonsterSkill(npc);
        SkillTemplate? skillTpl = null;
        skillCatalog?.TryGetSkill(skillId, out skillTpl);

        onNpcCastSkill?.Invoke(npc, target, skillId, skillLevel, (int)target.RuntimeNpcId);

        SkillDamagePayload payload = CombatDamageCalculator.BuildNpcSkillDamagePayload(npc, skillTpl, skillLevel);

        if (skillTpl is not null && skillTpl.MissileId > 0)
        {
            MissileTemplate? missileTpl = null;
            missileCatalog?.TryGetMissile(skillTpl.MissileId, out missileTpl);
            if (missileTpl is not null)
            {
                missileSystem.SpawnSkillMissiles(
                    npc.NpcId,
                    isLauncherPlayer: false,
                    npc.Camp,
                    PlayerPkMode.Fight,
                    npc.MpsX,
                    npc.MpsY,
                    target.CurrentMpsX,
                    target.CurrentMpsY,
                    target.RuntimeNpcId,
                    skillTpl,
                    missileTpl,
                    in payload);
                return;
            }
        }

        if (CombatDamageCalculator.CheckHitTarget(
            payload.UseAttackRating,
            payload.AttackRatingScore,
            target.EffectiveDefense,
            _random))
        {
            int dmg = CombatDamageCalculator.ResolveHitAndApplyDamage(
                npc.NpcId,
                isPvp: false,
                in payload,
                target.Series,
                target.PhysicsResistPercent,
                target.ColdResistPercent,
                target.FireResistPercent,
                target.LightResistPercent,
                target.PoisonResistPercent,
                target.StatusEffects,
                _random);

            onApplyDamageToPlayer?.Invoke(npc.NpcId, target, dmg, payload.DoHurtAnimation);
        }
    }

    private void StepLeashReturn(
        ushort mapId,
        IMapCatalog? mapCatalog,
        SpatialRegionGrid regionGrid,
        NpcZoneState npc,
        Action<NpcZoneState>? onNpcMoved)
    {
        if (npc.MpsX == npc.SpawnMpsX && npc.MpsY == npc.SpawnMpsY)
        {
            npc.CurLife = npc.MaxLife;
            npc.Doing = EntityDoingPosture.Stand;
            npc.AggroTargetNpcId = 0;
            npc.AiState = MonsterAiStateKind.IdlePatrol;
            onNpcMoved?.Invoke(npc);
            return;
        }

        npc.Doing = EntityDoingPosture.Run;
        npc.TargetMpsX = npc.SpawnMpsX;
        npc.TargetMpsY = npc.SpawnMpsY;

        if (StepNpcTowardsTarget(mapId, mapCatalog, regionGrid, npc, npc.SpawnMpsX, npc.SpawnMpsY, npc.EffectiveRunSpeed))
        {
            if (npc.MpsX == npc.SpawnMpsX && npc.MpsY == npc.SpawnMpsY)
            {
                npc.CurLife = npc.MaxLife;
                npc.Doing = EntityDoingPosture.Stand;
                npc.AggroTargetNpcId = 0;
                npc.AiState = MonsterAiStateKind.IdlePatrol;
            }

            onNpcMoved?.Invoke(npc);
        }
    }

    private static void RespawnMonster(
        NpcZoneState npc,
        SpatialRegionGrid regionGrid,
        Action<NpcZoneState>? onNpcRespawned)
    {
        MoveNpcAndUpdateRegion(regionGrid, npc, npc.SpawnMpsX, npc.SpawnMpsY);
        npc.TargetMpsX = npc.SpawnMpsX;
        npc.TargetMpsY = npc.SpawnMpsY;
        npc.CurLife = npc.MaxLife;
        npc.Doing = EntityDoingPosture.Stand;
        npc.AiState = MonsterAiStateKind.IdlePatrol;
        npc.AggroTargetNpcId = 0;
        npc.StatusEffects.ClearAll();

        onNpcRespawned?.Invoke(npc);
    }

    private static void BeginLeashReturn(NpcZoneState npc)
    {
        npc.AggroTargetNpcId = 0;
        npc.AiState = MonsterAiStateKind.LeashReturn;
        npc.TargetMpsX = npc.SpawnMpsX;
        npc.TargetMpsY = npc.SpawnMpsY;
    }

    private PlayerZoneState? FindNearestHostilePlayerInVision(SpatialRegionGrid regionGrid, NpcZoneState npc)
    {
        int regionCount = regionGrid.GetSurroundingRegions(npc.RegionX, npc.RegionY, _aoiSlots);
        PlayerZoneState? best = null;
        int bestDist = npc.VisionRadiusMps + 1;

        for (int i = 0; i < regionCount; i++)
        {
            List<PlayerZoneState> players = _aoiSlots[i].Players;
            for (int pIdx = 0; pIdx < players.Count; pIdx++)
            {
                PlayerZoneState candidate = players[pIdx];
                if (!candidate.IsSyncCompleted ||
                    candidate.CurrentLife <= 0 ||
                    candidate.Doing == EntityDoingPosture.Death ||
                    candidate.FightMode == 0)
                {
                    continue;
                }

                EntityRelationKind rel = CombatDamageCalculator.EvaluateRelation(
                    npc.NpcId,
                    npc.Kind,
                    npc.Camp,
                    attackerFightMode: 1,
                    PlayerPkMode.Fight,
                    candidate.RuntimeNpcId,
                    EntityKind.Player,
                    candidate.Camp,
                    candidate.FightMode,
                    candidate.PkState);

                if (rel != EntityRelationKind.Enemy)
                {
                    continue;
                }

                int dist = SixtyFourDirectionTrigTable.GetDistance(npc.MpsX, npc.MpsY, candidate.CurrentMpsX, candidate.CurrentMpsY);
                if (dist <= npc.VisionRadiusMps && dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }
        }

        return best;
    }

    private static bool TryGetValidAggroTarget(
        IReadOnlyDictionary<uint, PlayerZoneState> playersByNpcId,
        NpcZoneState npc,
        out PlayerZoneState? target)
    {
        if (npc.AggroTargetNpcId != 0 &&
            playersByNpcId.TryGetValue(npc.AggroTargetNpcId, out target) &&
            target is not null &&
            target.IsSyncCompleted &&
            target.CurrentLife > 0 &&
            target.Doing != EntityDoingPosture.Death &&
            target.FightMode != 0)
        {
            return true;
        }

        target = null;
        return false;
    }

    private (int SkillId, int SkillLevel) ChooseMonsterSkill(NpcZoneState npc)
    {
        if (npc.Skills is { Count: > 0 })
        {
            int totalRate = 0;
            for (int i = 0; i < npc.Skills.Count; i++)
            {
                totalRate += Math.Max(1, npc.Skills[i].RatePercent);
            }

            int roll = _random.Next(Math.Max(1, totalRate));
            int acc = 0;
            for (int i = 0; i < npc.Skills.Count; i++)
            {
                NpcSkillSlotTemplate slot = npc.Skills[i];
                acc += Math.Max(1, slot.RatePercent);
                if (roll < acc && slot.SkillId > 0)
                {
                    return (slot.SkillId, Math.Max(1, (int)slot.BaseLevel));
                }
            }

            NpcSkillSlotTemplate first = npc.Skills[0];
            if (first.SkillId > 0)
            {
                return (first.SkillId, Math.Max(1, (int)first.BaseLevel));
            }
        }

        return (29, Math.Max(1, (int)npc.Level));
    }

    private static bool StepNpcTowardsTarget(
        ushort mapId,
        IMapCatalog? mapCatalog,
        SpatialRegionGrid regionGrid,
        NpcZoneState npc,
        int targetX,
        int targetY,
        int stepSpeed)
    {
        long dx = (long)targetX - npc.MpsX;
        long dy = (long)targetY - npc.MpsY;
        double dist = Math.Sqrt((dx * dx) + (dy * dy));
        int nextX;
        int nextY;

        if (dist <= stepSpeed || dist < 1.0)
        {
            nextX = targetX;
            nextY = targetY;
        }
        else
        {
            double ratio = stepSpeed / dist;
            nextX = npc.MpsX + (int)Math.Round(dx * ratio);
            nextY = npc.MpsY + (int)Math.Round(dy * ratio);
        }

        if (IsNpcCellBlocked(mapId, mapCatalog, nextX, nextY))
        {
            npc.TargetMpsX = npc.MpsX;
            npc.TargetMpsY = npc.MpsY;
            npc.Doing = EntityDoingPosture.Stand;
            return false;
        }

        int dirIdx = SixtyFourDirectionTrigTable.GetDirectionIndex(npc.MpsX, npc.MpsY, nextX, nextY);
        if (dirIdx >= 0)
        {
            npc.Dir = (byte)(dirIdx & 63);
        }

        MoveNpcAndUpdateRegion(regionGrid, npc, nextX, nextY);
        return true;
    }

    private static void MoveNpcAndUpdateRegion(SpatialRegionGrid regionGrid, NpcZoneState npc, int nextMpsX, int nextMpsY)
    {
        int oldRx = npc.RegionX;
        int oldRy = npc.RegionY;

        npc.MpsX = nextMpsX;
        npc.MpsY = nextMpsY;

        int newRx = npc.RegionX;
        int newRy = npc.RegionY;
        if (oldRx == newRx && oldRy == newRy)
        {
            return;
        }

        if (regionGrid.TryGetRegion(oldRx, oldRy, out SpatialRegionSlot? oldSlot) && oldSlot is not null)
        {
            oldSlot.Npcs.Remove(npc);
        }

        SpatialRegionSlot newSlot = regionGrid.GetOrCreateRegion(newRx, newRy);
        if (!newSlot.Npcs.Contains(npc))
        {
            newSlot.Npcs.Add(npc);
        }
    }

    private static bool IsNpcCellBlocked(ushort mapId, IMapCatalog? mapCatalog, int mpsX, int mpsY)
    {
        if (mpsX < 0 || mpsY < 0)
        {
            return true;
        }

        if (mapCatalog is not null)
        {
            return mapCatalog.GetCellObstacle(mapId, mpsX, mpsY) == 1;
        }

        return false;
    }
}
