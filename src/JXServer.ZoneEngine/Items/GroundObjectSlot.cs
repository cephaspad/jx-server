using System;
using JXServer.Primitives.Items;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.Items;

/// <summary>
/// Live ground object residing in <c>SpatialRegionSlot.GroundObjects</c> (`FR-011`).
/// </summary>
public sealed class GroundObjectSlot
{
    public int ObjId { get; }

    public int ObjDataId { get; }

    public GroundObjectKind Kind { get; }

    public string DisplayNameUtf8 { get; }

    public ushort MapId { get; }

    public int MpsX { get; }

    public int MpsY { get; }

    public int RegionX => MpsX / WorldCoordinateConsts.RegionWidthMps;

    public int RegionY => MpsY / WorldCoordinateConsts.RegionHeightMps;

    public byte DirIndex { get; }

    public int MoneyAmount { get; }

    public PlayerItemSlot? ItemPayload { get; }

    public byte ColorId { get; }

    public Guid? OwnerCharacterId { get; }

    public long OwnerProtectionExpiryTick { get; }

    public long ExpireTick { get; }

    public GroundObjectSlot(
        int objId,
        int objDataId,
        GroundObjectKind kind,
        string displayNameUtf8,
        ushort mapId,
        int mpsX,
        int mpsY,
        byte dirIndex,
        int moneyAmount,
        PlayerItemSlot? itemPayload,
        byte colorId,
        Guid? ownerCharacterId,
        long ownerProtectionExpiryTick,
        long expireTick)
    {
        ObjId = objId;
        ObjDataId = objDataId;
        Kind = kind;
        DisplayNameUtf8 = displayNameUtf8 ?? string.Empty;
        MapId = mapId;
        MpsX = mpsX;
        MpsY = mpsY;
        DirIndex = dirIndex;
        MoneyAmount = moneyAmount;
        ItemPayload = itemPayload;
        ColorId = colorId;
        OwnerCharacterId = ownerCharacterId;
        OwnerProtectionExpiryTick = ownerProtectionExpiryTick;
        ExpireTick = expireTick;
    }

    public bool CanCharacterPickup(Guid characterId, long currentTick) =>
        !OwnerCharacterId.HasValue ||
        OwnerCharacterId.Value == characterId ||
        currentTick >= OwnerProtectionExpiryTick;

    public GroundObjectSyncSnapshot CreateSyncSnapshot() =>
        new(
            ObjId: ObjId,
            DataId: ObjDataId,
            Dir: DirIndex,
            MpsX: MpsX,
            MpsY: MpsY,
            MoneyNum: MoneyAmount,
            ItemId: ItemPayload is not null ? (int)ItemPayload.RuntimeItemId : 0,
            ItemWidth: ItemPayload?.Template.GridWidthCells ?? 1,
            Genre: (ushort)(ItemPayload?.Genre ?? ItemGenreKind.Equipment),
            ItemHeight: ItemPayload?.Template.GridHeightCells ?? 1,
            ColorId: ColorId,
            NameUtf8: DisplayNameUtf8);
}
