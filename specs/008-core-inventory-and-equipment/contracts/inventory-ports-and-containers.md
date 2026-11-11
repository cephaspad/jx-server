# Contract: Internal Inventory Ports & Container Interfaces

**Feature Branch**: `008-core-inventory-and-equipment` | **Date**: 2026-11-11

---

## 1. Item Template Catalog Port (`JXServer.GameAssets.Items`)

```csharp
namespace JXServer.GameAssets.Items;

public interface IItemTemplateCatalog
{
    bool TryGetItemByCode(string code, out ItemTemplate? item);

    bool TryGetItem(
        ItemGenreKind genre,
        int detailType,
        int particularType,
        byte level,
        out ItemTemplate? item);
}
```

---

## 2. Inventory Domain Outbound Sink Port (`JXServer.ZoneEngine.PlayerSessions`)

```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public interface IInventoryOutboundSink
{
    void SendItemSync(in PlayerItemSyncSnapshot snapshot);

    void SendItemRemove(uint runtimeItemId);

    void SendMoneySync(int inventoryMoney, int repositoryMoney, int tradeMoney = 0);

    void SendPlayerMoveItemConfirm(
        byte downY,
        ItemContainerPlace downPos,
        byte downX,
        byte upY,
        byte upX,
        ItemContainerPlace upPos);

    void SendItemAutoMoveUnlock();
}
```

---

## 3. Inventory Zone Commands (`JXServer.ZoneEngine.Items`)

```csharp
namespace JXServer.ZoneEngine.Items;

public sealed record MoveInventoryItemZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    byte DownY,
    ItemContainerPlace DownPos,
    byte DownX,
    byte UpY,
    byte UpX,
    ItemContainerPlace UpPos) : IZoneCommand;

public sealed record DivideInventoryItemZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    uint SourceItemId,
    ushort SplitCount,
    ItemContainerPlace SourcePlace,
    byte SourceGridX,
    byte SourceGridY) : IZoneCommand;

public sealed record UseInventoryItemZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    uint RuntimeItemId,
    byte GridX,
    byte GridY,
    ItemContainerPlace Place) : IZoneCommand;

public sealed record DropInventoryItemZoneCommand(
    Guid CharacterId,
    long LeaseEpoch) : IZoneCommand;
```
