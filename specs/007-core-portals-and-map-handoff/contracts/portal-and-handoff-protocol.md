# Contract: Boundary Portals & Map Transition Wire Protocol (Port 6666)

**Feature Branch**: `007-core-portals-and-map-handoff` | **Date**: 2026-11-11

All packets below are exchanged over the **GameServer TCP listener (`6666`)**, prefixed by a 2-byte little-endian length header and encrypted/decrypted with the per-session `KSG_DecodeEncode` stream cipher. All multi-byte values are **Little-Endian (`Pack = 1`)**.

---

## 1. Same-Server Map Transition Packets

### 1.1 `s2c_syncworld` (`0x49` / `73`) — Map World Synchronization
- **Direction**: `Server -> Client (S2C)`
- **Payload Size**: `34 bytes` (`wPacketLen = 0x0022`)
- **C# Struct**: `SyncWorldS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x49` (`s2c_syncworld`) |
| `0x01` | `4` | `uint` | `SubWorldID` | Destination Map ID (e.g., `53`, `54`) |
| `0x05` | `4` | `uint` | `Region` | Packed destination region: `(RegionY << 16) \| RegionX` |
| `0x09` | `1` | `byte` | `FightMode` | Active combat mode (`0` = Safe, `1` = Combat) |
| `0x0A` | `24` | `byte[24]` | `MapName` | Null-terminated `TCVN3` encoded map display name |

---

### 1.2 `s2c_syncnpcminplayer` (`0x4E` / `78`) — Arrival Position Synchronization
- **Direction**: `Server -> Client (S2C)`
- **Payload Size**: `13 bytes` (`wPacketLen = 0x000D`)
- **C# Struct**: `SyncNpcMinPlayerS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x4E` (`s2c_syncnpcminplayer`) |
| `0x01` | `4` | `uint` | `NpcId` | Player's own runtime entity identifier |
| `0x05` | `2` | `ushort` | `OffX` | Sub-cell X offset: `(ArrivalMpsX % 32) << 10` |
| `0x07` | `2` | `ushort` | `OffY` | Sub-cell Y offset: `(ArrivalMpsY % 32) << 10` |
| `0x09` | `1` | `byte` | `CellX` | Region cell column: `(ArrivalMpsX / 32) % 16` |
| `0x0A` | `1` | `byte` | `CellY` | Region cell row: `(ArrivalMpsY / 32) % 32` |
| `0x0B` | `1` | `byte` | `Doing` | `EntityDoingPosture` (`Stand = 1`) |
| `0x0C` | `1` | `byte` | `Dir` | Facing direction (`0..63`) |

---

## 2. Cross-Node Cluster Redirection Packet

### 2.1 `s2c_notifyplayerexchange` (`0x35` / `53`)
- **Direction**: `Server -> Client (S2C)`
- **Payload Size**: `25 bytes` (`wPacketLen = 0x0019`)
- **C# Struct**: `NotifyPlayerExchangeS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x35` (`s2c_notifyplayerexchange`) |
| `0x01` | `16` | `Guid` | `HandoffGuid` | 16-byte single-use handoff ticket `GUID` (`0` = unavailable) |
| `0x11` | `4` | `uint` | `IpAddr` | Destination GameServer IPv4 address (little-endian uint32) |
| `0x15` | `2` | `ushort` | `Port` | Destination GameServer port in **plain little-endian** (`6666 = 0x1A0A`, NOT `htons`) |
| `0x17` | `2` | `ushort` | `Padding` | Alignment padding (`0x0000`) |

**Hex Test Vector (`0x35`, Port 6666, IP 127.0.0.1)**:
`35 [16 bytes GUID] 01 00 00 7F 0A 1A 00 00`
