# Contract: Spatial AOI Entity Visibility & Detail Query Wire Protocol (Port 6666)

**Feature Branch**: `005-core-spatial-aoi-and-spawns` | **Date**: 2026-11-11

All packets below are exchanged over the **GameServer TCP listener (`6666`)**, prefixed by a 2-byte little-endian length header and encrypted/decrypted with the per-session `KSG_DecodeEncode` stream cipher. All multi-byte values are **Little-Endian (`Pack = 1`)**.

---

## 1. On-Demand Entity Detail Requests

### 1.1 `c2s_requestplayer` (`0x48` / `72`) & `c2s_requestnpc` (`0x49` / `73`)
- **Direction**: `Client -> Server (C2S)`
- **Payload Size**: `5 bytes` (`wPacketLen = 0x0005`)
- **C# Structs**: `RequestPlayerC2SPacket`, `RequestNpcC2SPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | `0x48` (`c2s_requestplayer`) or `0x49` (`c2s_requestnpc`) |
| `0x01` | `4` | `uint` | `TargetNpcId` | Runtime `NpcId` whose full `0x4C` details are requested |

**Hex Test Vector (`c2s_requestnpc`, Target = 15)**:
`49 0F 00 00 00`

---

## 2. Spatial Entity Synchronization Packets

### 2.1 `s2c_syncnpc` (`0x4C` / `76`) — Full Entity Sync
- **Direction**: `Server -> Client (S2C)`
- **Payload Size**: `61 bytes` (`wPacketLen = 0x003D`)
- **C# Struct**: `SyncNpcFullS2CPacket` (or `SyncNpcS2CPacket`)

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x4C` (`s2c_syncnpc`) |
| `0x01` | `2` | `ushort` | `LengthMinusOne` | `60` (`0x003C`, `sizeof(NPC_NORMAL_SYNC) - 1`) |
| `0x03` | `4` | `uint` | `NpcId` | Runtime entity identifier (`1..9999` for NPCs, `>= 10000` for players) |
| `0x07` | `4` | `int` | `MapX` | World `MpsX` pixel coordinate |
| `0x0B` | `4` | `int` | `MapY` | World `MpsY` pixel coordinate |
| `0x0F` | `2` | `ushort` | `OffX` | `0` |
| `0x11` | `2` | `ushort` | `OffY` | `0` |
| `0x13` | `1` | `byte` | `Doing` | `EntityDoingPosture` (`1` = Stand, `2` = Walk, `3` = Run, `4` = Sit, `5` = Jump) |
| `0x14` | `1` | `byte` | `LifePer` | Health percentage `0..100` (`(CurLife * 100) / MaxLife`) |
| `0x15` | `1` | `byte` | `IsGold` | `1` if special/boss NPC, else `0` |
| `0x16` | `1` | `byte` | `Series` | Five-elements series (`0..4`: Metal, Wood, Water, Fire, Earth) |
| `0x17` | `4` | `uint` | `StateInfo` | Status bitmask (`0x00000000`) |
| `0x1B` | `1` | `byte` | `Camp` | Current camp (`0..6`) |
| `0x1C` | `4` | `uint` | `NpcSettingIdx` | For NPCs: `((uint)Level << 16) \| (uint)(ushort)TemplateId`; for Players: `0` |
| `0x20` | `1` | `byte` | `Dir` | 64-direction facing angle (`0..63`) |
| `0x21` | `1` | `byte` | `Kind` | `EntityKind` (`0` = Monster, `1` = Player, `2` = Partner, `3` = Dialog NPC) |
| `0x22` | `3` | `byte[3]` | `Reserved` | Zero-filled (`0x000000`) |
| `0x25` | `32` | `byte[32]` | `Name` | Null-terminated `TCVN3` encoded display name |

---

### 2.2 `s2c_syncnpcmin` (`0x4D` / `77`) — Compact Neighbor Position Sync
- **Direction**: `Server -> Client (S2C)`
- **Payload Size**: `18 bytes` (`wPacketLen = 0x0012`)
- **C# Struct**: `SyncNpcMinS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x4D` (`s2c_syncnpcmin`) |
| `0x01` | `4` | `uint` | `NpcId` | Runtime entity identifier |
| `0x05` | `4` | `int` | `MpsX` | World `MpsX` pixel coordinate |
| `0x09` | `4` | `int` | `MpsY` | World `MpsY` pixel coordinate |
| `0x0D` | `1` | `byte` | `Doing` | `EntityDoingPosture` (`1..5`) |
| `0x0E` | `1` | `byte` | `LifePer` | Health percentage `0..100` |
| `0x0F` | `1` | `byte` | `State` | Status flag (`0`) |
| `0x10` | `1` | `byte` | `Camp` | Current camp (`0..6`) |
| `0x11` | `1` | `byte` | `Dir` | 64-direction facing angle (`0..63`) |

---

### 2.3 `s2c_syncnpcminplayer` (`0x4E` / `78`) — Authoritative Self Position Sync
- **Direction**: `Server -> Client (S2C)`
- **Payload Size**: `13 bytes` (`wPacketLen = 0x000D`)
- **C# Struct**: `SyncNpcMinPlayerS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x4E` (`s2c_syncnpcminplayer`) |
| `0x01` | `4` | `uint` | `NpcId` | Player's own runtime `NpcId` |
| `0x05` | `2` | `ushort` | `OffX` | Sub-cell X offset: `(MpsX % 32) << 10` |
| `0x07` | `2` | `ushort` | `OffY` | Sub-cell Y offset: `(MpsY % 32) << 10` |
| `0x09` | `1` | `byte` | `CellX` | Cell X inside current region: `(MpsX / 32) % 16` |
| `0x0A` | `1` | `byte` | `CellY` | Cell Y inside current region: `(MpsY / 32) % 32` |
| `0x0B` | `1` | `byte` | `Doing` | `EntityDoingPosture` (`1` = Stand, etc.) |
| `0x0C` | `1` | `byte` | `Dir` | 64-direction facing angle (`0..63`) |
