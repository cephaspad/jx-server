# Wire Protocol Contract: Ground Loot Objects & Item Generation (Port 6666)

**Feature Branch**: `011-core-loot-and-item-generation`  
**Date**: 2026-11-11  
**Ground-Truth Sources**:
- `jx-swarm/docs/02-game-systems/npc-dialog-inventory-and-economy.md`
- `jx-win-src/Sources/Core/Src/KProtocol.h`, `KObj.cpp`, `KItemGenerator.cpp`, `KItemSet.cpp`

All packets are little-endian and encrypted on TCP Port `6666` via the session's `KsgStreamCipher` + `SecondLayerXorCipher`.

---

## 1. Ground Objects (`KObj` — `C2S` & `S2C`)

### 1.1 `s2c_objadd` (`0x64`, Variable `35 + NameLen` Bytes — `tagOBJ_ADD_SYNC`)
Sent when a ground item or gold pile spawns or when a player enters its `3x3` region neighborhood.

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x64` (`s2c_objadd`) |
| `+0x01` | `2` | `ushort` | `Length` | `TotalSize - 1` (`34 + NameBytes.Length`) |
| `+0x03` | `1` | `byte` | `Dir` | Heading direction (`0..63`) |
| `+0x04` | `4` | `int` | `ObjId` | Runtime ground object ID |
| `+0x08` | `4` | `int` | `DataId` | `ObjDataId` in `objdata.txt` / ground object catalog |
| `+0x0C` | `4` | `int` | `MpsX` | World X coordinate in Mps pixels |
| `+0x10` | `4` | `int` | `MpsY` | World Y coordinate in Mps pixels |
| `+0x14` | `4` | `int` | `MoneyNum` | Gold amount (if currency object; `0` for items) |
| `+0x18` | `4` | `int` | `ItemId` | Runtime `ItemId` (if item object; `0` for money) |
| `+0x1C` | `1` | `byte` | `State` | `0` |
| `+0x1D` | `1` | `byte` | `ItemWidth` | Width in grid cells (`GridWidthCells`) |
| `+0x1E` | `2` | `ushort` | `Genre` | `ItemGenreKind` |
| `+0x20` | `1` | `byte` | `ItemHeight` | Height in grid cells (`GridHeightCells`) |
| `+0x21` | `1` | `byte` | `ColorId` | Quality color (`0` = White, `1` = Blue, `2` = Purple, `3` = Gold) |
| `+0x22` | `1` | `byte` | `Flag` | `0` |
| `+0x23..N` | `Var` | `byte[]` | `Name` | Null-terminated `TCVN3` display name |

### 1.2 `s2c_objremove` (`0x67`, Fixed `6` Bytes — `OBJ_REMOVE_SYNC`)
Sent when a ground object is picked up or expires.

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x67` (`s2c_objremove`) |
| `+0x01` | `4` | `int` | `ObjId` | Runtime ground object ID |
| `+0x05` | `1` | `byte` | `Reason` | `0` = expired/despawned, `1` = picked up by player |

### 1.3 `c2s_requestobj` (`0x4A`, Fixed `5` Bytes) & `c2s_objmouseclick` (`0x72`, Fixed `9` Bytes)
- **`0x4A` (`5B`, `<Bi`)**: `+0x00` `0x4A`, `+0x01` `int ObjId` $\rightarrow$ replies with `0x64` (`s2c_objadd`) if object exists.
- **`0x72` (`9B`, `<BII`)**: `+0x00` `0x72`, `+0x01` `uint RegionId`, `+0x05` `int ObjId` $\rightarrow$ triggers interactive pickup.

---

## 2. Item Pickup & Drop Packets (`C2S`)

### 2.1 `c2s_playerpickupitem` (`0x5A`, Fixed `12` Bytes — `PLAYER_PICKUP_ITEM_COMMAND`)
Sent when a player attempts to pick up a ground object.

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `+0x00` | `1` | `byte` | `ProtocolType` | Always `0x5A` (`c2s_playerpickupitem`) |
| `+0x01` | `1` | `byte` | `TargetGridY` | Preferred placement bag Y coordinate |
| `+0x02` | `1` | `byte` | `TargetGridX` | Preferred placement bag X coordinate |
| `+0x03` | `1` | `byte` | `TargetPlace` | Target container (`2`/`3` = `pos_equiproom`, `7` = `pos_immediacy`) |
| `+0x04` | `4` | `int` | `ObjId` | Runtime ground object ID |
| `+0x08` | `4` | `uint` | `Reserved` | `0` |

### 2.2 `c2s_playerthrowawayitem` (`0x5E`, Fixed `1` Byte) & `c2s_throwawayallmedicine` (`0x83`, Fixed `4` Bytes)
- **`0x5E` (`1B`, `<B`)**: Drops the item currently held on cursor hand (`pos_hand = 1`) onto the ground.
- **`0x83` (`4B`, `<BBBB`)**: Drops all medicines from the character's inventory onto the ground.

---

## 3. Outbound Notification Port: `IInventoryOutboundSink`

```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public interface IInventoryOutboundSink
{
    void SendGroundObjectAdd(in GroundObjectSyncSnapshot snapshot);
    void SendGroundObjectRemove(int objId, byte reason);
    void SendItemSync(in PlayerItemSyncSnapshot snapshot);
    void SendMoneySync(int inventoryMoney, int repositoryMoney, int tradeMoney = 0);
}
```
