using System;
using System.Collections.Generic;
using JXServer.GameAssets.Items;
using JXServer.GameAssets.LootTables;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.NpcEntities;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.SpatialGrid;

namespace JXServer.ZoneEngine.Items;

/// <summary>
/// Evaluates <see cref="LootTableTemplate"/> drops on monster death, finds walkable drop coordinates,
/// expires uncollected <see cref="GroundObjectSlot"/> instances at <c>ExpireTick</c>, and ticks active medicine regeneration (`FR-010`, `FR-011`, `FR-013`).
/// </summary>
public sealed class GroundObjectTickSystem
{
    private static readonly (int DxCells, int DyCells)[] SpiralOffsets =
    [
        (0, 0),
        (1, 0), (0, 1), (-1, 0), (0, -1),
        (1, 1), (-1, 1), (-1, -1), (1, -1),
        (2, 0), (0, 2), (-2, 0), (0, -2),
        (2, 1), (1, 2), (-1, 2), (-2, 1), (-2, -1), (-1, -2), (1, -2), (2, -1),
    ];

    private readonly Dictionary<int, GroundObjectSlot> _objectsById = [];
    private readonly List<GroundObjectSlot> _expiredBuffer = new(32);
    private readonly Random _random = new();
    private int _nextObjId = 1;
    private uint _nextDroppedItemId = 500000;

    public int ActiveGroundObjectCount => _objectsById.Count;

    public IReadOnlyDictionary<int, GroundObjectSlot> ObjectsById => _objectsById;

    public bool TryGetGroundObject(int objId, out GroundObjectSlot? slot) =>
        _objectsById.TryGetValue(objId, out slot);

    public bool TryRemoveGroundObject(int objId, SpatialRegionGrid regionGrid, out GroundObjectSlot? removed)
    {
        if (!_objectsById.Remove(objId, out removed) || removed is null)
        {
            return false;
        }

        if (regionGrid.TryGetRegion(removed.RegionX, removed.RegionY, out SpatialRegionSlot? region) && region is not null)
        {
            region.GroundObjects.Remove(removed);
        }

        return true;
    }

    public GroundObjectSlot SpawnItemGroundObject(
        ushort mapId,
        IMapCatalog? mapCatalog,
        SpatialRegionGrid regionGrid,
        PlayerItemSlot itemPayload,
        int centerMpsX,
        int centerMpsY,
        Guid? ownerCharacterId,
        long currentTick,
        int spiralIndex = 0)
    {
        (int dropX, int dropY) = FindWalkableDropCoordinate(mapId, mapCatalog, centerMpsX, centerMpsY, spiralIndex);
        int objId = AllocateObjId();
        int dataId = itemPayload.Template.ObjDataId > 0 ? itemPayload.Template.ObjDataId : 4;
        long protExpiry = ownerCharacterId.HasValue
            ? currentTick + ItemConsts.DefaultLootOwnerProtectionFrames
            : 0L;
        long expireTick = currentTick + ItemConsts.DefaultGroundObjectLifeFrames;

        var obj = new GroundObjectSlot(
            objId: objId,
            objDataId: dataId,
            kind: GroundObjectKind.Item,
            displayNameUtf8: itemPayload.Template.Name,
            mapId: mapId,
            mpsX: dropX,
            mpsY: dropY,
            dirIndex: (byte)(_random.Next(64) & 63),
            moneyAmount: 0,
            itemPayload: itemPayload,
            colorId: itemPayload.ColorId,
            ownerCharacterId: ownerCharacterId,
            ownerProtectionExpiryTick: protExpiry,
            expireTick: expireTick);

        _objectsById[objId] = obj;
        SpatialRegionSlot region = regionGrid.GetOrCreateRegion(obj.RegionX, obj.RegionY);
        region.GroundObjects.Add(obj);
        return obj;
    }

    public GroundObjectSlot SpawnMoneyGroundObject(
        ushort mapId,
        IMapCatalog? mapCatalog,
        SpatialRegionGrid regionGrid,
        int moneyAmount,
        int centerMpsX,
        int centerMpsY,
        Guid? ownerCharacterId,
        long currentTick,
        int spiralIndex = 0)
    {
        (int dropX, int dropY) = FindWalkableDropCoordinate(mapId, mapCatalog, centerMpsX, centerMpsY, spiralIndex);
        int objId = AllocateObjId();
        long protExpiry = ownerCharacterId.HasValue
            ? currentTick + ItemConsts.DefaultLootOwnerProtectionFrames
            : 0L;
        long expireTick = currentTick + ItemConsts.DefaultGroundObjectLifeFrames;

        var obj = new GroundObjectSlot(
            objId: objId,
            objDataId: 1,
            kind: GroundObjectKind.Money,
            displayNameUtf8: $"{moneyAmount} Lượng",
            mapId: mapId,
            mpsX: dropX,
            mpsY: dropY,
            dirIndex: 0,
            moneyAmount: Math.Max(1, moneyAmount),
            itemPayload: null,
            colorId: 0,
            ownerCharacterId: ownerCharacterId,
            ownerProtectionExpiryTick: protExpiry,
            expireTick: expireTick);

        _objectsById[objId] = obj;
        SpatialRegionSlot region = regionGrid.GetOrCreateRegion(obj.RegionX, obj.RegionY);
        region.GroundObjects.Add(obj);
        return obj;
    }

    public List<GroundObjectSlot> EvaluateAndSpawnMonsterLoot(
        ushort mapId,
        IMapCatalog? mapCatalog,
        ILootTableCatalog? lootCatalog,
        ItemGenerator? itemGenerator,
        SpatialRegionGrid regionGrid,
        NpcZoneState slainMonster,
        PlayerZoneState? killerPlayer,
        long currentTick)
    {
        var spawned = new List<GroundObjectSlot>(4);
        LootTableTemplate? lootTable = null;
        if (lootCatalog is null ||
            !lootCatalog.TryGetLootTable(slainMonster.LootTableCode, out lootTable) ||
            lootTable is null)
        {
            lootCatalog?.TryGetLootTable("default-monster-loot", out lootTable);
        }

        Guid? ownerId = killerPlayer?.CharacterId;
        int killerLuck = killerPlayer?.Lucky ?? 0;
        int spiralIdx = 0;

        // 1. Evaluate money drop
        double moneyChance = lootTable?.MoneyDropChance ?? 0.5;
        int minGold = lootTable?.MinMoneyGold ?? 10;
        int maxGold = Math.Max(minGold, lootTable?.MaxMoneyGold ?? 50);

        if (_random.NextDouble() <= moneyChance)
        {
            int gold = _random.Next(minGold, maxGold + 1) * Math.Max(1, (int)slainMonster.Level);
            GroundObjectSlot moneyObj = SpawnMoneyGroundObject(
                mapId,
                mapCatalog,
                regionGrid,
                gold,
                slainMonster.MpsX,
                slainMonster.MpsY,
                ownerId,
                currentTick,
                spiralIdx++);
            spawned.Add(moneyObj);
        }

        if (itemGenerator is null || lootTable is null || lootTable.WeightedPools.Count == 0)
        {
            return spawned;
        }

        int dropRolls = Math.Max(1, Math.Min(slainMonster.TreasureDropCount, lootTable.MaxDropsPerKill));
        long totalPoolWeight = 0;
        for (int i = 0; i < lootTable.WeightedPools.Count; i++)
        {
            totalPoolWeight += Math.Max(0, lootTable.WeightedPools[i].Weight);
        }

        int randRange = Math.Max((int)totalPoolWeight, lootTable.RandRange);
        if (randRange <= 0)
        {
            return spawned;
        }

        for (int r = 0; r < dropRolls; r++)
        {
            long roll = _random.NextInt64(randRange);
            long acc = 0;
            WeightedLootPoolEntryTemplate? chosenPool = null;

            for (int i = 0; i < lootTable.WeightedPools.Count; i++)
            {
                WeightedLootPoolEntryTemplate entry = lootTable.WeightedPools[i];
                acc += Math.Max(0, entry.Weight);
                if (roll < acc)
                {
                    chosenPool = entry;
                    break;
                }
            }

            if (chosenPool is null)
            {
                continue;
            }

            GeneratedItemData generated = GenerateDropFromPoolEntry(
                itemGenerator,
                lootTable,
                chosenPool,
                slainMonster,
                killerLuck);

            uint runtimeItemId = _nextDroppedItemId++;
            var itemSlot = PlayerItemSlot.FromGeneratedItem(runtimeItemId, generated);
            GroundObjectSlot itemObj = SpawnItemGroundObject(
                mapId,
                mapCatalog,
                regionGrid,
                itemSlot,
                slainMonster.MpsX,
                slainMonster.MpsY,
                ownerId,
                currentTick,
                spiralIdx++);
            spawned.Add(itemObj);
        }

        return spawned;
    }

    public void StepTick(
        long currentTick,
        SpatialRegionGrid regionGrid,
        IReadOnlyDictionary<Guid, PlayerZoneState> playersByCharacterId,
        Action<GroundObjectSlot, byte>? onGroundObjectRemoved)
    {
        // 1. Expire ground objects whose ExpireTick <= currentTick
        if (_objectsById.Count > 0)
        {
            _expiredBuffer.Clear();
            foreach (GroundObjectSlot obj in _objectsById.Values)
            {
                if (currentTick >= obj.ExpireTick)
                {
                    _expiredBuffer.Add(obj);
                }
            }

            for (int i = 0; i < _expiredBuffer.Count; i++)
            {
                GroundObjectSlot expired = _expiredBuffer[i];
                if (TryRemoveGroundObject(expired.ObjId, regionGrid, out _))
                {
                    onGroundObjectRemoved?.Invoke(expired, 0);
                }
            }
        }

        // 2. Step gradual medicine HP/MP regeneration on players (applied every 9 frames = 0.5s)
        if ((currentTick % 9) != 0)
        {
            return;
        }

        foreach (PlayerZoneState player in playersByCharacterId.Values)
        {
            if (player.CurrentLife <= 0 || player.Doing == EntityDoingPosture.Death)
            {
                player.MedicineLifeRemainingFrames = 0;
                player.MedicineManaRemainingFrames = 0;
                continue;
            }

            bool vitalChanged = false;
            if (player.MedicineLifeRemainingFrames > 0)
            {
                int framesToConsume = Math.Min(9, player.MedicineLifeRemainingFrames);
                player.MedicineLifeRemainingFrames -= framesToConsume;
                int heal = Math.Max(1, player.MedicineLifeRegenPerTick);
                int nextLife = Math.Min(player.MaxLife, player.CurrentLife + heal);
                if (nextLife != player.CurrentLife)
                {
                    player.CurrentLife = nextLife;
                    vitalChanged = true;
                }
            }

            if (player.MedicineManaRemainingFrames > 0)
            {
                int framesToConsume = Math.Min(9, player.MedicineManaRemainingFrames);
                player.MedicineManaRemainingFrames -= framesToConsume;
                int manaAdd = Math.Max(1, player.MedicineManaRegenPerTick);
                int nextMana = Math.Min(player.MaxMana, player.CurrentMana + manaAdd);
                if (nextMana != player.CurrentMana)
                {
                    player.CurrentMana = nextMana;
                    vitalChanged = true;
                }
            }

            if (vitalChanged)
            {
                player.OutboundSink.SendNormalVitalSync(player);
            }
        }
    }

    private GeneratedItemData GenerateDropFromPoolEntry(
        ItemGenerator itemGenerator,
        LootTableTemplate lootTable,
        WeightedLootPoolEntryTemplate entry,
        NpcZoneState slainMonster,
        int killerLuck)
    {
        bool rollBlue = ShouldRollBlueAffixes(lootTable, entry, killerLuck);
        byte minLvl = Math.Max((byte)1, entry.MinLevel > 0 ? entry.MinLevel : lootTable.MinItemLevel);
        byte maxLvl = Math.Max(minLvl, entry.MaxLevel > 0 ? entry.MaxLevel : lootTable.MaxItemLevel);
        byte itemLevel = (byte)_random.Next(minLvl, maxLvl + 1);
        ElementSeries series = Enum.IsDefined(entry.ElementSeries)
            ? entry.ElementSeries
            : (ElementSeries)_random.Next(0, 5);

        if (!string.IsNullOrWhiteSpace(entry.ItemCode))
        {
            return itemGenerator.GenerateFromItemCode(
                entry.ItemCode,
                seriesOverride: series,
                levelOverride: itemLevel,
                luck: killerLuck,
                rollBlueAffixes: rollBlue);
        }

        ItemGenreKind genre = entry.Genre ?? ItemGenreKind.Equipment;
        int detail = entry.DetailType ?? 0;
        int particular = entry.ParticularType ?? 0;

        if (genre == ItemGenreKind.Equipment)
        {
            int[] thresholds = rollBlue
                ? BuildMagicThresholds(itemLevel, killerLuck)
                : [];
            return itemGenerator.GenerateEquipment(
                detail,
                particular,
                series,
                itemLevel,
                thresholds,
                killerLuck);
        }

        if (genre == ItemGenreKind.TownPortal)
        {
            return itemGenerator.GenerateTownPortal(Math.Max(1, particular));
        }

        return itemGenerator.GenerateMedicine(detail, Math.Max(1, particular), itemLevel);
    }

    private bool ShouldRollBlueAffixes(
        LootTableTemplate lootTable,
        WeightedLootPoolEntryTemplate entry,
        int killerLuck)
    {
        if (entry.Genre.HasValue && entry.Genre.Value != ItemGenreKind.Equipment)
        {
            return false;
        }

        int magicRate = Math.Max(0, lootTable.MagicRate);
        if (magicRate <= 0)
        {
            return true;
        }

        double luckMultiplier = 1.0 + (Math.Max(0, killerLuck) * Math.Max(0.01, lootTable.BlueAffixLuckFactor));
        int effectiveRate = (int)Math.Round(magicRate * luckMultiplier);
        int range = Math.Max(100, lootTable.RandRange > 0 ? Math.Min(10000, lootTable.RandRange) : 1000);
        return _random.Next(range) < Math.Max(range / 2, effectiveRate);
    }

    private int[] BuildMagicThresholds(byte itemLevel, int killerLuck)
    {
        var thresholds = new int[ItemConsts.MaxMagicAffixSlots];
        int baseMax = Math.Clamp(itemLevel + (killerLuck / 10), 1, 10);
        int activePairs = _random.Next(1, 4); // 1..3 pairs (2, 4, or 6 affixes)
        if (killerLuck >= 20)
        {
            activePairs = 3;
        }

        for (int i = 0; i < activePairs * 2 && i < thresholds.Length; i++)
        {
            thresholds[i] = baseMax;
        }

        return thresholds;
    }

    private static (int DropMpsX, int DropMpsY) FindWalkableDropCoordinate(
        ushort mapId,
        IMapCatalog? mapCatalog,
        int centerMpsX,
        int centerMpsY,
        int spiralStartIndex)
    {
        for (int i = 0; i < SpiralOffsets.Length; i++)
        {
            (int dxCells, int dyCells) = SpiralOffsets[(spiralStartIndex + i) % SpiralOffsets.Length];
            int candX = Math.Max(32, centerMpsX + (dxCells * WorldCoordinateConsts.CellWidthMps));
            int candY = Math.Max(32, centerMpsY + (dyCells * WorldCoordinateConsts.CellHeightMps));

            if (mapCatalog is null || mapCatalog.GetCellObstacle(mapId, candX, candY) == 0)
            {
                return (candX, candY);
            }
        }

        return (Math.Max(32, centerMpsX), Math.Max(32, centerMpsY));
    }

    private int AllocateObjId()
    {
        int id = _nextObjId++;
        if (id <= 0)
        {
            _nextObjId = 1;
            id = _nextObjId++;
        }

        return id;
    }
}
