# Contract: Movement, Posture & Obstacle Wire Protocol (Port 6666)

**Feature Branch**: `006-core-movement-and-obstacles` | **Date**: 2026-11-11

All packets below are exchanged over the **GameServer TCP listener (`6666`)**, prefixed by a 2-byte little-endian length header and encrypted/decrypted with the per-session `KSG_DecodeEncode` stream cipher. All multi-byte values are **Little-Endian (`Pack = 1`)**.

---

## 1. Walk & Run Commands (`C2S`)

### 1.1 `c2s_npcwalk` (`0x4B` / `75`) & `c2s_npcrun` (`0x4C` / `76`)
- **Direction**: `Client -> Server (C2S)`
- **Payload Size**: `13 bytes` (`wPacketLen = 0x000D`)
- **C# Structs**: `NpcWalkC2SPacket`, `NpcRunC2SPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | `0x4B` (`c2s_npcwalk`) or `0x4C` (`c2s_npcrun`) |
| `0x01` | `4` | `int` | `TargetMpsX` | Target destination `MpsX` |
| `0x05` | `4` | `int` | `TargetMpsY` | Target destination `MpsY` |
| `0x09` | `4` | `uint` | `CheckValue` | Security checksum (`dwCheckValue`) validated via canonical key |

**Hex Test Vector (`c2s_npcwalk`, Target=(51200, 102400), Check=0x12345678)**:
`4B 00 C8 00 00 00 90 01 00 78 56 34 12`

---

## 2. Walk & Run Trajectory Broadcasts (`S2C`)

### 2.1 `s2c_npcwalk` (`0x50` / `80`)
- **Direction**: `Server -> Client (S2C)`
- **Payload Size**: `13 bytes` (`wPacketLen = 0x000D`)
- **Format**: `<BIii`
- **C# Struct**: `NpcWalkS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x50` (`s2c_npcwalk`) |
| `0x01` | `4` | `uint` | `NpcId` | Moving entity identifier |
| `0x05` | `4` | `int` | `DestMpsY` | Target destination `MpsY` |
| `0x09` | `4` | `int` | `DestMpsX` | Target destination `MpsX` |

### 2.2 `s2c_npcrun` (`0x51` / `81`)
- **Direction**: `Server -> Client (S2C)`
- **Payload Size**: `13 bytes` (`wPacketLen = 0x000D`)
- **Format**: `<BiIi` (Note asymmetric order: `DestMpsY` precedes `NpcId`!)
- **C# Struct**: `NpcRunS2CPacket`

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x51` (`s2c_npcrun`) |
| `0x01` | `4` | `int` | `DestMpsY` | Target destination `MpsY` |
| `0x05` | `4` | `uint` | `NpcId` | Moving entity identifier |
| `0x09` | `4` | `int` | `DestMpsX` | Target destination `MpsX` |

---

## 3. Jump Locomotion Packets

### 3.1 `c2s_npcjump` (`0x4E` / `78`)
- **Payload Size**: `13 bytes` (`<BiiI`)

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x4E` (`c2s_npcjump`) |
| `0x01` | `4` | `int` | `TargetMpsX` | Jump target `MpsX` |
| `0x05` | `4` | `int` | `TargetMpsY` | Jump target `MpsY` |
| `0x09` | `4` | `uint` | `Reserved` | `0` |

### 3.2 `s2c_npcjump` (`0x54` / `84`)
- **Payload Size**: `13 bytes` (`<BiiI`)

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x54` (`s2c_npcjump`) |
| `0x01` | `4` | `int` | `DestMpsY` | Jump target `MpsY` |
| `0x05` | `4` | `int` | `DestMpsX` | Jump target `MpsX` |
| `0x09` | `4` | `uint` | `NpcId` | Jumping entity identifier |

---

## 4. Posture (Sitting / Meditation) Packets

### 4.1 `c2s_npcsit` (`0x71` / `113`)
- **Payload Size**: `2 bytes` (`<BB`)

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x71` (`c2s_npcsit`) |
| `0x01` | `1` | `byte` | `SitFlag` | `0` = Stand (`do_stand`), `1` = Sit (`do_sit`) |

### 4.2 `s2c_npcsit` (`0x53` / `83`)
- **Payload Size**: `5 bytes` (`<BI`)

| Offset | Size | Type | Field | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00` | `1` | `byte` | `ProtocolType` | Always `0x53` (`s2c_npcsit`) |
| `0x01` | `4` | `uint` | `NpcId` | Sitting entity identifier |
