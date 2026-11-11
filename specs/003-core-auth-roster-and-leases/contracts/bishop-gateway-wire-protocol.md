# Contract 01: Bishop Gateway Wire Protocol (`TCP Port 5622`)

**Feature**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)  
**Ground-Truth Reference**: `jx-swarm/docs/01-session-lifecycle/bishop-gateway.md`, `SwordOnline/Sources/Core/Src/KAccountUser.cpp`, `KRoleUser.cpp`  

---

## 1. Gateway Connection Lifecycle

1. **TCP Connection**: Client establishes connection to Bishop port (default `5622`).
2. **Handshake (`AccountBeginS2CPacket`, `0x20`)**: Gateway immediately transmits unencrypted 44-byte frame per Spec 002.
3. **Encrypted Framing**: All subsequent C2S and S2C packets are framed with a 2-byte length prefix and encrypted using `KSG_DecodeEncode`.

---

## 2. Gateway Packet Definitions

### 2.1 `AccountLoginC2SPacket` (`0x41`, 127 Bytes Payload)
- `+0x00` (`1B`): `0x41`
- `+0x01` (`2B` uint16): `Size = 0x007E` (`126`)
- `+0x03` (`4B` uint32): `Param = 0x00050000`
- `+0x07` (`32B`): Null-terminated ASCII username (`1..31` bytes)
- `+0x27` (`64B`): Null-terminated 32-character uppercase MD5 hex password digest
- `+0x67` (`4B` uint32): `nLeftTime`
- `+0x6B` (`4B` uint32): `ProtocolVersion` (must equal `0x00000041`)
- `+0x6F` (`4B` uint32): `bKickOnline` (`1` = fence and evict existing session; `0` = reject if online)
- `+0x73` (`12B`): Account limit-time fields

### 2.2 `AccountLoginResultS2CPacket` (`0x41`, 127 Bytes Payload)
Echoes `AccountLoginC2SPacket` layout with `Param` at offset `+0x03` indicating result:
- `0x00050001`: `Success`
- `0x00050002`: `Failed`
- `0x00050003`: `AccountOrPasswordError`
- `0x00050004`: `AccountAlreadyOnline`
- `0x00050005`: `Timeout`
- `0x0005000A`: `InvalidProtocolVersion`
- `0x0005000B`: `AccountFrozen`

On `Success`, the gateway immediately transmits `RoleListResultS2CPacket` (`0x37`).

### 2.3 `RoleListResultS2CPacket` (`0x37`, Variable Length)
- `+0x00` (`1B`): `0x37`
- `+0x01` (`4B` uint32): `nDataLen = 2 + 35 * RoleCount`
- `+0x05` (`4B` uint32): `ulIdentity = 0`
- `+0x09` (`1B`): `bLeave = 0`
- `+0x0A` (`nDataLen` bytes): Encrypted using secondary 16-bit XOR cipher (seed `0x3042`):
  - `+0x00` (`1B`): `RoleCount` (`0..3`)
  - `+0x01` (`1B`): `RefuseCount = 0`
  - For each character entry ($i = 0..\text{RoleCount}-1$):
    - `+0x00` (`32B`): `szName` (null-terminated TCVN3 character name)
    - `+0x20` (`1B`): `Sex` (`0`=Male, `1`=Female)
    - `+0x21` (`1B`): `Series` (`0`=Metal, `1`=Wood, `2`=Water, `3`=Fire, `4`=Earth)
    - `+0x22` (`1B`): `Level` (`1..255`)

### 2.4 `CreateCharacterC2SPacket` (`0x45`, Variable Length `21..45 Bytes`)
- `+0x00` (`1B`): `0x45`
- `+0x01` (`4B` uint32): `nDataLen = NameByteLen + 5`
- `+0x05` (`4B` uint32): `ulIdentity = 0`
- `+0x09` (`1B`): `bLeave = 0`
- `+0x0A` (`1B`): `RoleNo` (`0`=Male, `1`=Female)
- `+0x0B` (`1B`): `Series` (`0..4`)
- `+0x0C` (`2B` uint16): `NativePlaceId` (starter village map ID: `53, 20, 99, 100, 101, 121, 153, 174`)
- `+0x0E` (`NameByteLen` bytes): Null-terminated TCVN3 character name (`6..30` bytes)

### 2.5 `RoleNewDelResponseS2CPacket` (`0x8C`, 42 Bytes Payload)
- `+0x00` (`1B`): `0x8C`
- `+0x01` (`4B` uint32): `ulIdentity = 0`
- `+0x05` (`4B` uint32): `nNativePlaceId`
- `+0x09` (`32B`): `szRoleName` (null-terminated TCVN3)
- `+0x29` (`1B`): `bSucceeded` (`1`=Success, `0`=Failed)

When `bSucceeded == 1` for creation, immediately followed by `NotifyPlayerLoginS2CPacket` (`0x34`).

### 2.6 `DeleteCharacterC2SPacket` (`0x3A`, 129 Bytes Payload)
- `+0x00` (`1B`): `0x3A`
- `+0x01` (`4B` uint32): `ulIdentity = 0`
- `+0x05` (`32B`): `szAccountName` (null-terminated ASCII)
- `+0x25` (`64B`): `Password` (32-character uppercase MD5 hex digest)
- `+0x65` (`28B`): `szRoleName` (null-terminated TCVN3 name to delete)

### 2.7 `SelectCharacterC2SPacket` (`0x64`, 41 Bytes Payload)
- `+0x00` (`1B`): `0x64`
- `+0x01` (`4B` uint32): `ulIdentity = 0`
- `+0x05` (`4B` int32): `nNameLen = strlen(szRoleName) - 1`
- `+0x09` (`32B`): `szRoleName` with the first `nNameLen` bytes obfuscated via the secondary 16-bit XOR cipher (seed `0x38BC`) and trailing bytes plaintext

### 2.8 `NotifyPlayerLoginS2CPacket` (`0x34`, 60 Bytes Payload)
- `+0x00` (`1B`): `0x34`
- `+0x01` (`4B` uint32): `ulIdentity = 0`
- `+0x05` (`16B`): `Guid` (16-byte single-use world handoff ticket)
- `+0x15` (`32B`): `szRoleName` (null-terminated TCVN3)
- `+0x35` (`1B`): `bPermit` (`1`=Permitted, `0`=Denied)
- `+0x36` (`4B` uint32): `nIPAddr` (IPv4 in network byte order)
- `+0x3A` (`2B` uint16): `nPort` (Little-Endian GameServer TCP port, default `6666`)
