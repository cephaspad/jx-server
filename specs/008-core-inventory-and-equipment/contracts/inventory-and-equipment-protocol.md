# Contract: Inventory, Equipment & Container Wire Protocol (Port 6666)

**Feature Branch**: `008-core-inventory-and-equipment` | **Date**: 2026-11-11

All packets below are exchanged over the **GameServer TCP listener (`6666`)**, prefixed by a 2-byte little-endian length header and encrypted/decrypted with the per-session `KSG_DecodeEncode` stream cipher. All multi-byte values are **Little-Endian (`Pack = 1`)**.

---

## 1. Item Synchronization Packets

### 1.1 `s2c_syncitem` (`0x5F` / `95`) — Full Item Sync
- **Direction**: `Server -> Client (S2C)`
- **Payload Size**: `80 bytes` (`wPacketLen = 0x0050`)
- **C# Struct**: `SyncItemS2CPacket`

Synchronizes an item instance in `pos_hand` (`1`), `pos_equip` (`2`), `pos_equiproom` (`3`), or `pos_immediacy` (`7`).

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x5F` (`s2c_syncitem`) |
| **`+0x01..+0x40`** | **`64`** | **`struct`** | **`SViewItemInfo`** | **64-byte item data block** |
| `+0x01` | `4` | `uint` | `RuntimeItemId` | Shard runtime entity ID (`m_ID`) |
| `+0x05` | `1` | `byte` | `Pattern` | `(byte)Genre \| ((byte)Quality << 4)` |
| `+0x06` | `2` | `short` | `DetailType` | Detail archetype classification (`m_Detail`) |
| `+0x08` | `2` | `ushort` | `ParticularType` | Specific item template index (`m_Particur`) |
| `+0x0A` | `1` | `byte` | `Series` | Five-elements series (`0..4`) |
| `+0x0B` | `1` | `byte` | `Level` | Item tier level (`1..20`) |
| `+0x0C` | `1` | `byte` | `Luck` | Luck bonus (`m_btLuck`) |
| `+0x0D..+0x24` | `24` | `short[12]` | `MagicLevel` | Magic affix levels & parameters |
| `+0x25` | `2` | `ushort` | `Version` | `4` (`ItemConsts.GeneratorVersion`) |
| `+0x27` | `4` | `uint` | `RandomSeed` | Affix random seed (`m_dwRandomSeed`) |
| `+0x2B` | `2` | `ushort` | `StackCount` | Stack count (`m_wStackNum`) |
| `+0x2D..+0x40` | `20` | `byte[20]` | `Reserved` | Expiration / lock state metadata |
| **`+0x41..+0x4F`** | **`15`** | **Container Info** | | **Placement & Durability** |
| `+0x41` | `2` | `short` | `MaxDurability` | Maximum item durability |
| `+0x43` | `2` | `short` | `CurrentDurability` | Current remaining durability |
| `+0x45` | `1` | `byte` | `ContainerPlace` | `1`=Hand, `2`=Equip, `3`=EquipRoom, `7`=Immediacy |
| `+0x46` | `1` | `byte` | `GridY` | Container row coordinate |
| `+0x47` | `1` | `byte` | `GridX` | Container column coordinate |
| `+0x48` | `4` | `int` | `WidthCells` | Cell width footprint (`GridWidthCells`) |
| `+0x4C` | `4` | `int` | `HeightCells` | Cell height footprint (`GridHeightCells`) |

---

### 1.2 `s2c_removeitem` (`0x60` / `96`)
- **Direction**: `Server -> Client (S2C)`
- **Payload Size**: `5 bytes` (`wPacketLen = 0x0005`, `<BI`)
- **C# Struct**: `RemoveItemS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x60` (`s2c_removeitem`) |
| `0x01` | `4` | `uint` | `RuntimeItemId` | Item identifier to remove from client UI |

---

### 1.3 `s2c_syncmoney` (`0x68` / `104`)
- **Direction**: `Server -> Client (S2C)`
- **Payload Size**: `13 bytes` (`wPacketLen = 0x000D`, `<Biii`)
- **C# Struct**: `SyncMoneyS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x68` (`s2c_syncmoney`) |
| `0x01` | `4` | `int` | `InventoryMoney` | Gold carried in personal inventory bag |
| `0x05` | `4` | `int` | `RepositoryMoney` | Gold stored in repository storage box |
| `0x09` | `4` | `int` | `TradeMoney` | Gold offered in active player trade (`0`) |

---

## 2. Item Drag, Move & Swap Packets

### 2.1 `c2s_playermoveitem` (`0x5B` / `91`)
- **Direction**: `Client -> Server (C2S)`
- **Payload Size**: `7 bytes` (`wPacketLen = 0x0007`, `<BBBBBBB`)
- **C# Struct**: `PlayerMoveItemC2SPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x5B` (`c2s_playermoveitem`) |
| `0x01` | `1` | `byte` | `DownY` | Source `SrcGridY` |
| `0x02` | `1` | `byte` | `UpX` | Destination `DestGridX` |
| `0x03` | `1` | `byte` | `DownPos` | Source `SrcPlace` (`ItemContainerPlace`) |
| `0x04` | `1` | `byte` | `DownX` | Source `SrcGridX` |
| `0x05` | `1` | `byte` | `UpY` | Destination `DestGridY` |
| `0x06` | `1` | `byte` | `UpPos` | Destination `DestPlace` (`ItemContainerPlace`) |

---

### 2.2 `s2c_playermoveitem` (`0x61` / `97`) & `s2c_ItemAutoMove` (`0x8E` / `142`)
- **`s2c_playermoveitem` (`0x61`, `7 bytes`, `<BBBBBBB`)**:
  - `+0x00` (`byte`): `0x61`
  - `+0x01` (`byte`): `DownY` (`SrcGridY`)
  - `+0x02` (`byte`): `DownPos` (`SrcPlace`)
  - `+0x03` (`byte`): `DownX` (`SrcGridX`)
  - `+0x04` (`byte`): `UpY` (`DestGridY`)
  - `+0x05` (`byte`): `UpX` (`DestGridX`)
  - `+0x06` (`byte`): `UpPos` (`DestPlace`)
- **`s2c_ItemAutoMove` (`0x8E`, `1 byte`)**:
  - `+0x00` (`byte`): `0x8E` (Mandatory unlock signal freeing the client's mouse drag state).

---

## 3. Stack Splitting, Consumables & Dropping

### 3.1 `c2s_playerdivideitem` (`0x85` / `133`)
- **Payload Size**: `10 bytes` (`wPacketLen = 0x000A`, `<BIHBBBB`)
- **C# Struct**: `PlayerDivideItemC2SPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x85` (`c2s_playerdivideitem`) |
| `0x01` | `4` | `uint` | `RuntimeItemId` | Source item identifier to divide |
| `0x05` | `2` | `ushort` | `DivideCount` | Quantity to split into cursor hand |
| `0x07` | `1` | `byte` | `SrcPlace` | Container location of source stack |
| `0x08` | `1` | `byte` | `SrcGridX` | Column coordinate of source stack |
| `0x09` | `1` | `byte` | `SrcGridY` | Row coordinate of source stack |

---

### 3.2 `c2s_playereatitem` (`0x59` / `89`) — Medicine Consumption
- **Payload Size**: `16 bytes` (`wPacketLen = 0x0010`, `<BBiBBII`)
- **C# Struct**: `PlayerEatItemC2SPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x59` (`c2s_playereatitem`) |
| `0x01` | `1` | `byte` | `GridX` | Source column coordinate |
| `0x02` | `4` | `uint` | `RuntimeItemId` | Runtime item identifier |
| `0x06` | `1` | `byte` | `GridY` | Source row coordinate |
| `0x07` | `1` | `byte` | `ContainerPlace` | `3` = EquipRoom, `7` = Immediacy |
| `0x08` | `4` | `uint` | `AntiBotToken` | Client anti-bot token |
| `0x0C` | `4` | `uint` | `TargetId` | Target entity ID (`0` = self) |

---

### 3.3 Item Dropping Packets
- **`c2s_playerthrowawayitem` (`0x5E`, 1 byte)**: Discards the item currently held on cursor (`pos_hand = 1`) to the ground, removing it via `0x60` and broadcasting `0x64` (`s2c_objadd`).
- **`c2s_throwawayallmedicine` (`0x83`, 4 bytes)**: Drops all consumable potions from bag and belt onto the surrounding ground.
