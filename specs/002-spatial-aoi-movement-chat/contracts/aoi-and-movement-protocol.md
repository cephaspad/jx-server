# Contract: GameServer Spatial AOI, Movement & Map Transition Wire Protocol (Port 6666)

**Feature Branch**: `002-spatial-aoi-movement-chat`  
**Date**: 2026-11-09  
**Spec**: [../spec.md](../spec.md) | **Research**: [../research.md](../research.md)

All packets below are exchanged over the **GameServer TCP listener (`6666`)**, prefixed by a 2-byte little-endian `wPacketLen` and encrypted/decrypted with the per-session `KSG_DecodeEncode` stream cipher (`g_uPublicKeys`). All multi-byte integers are **Little-Endian (`Pack = 1`)**.

---

## 1. Entity Detail Request & AOI Synchronization Packets

### 1.1 `c2s_requestplayer` (`0x48` / Decimal `72`) & `c2s_requestnpc` (`0x49` / Decimal `73`)
- **Direction**: `Client -> Server (C2S)`
- **Fixed Payload Size**: `5 bytes` (`wPacketLen = 0x0005`)
- **C# Structs**: `RequestPlayerC2SPacket`, `RequestNpcC2SPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | `0x48` (`c2s_requestplayer`) or `0x49` (`c2s_requestnpc`) |
| `0x01` | `4` | `uint` | `TargetNpcId` | Runtime `Npc.m_dwID` whose full `0x4C` (`s2c_syncnpc`) details are requested |

---

### 1.2 `s2c_syncnpc` (`0x4C` / Decimal `76`) — Full Entity Sync
- **Direction**: `Server -> Client (S2C)`
- **Payload Size**: `61 bytes` (`wPacketLen = 0x003D`)
- **C# Struct**: `SyncNpcS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x4C` (`s2c_syncnpc`) |
| `0x01` | `2` | `ushort` | `LengthMinusOne` | `60` (`0x003C`, `sizeof(NPC_NORMAL_SYNC) - 1`) |
| `0x03` | `4` | `uint` | `NpcId` | Runtime entity ID (`1..9999` for static NPCs/monsters, `>= 10000` for players) |
| `0x07` | `4` | `int` | `MapX` | World `MpsX` pixel coordinate |
| `0x0B` | `4` | `int` | `MapY` | World `MpsY` pixel coordinate |
| `0x0F` | `2` | `ushort` | `OffX` | `0` |
| `0x11` | `2` | `ushort` | `OffY` | `0` |
| `0x13` | `1` | `byte` | `Doing` | `EntityDoingPosture` (`1` = Stand, `2` = Walk, `3` = Run, `4` = Sit, `5` = Jump) |
| `0x14` | `1` | `byte` | `LifePer` | Health percentage `0..100` (`(CurLife * 100) / MaxLife`) |
| `0x15` | `1` | `byte` | `IsGold` | `1` if `IsSpecialNpc`, else `0` |
| `0x16` | `1` | `byte` | `Series` | Five-elements series (`0..4`) |
| `0x17` | `4` | `uint` | `StateInfo` | Active status bitmask (`0` in Milestone 2) |
| `0x1B` | `1` | `byte` | `Camp` | Current camp (`0..6`) |
| `0x1C` | `4` | `uint` | `NpcSettingIdx` | For NPCs: `((uint)(ushort)Level << 16) \| (uint)(ushort)TemplateId`; for Players: `0` |
| `0x20` | `1` | `byte` | `Dir` | 64-direction facing angle (`0..63`) |
| `0x21` | `1` | `byte` | `Kind` | `EntityKind` (`0` = Monster, `1` = Player, `2` = Partner, `3` = Dialog NPC) |
| `0x22` | `3` | `byte[3]` | `Reserved` | Zero-filled (`0x00`) |
| `0x25` | `32` | `byte[32]` | `Name` | Null-terminated `TCVN3` display name |

---

### 1.3 `s2c_syncnpcmin` (`0x4D` / Decimal `77`) — Compact Neighbor Position Sync
- **Direction**: `Server -> Client (S2C)`
- **Fixed Payload Size**: `18 bytes` (`wPacketLen = 0x0012`)
- **C# Struct**: `SyncNpcMinS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x4D` (`s2c_syncnpcmin`) |
| `0x01` | `4` | `uint` | `NpcId` | Runtime entity ID (`Npc.m_dwID`) |
| `0x05` | `4` | `int` | `MpsX` | Current world `MpsX` pixel coordinate |
| `0x09` | `4` | `int` | `MpsY` | Current world `MpsY` pixel coordinate |
| `0x0D` | `1` | `byte` | `Doing` | `EntityDoingPosture` (`1` = Stand, `2` = Walk, `3` = Run, `4` = Sit, `5` = Jump) |
| `0x0E` | `1` | `byte` | `LifePer` | Health percentage `0..100` |
| `0x0F` | `1` | `byte` | `State` | Status flag (`0`) |
| `0x10` | `1` | `byte` | `Camp` | Current camp (`0..6`) |
| `0x11` | `1` | `byte` | `Dir` | 64-direction facing angle (`0..63`) |

---

### 1.4 `s2c_syncnpcminplayer` (`0x4E` / Decimal `78`) — Self Position Sync
- **Direction**: `Server -> Client (S2C)`
- **Fixed Payload Size**: `13 bytes` (`wPacketLen = 0x000D`)
- **C# Struct**: `SyncNpcMinPlayerS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x4E` (`s2c_syncnpcminplayer`) |
| `0x01` | `4` | `uint` | `NpcId` | Player's own runtime `NpcId` |
| `0x05` | `2` | `ushort` | `OffX` | Sub-cell X offset (`(MpsX % 32) << 10`) |
| `0x07` | `2` | `ushort` | `OffY` | Sub-cell Y offset (`(MpsY % 32) << 10`) |
| `0x09` | `1` | `byte` | `CellX` | Cell X inside current region (`0..15`) |
| `0x0A` | `1` | `byte` | `CellY` | Cell Y inside current region (`0..31`) |
| `0x0B` | `1` | `byte` | `Doing` | `EntityDoingPosture` (`1..5`) |
| `0x0C` | `1` | `byte` | `Dir` | 64-direction facing angle (`0..63`) |

---

## 2. Movement & Posture Packets

### 2.1 `c2s_npcwalk` (`0x4B` / `75`) & `c2s_npcrun` (`0x4C` / `76`)
- **Direction**: `Client -> Server (C2S)`
- **Fixed Payload Size**: `13 bytes` (`wPacketLen = 0x000D`)
- **C# Structs**: `NpcWalkC2SPacket`, `NpcRunC2SPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | `0x4B` (`c2s_npcwalk`) or `0x4C` (`c2s_npcrun`) |
| `0x01` | `4` | `int` | `TargetMpsX` | Target destination `MpsX` |
| `0x05` | `4` | `int` | `TargetMpsY` | Target destination `MpsY` |
| `0x09` | `4` | `uint` | `CheckValue` | Verified via `MovementAntiCheatConsts.IsValidCheckValue` (`0x24A1` for `0x4B`, `0x1D78` for `0x4C`) |

---

### 2.2 `s2c_npcwalk` (`0x50` / `80`) vs. `s2c_npcrun` (`0x51` / `81`)
- **Direction**: `Server -> Client (S2C)`
- **Fixed Payload Size**: `13 bytes` (`wPacketLen = 0x000D`)
- **C# Structs**: `NpcWalkS2CPacket`, `NpcRunS2CPacket`

> [!IMPORTANT]
> Note the asymmetric field order between `0x50` (`NpcId, DestMpsY, DestMpsX`) and `0x51` (`DestMpsY, NpcId, DestMpsX`) verified in `KNpc::DoWalk` (`0x08096f40`) and `KNpc::DoRun` (`0x08097060`).

#### `s2c_npcwalk` (`0x50`, `<BIii`):
| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x50` (`s2c_npcwalk`) |
| `0x01` | `4` | `uint` | `NpcId` | Moving entity `NpcId` |
| `0x05` | `4` | `int` | `DestMpsY` | Target destination `MpsY` |
| `0x09` | `4` | `int` | `DestMpsX` | Target destination `MpsX` |

#### `s2c_npcrun` (`0x51`, `<BiIi`):
| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x51` (`s2c_npcrun`) |
| `0x01` | `4` | `int` | `DestMpsY` | Target destination `MpsY` |
| `0x05` | `4` | `uint` | `NpcId` | Moving entity `NpcId` |
| `0x09` | `4` | `int` | `DestMpsX` | Target destination `MpsX` |

---

### 2.3 `c2s_npcjump` (`0x4E` / `78`) & `s2c_npcjump` (`0x54` / `84`)
- **Fixed Payload Size**: `13 bytes` (`wPacketLen = 0x000D`)
- **C# Structs**: `NpcJumpC2SPacket`, `NpcJumpS2CPacket`

#### `c2s_npcjump` (`0x4E`, `<BiiI`):
| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x4E` (`c2s_npcjump`) |
| `0x01` | `4` | `int` | `TargetMpsX` | Jump destination `MpsX` |
| `0x05` | `4` | `int` | `TargetMpsY` | Jump destination `MpsY` |
| `0x09` | `4` | `uint` | `Reserved` | Unused (`0`, no check value validation) |

#### `s2c_npcjump` (`0x54`, `<BiiI`):
| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x54` (`s2c_npcjump`) |
| `0x01` | `4` | `int` | `DestMpsY` | Jump destination `MpsY` |
| `0x05` | `4` | `int` | `DestMpsX` | Jump destination `MpsX` |
| `0x09` | `4` | `uint` | `NpcId` | Jumping entity `NpcId` |

---

### 2.4 `c2s_npcsit` (`0x71` / `113`) & `s2c_npcsit` (`0x53` / `83`)
- **C# Structs**: `NpcSitC2SPacket` (`2B`), `NpcSitS2CPacket` (`5B`)

#### `c2s_npcsit` (`0x71`, `2 bytes`, `<BB`):
| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x71` (`c2s_npcsit`) |
| `0x01` | `1` | `byte` | `SitFlag` | `0` = Stand (`do_stand`), non-zero = Sit (`do_sit`) |

#### `s2c_npcsit` (`0x53`, `5 bytes`, `<BI`):
| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x53` (`s2c_npcsit`) |
| `0x01` | `4` | `uint` | `NpcId` | Sitting entity `NpcId` |

---

## 3. Map Transitions (`s2c_syncworld` `0x49` vs. `s2c_notifyplayerexchange` `0x35`)

### 3.1 Same-Server Map Transition Sequence
When a portal trap transitions a player to a map hosted on the same GameServer node:
1. Send `s2c_syncworld` (`0x49`, `34 bytes`) with `SubWorldID = TargetMapId` and `Region = (TargetRegionY << 16) | TargetRegionX`.
2. Send `s2c_syncnpcminplayer` (`0x4E`, `13 bytes`) with the arrival sub-cell/cell offsets and `Doing = Stand (1)`.
3. Send `s2c_syncnpc` (`0x4C`, `61 bytes`) for all static NPCs and players in the destination's `3x3` region AOI.
4. **Do NOT** send `s2c_syncend` (`0x43`) and **do NOT** reset the TCP stream cipher.

### 3.2 Cross-Server Map Transition (`s2c_notifyplayerexchange` `0x35` / `53`)
- **Direction**: `Server -> Client (S2C)`
- **Fixed Payload Size**: `25 bytes` (`wPacketLen = 0x0019`)
- **C# Struct**: `NotifyPlayerExchangeS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x35` (`s2c_notifyplayerexchange`) |
| `0x01` | `16` | `Guid` | `HandoffGuid` | 16-byte single-use handoff ticket `GUID` (`0` = server unavailable) |
| `0x11` | `4` | `uint` | `IpAddr` | Target GameServer IPv4 address (`uint32` little-endian) |
| `0x15` | `2` | `ushort` | `Port` | Target GameServer port in **plain little-endian** (`6666 = 0x1A0A`, NOT `htons`) |
| `0x17` | `2` | `ushort` | `Padding` | Alignment padding (`0x0000`) |
