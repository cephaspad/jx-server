# Wire Protocol Contract: Ground Loot Objects & `6x10` Player Inventory (Port 6666)

**Feature Branch**: `003-combat-missiles-loot-inventory`  
**Date**: 2026-11-09  
**Ground-Truth Sources**:
- `jx-swarm/docs/02-game-systems/npc-dialog-inventory-and-economy.md`
- `jx-win-src/Sources/Core/Src/KProtocol.h`, `KObj.cpp`, `KItemList.cpp`, `KItem.cpp`

---

## 1. Ground Objects (`KObj` — `C2S` & `S2C`)

### 1.1 `s2c_objadd` (`0x64`, Variable `35 + NameLen` Bytes — `tagOBJ_ADD_SYNC`)
Sent when a ground item or gold pile spawns or when a player enters its `3x3` region neighborhood.
- **`+0x00` (`byte`)**: `ProtocolType = 0x64`
- **`+0x01` (`ushort`)**: `Length` (`TotalSize - 1`, i.e., `34 + NameBytes.Length`)
- **`+0x03` (`byte`)**: `Dir` (`0..63`)
- **`+0x04` (`int`)**: `ObjId` (runtime ground object ID)
- **`+0x08` (`int`)**: `DataId` (`ObjDataId` in `objdata.txt` / `groundObjects`)
- **`+0x0C` (`int`)**: `MpsX` (world X coordinate in Mps pixels)
- **`+0x10` (`int`)**: `MpsY` (world Y coordinate in Mps pixels)
- **`+0x14` (`int`)**: `MoneyNum` (gold amount if money object; `0` for items)
- **`+0x18` (`int`)**: `ItemId` (runtime `ItemId` if item object)
- **`+0x1C` (`byte`)**: `State` (`0`)
- **`+0x1D` (`byte`)**: `ItemWidth` (`GridWidthCells`)
- **`+0x1E` (`ushort`)**: `Genre` (`ItemGenreKind`)
- **`+0x20` (`byte`)**: `ItemHeight` (`GridHeightCells`)
- **`+0x21` (`byte`)**: `ColorId` (`0` = White, `1` = Blue, `2` = Purple, `3` = Gold)
- **`+0x22` (`byte`)**: `Flag` (`0`)
- **`+0x23..N` (`byte[]`)**: Null-terminated `TCVN3` display name (`Name`).

### 1.2 `s2c_objremove` (`0x67`, Fixed `6` Bytes — `OBJ_REMOVE_SYNC`)
- **`+0x00` (`byte`)**: `ProtocolType = 0x67`
- **`+0x01` (`int`)**: `ObjId`
- **`+0x05` (`byte`)**: `Reason` (`0` = expired/despawned, `1` = picked up by a player)

### 1.3 `c2s_requestobj` (`0x4A`, Fixed `5` Bytes) & `c2s_objmouseclick` (`0x72`, Fixed `9` Bytes)
- **`0x4A` (`5B`, `<Bi`)**: `+0x00` `0x4A`, `+0x01` `int ObjId` $\rightarrow$ replies with `0x64` (`s2c_objadd`) if the object exists.
- **`0x72` (`9B`, `<BII`)**: `+0x00` `0x72`, `+0x01` `uint RegionId`, `+0x05` `int ObjId` $\rightarrow$ triggers interactive map object or pickup.

---

## 2. Inventory Synchronization, Pickup, Move, Consume & Drop (`C2S` & `S2C`)

### 2.1 `s2c_syncitem` (`0x5F`, Fixed `80` Bytes — `tagItemSync` / `ITEM_SYNC`)
Synchronizes a single item instance in the player's inventory (`pos_hand = 1`, `pos_equip = 2`, `pos_equiproom = 3`, `pos_immediacy = 7`).
- **`+0x00` (`byte`)**: `ProtocolType = 0x5F`
- **`+0x01..+0x40` (`64` bytes — `SViewItemInfo`)**:
  - `+0x01` (`uint`): `RuntimeItemId` (`m_ID`)
  - `+0x05` (`byte`): `Pattern` (`(byte)Genre | ((byte)Quality << 4)`)
  - `+0x06` (`short`): `DetailType` (`m_Detail`)
  - `+0x08` (`ushort`): `ParticularType` (`m_Particur`)
  - `+0x0A` (`byte`): `Series` (`ElementSeries`)
  - `+0x0B` (`byte`): `Level` (`1..20`)
  - `+0x0C` (`byte`): `Luck` (`m_btLuck`)
  - `+0x0D..+0x24` (`short[12]` = `24` bytes): `MagicLevel[0..11]` (Generator magic levels / Purple enchase parameters)
  - `+0x25` (`ushort`): `Version` (`4` = `ItemConsts.GeneratorVersion`)
  - `+0x27` (`uint`): `RandomSeed` (`m_dwRandomSeed`)
  - `+0x2B` (`ushort`): `StackCount` (`m_wStackNum`)
  - `+0x2D..+0x40` (`20` bytes): Expiration/Binding metadata (`ExpireTime`, `LockState`)
- **`+0x41` (`short`)**: `MaxDurability` (`m_MaxDurability`)
- **`+0x43` (`short`)**: `CurrentDurability` (`m_CurrentDurability`)
- **`+0x45` (`byte`)**: `ContainerPlace` (`ItemContainerPlace`)
- **`+0x46` (`byte`)**: `GridY` (`m_btY`)
- **`+0x47` (`byte`)**: `GridX` (`m_btX`)
- **`+0x48` (`int`)**: `WidthCells` (`GridWidthCells`)
- **`+0x4C` (`int`)**: `HeightCells` (`GridHeightCells`)

### 2.2 `s2c_syncmoney` (`0x68`, Fixed `13` Bytes — `PLAYER_MONEY_SYNC`)
- **`+0x00` (`byte`)**: `ProtocolType = 0x68`
- **`+0x01` (`int`)**: `InventoryMoney` (gold carried in bag)
- **`+0x05` (`int`)**: `RepositoryMoney` (gold stored in storage box)
- **`+0x09` (`int`)**: `TradeMoney` (`0`)

### 2.3 `c2s_playerpickupitem` (`0x5A`, Fixed `12` Bytes — `PLAYER_PICKUP_ITEM_COMMAND`)
- **`+0x00` (`byte`)**: `ProtocolType = 0x5A`
- **`+0x01` (`byte`)**: `TargetGridY`
- **`+0x02` (`byte`)**: `TargetGridX`
- **`+0x03` (`byte`)**: `TargetPlace` (`2`/`3` = `pos_equiproom`, `7` = `pos_immediacy`)
- **`+0x04` (`int`)**: `ObjId` (runtime ground object ID)
- **`+0x08` (`uint`)**: `Reserved`

### 2.4 `c2s_playermoveitem` (`0x5B`, Fixed `7` Bytes) $\rightarrow$ `s2c_playermoveitem` (`0x61`, Fixed `7` Bytes) + `s2c_ItemAutoMove` (`0x8E`, `1` Byte)
- **Request (`0x5B`, `7B`)**:
  - `+0x00` (`byte`): `ProtocolType = 0x5B`
  - `+0x01` (`byte`): `DownY` (`SrcGridY`)
  - `+0x02` (`byte`): `UpX` (`DestGridX`)
  - `+0x03` (`byte`): `DownPos` (`SrcPlace`)
  - `+0x04` (`byte`): `DownX` (`SrcGridX`)
  - `+0x05` (`byte`): `UpY` (`DestGridY`)
  - `+0x06` (`byte`): `UpPos` (`DestPlace`)
- **Response (`0x61`, `7B`)**:
  - `+0x00` (`byte`): `ProtocolType = 0x61`
  - `+0x01` (`byte`): `DownY` (`SrcGridY`)
  - `+0x02` (`byte`): `DownPos` (`SrcPlace`)
  - `+0x03` (`byte`): `DownX` (`SrcGridX`)
  - `+0x04` (`byte`): `UpY` (`DestGridY`)
  - `+0x05` (`byte`): `UpX` (`DestGridX`)
  - `+0x06` (`byte`): `UpPos` (`DestPlace`)
- Followed by `0x8E` (`1B`, `s2c_ItemAutoMove`) to unlock the client's inventory drag lock, and equipment stat/Five-Elements recalculation if `SrcPlace == Equip` or `DestPlace == Equip`.

### 2.5 `c2s_playereatitem` (`0x59`, Fixed `16` Bytes) & `s2c_removeitem` (`0x60`, Fixed `5` Bytes)
- **Request (`0x59`, `16B`)**:
  - `+0x00` (`byte`): `0x59`
  - `+0x01` (`byte`): `GridX`
  - `+0x02` (`uint`): `RuntimeItemId`
  - `+0x06` (`byte`): `GridY`
  - `+0x07` (`byte`): `ContainerPlace` (`3` = `pos_equiproom`, `7` = `pos_immediacy`)
  - `+0x08` (`uint`): `AntiBotToken`
  - `+0x0C` (`uint`): `TargetId`
- **Item Removal (`0x60`, `5B`)**:
  - `+0x00` (`byte`): `0x60`
  - `+0x01` (`uint`): `RuntimeItemId` (sent when a single-stack consumable is eaten or an item is dropped).

### 2.6 `c2s_playerthrowawayitem` (`0x5E`, `1` Byte), `c2s_throwawayallmedicine` (`0x83`, `4` Bytes) & `c2s_playerdivideitem` (`0x85`, `10` Bytes)
- **`0x5E` (`1B`)**: Drops the item currently held in `pos_hand` (`1`), sending `0x60` (`s2c_removeitem`) to the player and broadcasting `0x64` (`s2c_objadd`) to the `3x3` region AOI.
- **`0x83` (`4B`)**: Drops all medicines from `pos_equiproom` / `pos_immediacy` onto the ground around the player.
- **`0x85` (`10B`, `<BIHBBBB`)**: Splits `DivideCount` (`ushort` at `+0x05`) from a stackable item (`uint RuntimeItemId` at `+0x01`) at `(SrcPlace, SrcGridX, SrcGridY)` into `pos_hand`.
