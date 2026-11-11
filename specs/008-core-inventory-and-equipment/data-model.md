# Data Model: Spatial Inventory & Equipment Grid

**Feature Branch**: `008-core-inventory-and-equipment` | **Date**: 2026-11-11

---

## 1. Domain Enums & Constants (`JXServer.Primitives.Items`)

```csharp
namespace JXServer.Primitives.Items;

public enum ItemContainerPlace : byte
{
    None = 0,
    Hand = 1,           // Cursor grab
    Equip = 2,          // Paper doll equipment slots
    EquipRoom = 3,      // 6x10 main inventory bag
    RepositoryRoom = 4, // Storage box
    Trade = 5,          // Player trade window
    Shop = 6,           // NPC buy/sell buffer
    Immediacy = 7,      // Quick-access belt slots
    Give = 8,           // Quest turn-in slot
}

public enum EquipmentPartKind : byte
{
    Head = 0,
    Body = 1,
    Belt = 2,
    Weapon = 3,
    Foot = 4,
    Cuff = 5,
    Amulet = 6,
    Ring1 = 7,
    Ring2 = 8,
    Pendant = 9,
    Horse = 10,
    Mask = 11,
    Mantle = 12,
    Signet = 13,
    Badge = 14,
}

public static class ItemConsts
{
    public const int EquipRoomWidth = 6;
    public const int EquipRoomHeight = 10;
    public const int MaxEquipParts = 15;
    public const int ImmediacySlots = 3;
    public const int GeneratorVersion = 4;
    public const byte MaxEnchaseLevel = 10;
}
```

---

## 2. In-Memory Entities & Container Grids

### 2.1 `PlayerItemSlot` (`JXServer.ZoneEngine.Items`)
In-memory representation of an item instance owned by a character on the active map.

| Property | Type | Description |
| :--- | :--- | :--- |
| `RuntimeItemId` | `uint` | Shard-unique runtime identifier |
| `CharacterItemId` | `Guid` | Durable database identifier |
| `Genre` | `ItemGenreKind` | Equipment, Medicine, TownPortal, Quest |
| `DetailType` | `short` | Detail archetype classification |
| `ParticularType` | `ushort` | Specific model/template index |
| `Level` | `byte` | Item tier level (`1..20`) |
| `Series` | `ElementSeries` | Five-elements series (`0..4`) |
| `WidthCells` | `byte` | Horizontal cell width in bag |
| `HeightCells` | `byte` | Vertical cell height in bag |
| `CurrentDurability` | `short` | Current remaining durability |
| `MaxDurability` | `short` | Maximum durability |
| `ContainerPlace` | `ItemContainerPlace` | Current container (`Hand`, `Equip`, `EquipRoom`, `Immediacy`) |
| `GridX` / `GridY` | `byte` | Container coordinate |
| `StackCount` | `ushort` | Current stack count |
| `RandomSeed` | `uint` | Deterministic affix seed |
| `IsBroken` | `bool` | True if durability is 0 (bonuses suspended) |
| `EnchaseLevel` | `byte` | Purple crafting enhancement tier (`0..10`) |
| `MagicAttributes` | `IReadOnlyList<ItemMagicAttributeValue>` | Rolled magic affixes |

### 2.2 `PlayerInventoryGrid` (`JXServer.ZoneEngine.Items`)
Manages spatial cell occupancy across container locations for a single character.

| Member | Purpose |
| :--- | :--- |
| `CanPlaceItem(int place, int gx, int gy, int w, int h)` | Validates boundary and cell overlap |
| `TryMoveItem(PlayerItemSlot item, int destPlace, int destX, int destY)` | Executes atomic move or swap |
| `TrySplitStack(PlayerItemSlot item, ushort splitCount, out PlayerItemSlot? splitItem)` | Splits stack into cursor hand |
| `SafelyReturnHandItem()` | Returns held cursor item to bag on disconnect |
| `RecalculateEquipAffixes()` | Evaluates Five-Elements generation ring and dark affixes |

---

## 3. Wire Packets (`JXServer.Adapter.LegacyJX1.Packets`)

| Struct | Opcode | Size | Description |
| :--- | :--- | :--- | :--- |
| `PlayerMoveItemC2SPacket` | `0x5B` | 7B | Move request: DownY, UpX, DownPos, DownX, UpY, UpPos |
| `PlayerMoveItemS2CPacket` | `0x61` | 7B | Move confirm: DownY, DownPos, DownX, UpY, UpX, UpPos |
| `ItemAutoMoveS2CPacket` | `0x8E` | 1B | Unlocks client mouse drag state |
| `PlayerDivideItemC2SPacket` | `0x85` | 10B | Stack split request with divide count |
| `PlayerEatItemC2SPacket` | `0x59` | 16B | Medicine consumption request |
| `SyncItemS2CPacket` | `0x5F` | 80B | Complete 64-byte `SViewItemInfo` + container placement |
| `RemoveItemS2CPacket` | `0x60` | 5B | Removes item from client inventory |
| `SyncMoneyS2CPacket` | `0x68` | 13B | Synchronizes bag and repository gold balances |
| `PlayerThrowAwayItemC2SPacket` | `0x5E` | 1B | Discards item held in hand to ground |
