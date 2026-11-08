# Contract 01: Bishop Gateway Wire Protocol (`TCP Port 5622`)

**Feature**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)  
**Ground-Truth Evidence**: `jx-swarm/docs/00-network-core/framing-and-cipher.md`, `jx-swarm/docs/01-session-lifecycle/bishop-gateway.md`, `bishop_y` (`0x0805411c..0x08057ba0`)

---

## 1. Framing & Initial Session Handshake (`0x20`)

1. **Outer TCP Frame**:
   - Every frame starts with a 2-byte little-endian plaintext `wFrameLength` (`PayloadLength + 2`, valid range `3..65535`).
2. **Initial Handshake (`AccountBeginS2CPacket`, Opcode `0x20`, `42 bytes` payload / `44 bytes` on wire)**:
   - Sent unencrypted by the Gateway immediately upon accepting a TCP connection:
     - `+0x00` (`1B`): `0x20`
     - `+0x01` (`1B`): `0x00`
     - `+0x02` (`6B`): Random padding
     - `+0x08` (`4B`): `uint32 ObfuscatedClientKey` (obfuscated `uClientKey` used to decrypt incoming `c2s_*` frames)
     - `+0x0C` (`5B`): Random padding
     - `+0x11` (`4B`): `uint32 ObfuscatedServerKey` (obfuscated `uServerKey` used to encrypt outgoing `s2c_*` frames)
     - `+0x15` (`21B`): Random padding
3. **Rolling `KSG_DecodeEncode` Stream Cipher**:
   - All subsequent frames are decrypted/encrypted in-place using the 5,679-entry `g_uPublicKeys` table (`SHA-256: 5491f08643c87d4ad6c08e3b5f44457acbc78a3cd0d288b6d6f4d99dafe66047`) and mutated per packet via `uKey = RotateLeft(uKey, 1) * 0x10A860C1u + 0xEE57FD49u`.

---

## 2. Gateway Packet Contracts (`*C2SPacket` & `*S2CPacket`)

| Packet Contract Name | Opcode | Direction | Payload Size | Wire Structure & Validation Rules |
| :--- | :--- | :--- | :--- | :--- |
| **`AccountLoginC2SPacket`** | `0x41` | `C2S` | Fixed `127B` | `+0x00`: `0x41`<br>`+0x01` (`uint16`): `Size = 0x007E` (`126`)<br>`+0x03` (`uint32`): `Param = 0x00050000`<br>`+0x07` (`byte[32]`): Null-terminated ASCII username (`1..31` bytes)<br>`+0x27` (`byte[64]`): Null-terminated 32-char uppercase MD5 hex password<br>`+0x67` (`uint32`): `nLeftTime`<br>`+0x6B` (`uint32`): `ProtocolVersion` (must equal `0x00000041`)<br>`+0x6F` (`uint32`): `bKickOnline` (`1` = fence & evict active session)<br>`+0x73` (`byte[12]`): Limit-time fields |
| **`AccountLoginResultS2CPacket`** | `0x41` | `S2C` | Fixed `127B` | Echoes `AccountLoginC2SPacket` header with `+0x03` (`uint32 Param`) set to `0x00050001` (`Success`), `0x00050003` (`AccountOrPasswordError`), `0x00050004` (`AccountAlreadyOnline`), `0x0005000A` (`InvalidProtocolVersion`), or `0x0005000B` (`AccountFrozen`). On `Success`, the gateway immediately follows with `RoleListResultS2CPacket` (`0x37`). |
| **`RoleListResultS2CPacket`** | `0x37` | `S2C` | Variable `10 + nDataLen` (`12B` for 0 roles, `47B`/`82B`/`117B` for 1/2/3 roles) | `+0x00`: `0x37`<br>`+0x01` (`uint32`): `nDataLen` (`2 + 35 * RoleCount`)<br>`+0x05` (`uint32`): `ulIdentity`<br>`+0x09` (`byte`): `bLeave = 0`<br>`+0x0A` (`byte[nDataLen]`): Buffer containing `RoleCount` (`1B`), `RefuseCount = 0` (`1B`), and `RoleCount` entries of `35B` (`szName[32]` TCVN3, `Sex` `1B`, `Series` `1B`, `Level` `1B`), encrypted with the second-layer `0x3042` rolling 16-bit XOR cipher. |
| **`CreateCharacterC2SPacket`** | `0x45` | `C2S` | Variable `15 + NameByteLen` (`21..45B`) | `+0x00`: `0x45`<br>`+0x01` (`uint32`): `nDataLen` (`NameByteLen + 5`)<br>`+0x05` (`uint32`): `ulIdentity = 0`<br>`+0x09` (`byte`): `bLeave = 0`<br>`+0x0A` (`byte`): `RoleNo` (`0`=Male, `1`=Female)<br>`+0x0B` (`byte`): `Series` (`0..4`)<br>`+0x0C` (`uint16`): `NativePlaceId` (valid starter village Map ID: `53`, `20`, `99`, `100`, `101`, `121`, `153`, `174`)<br>`+0x0E` (`byte[]`): Null-terminated TCVN3 character name (`6..30` bytes). |
| **`DeleteCharacterC2SPacket`** | `0x3A` | `C2S` | Fixed `129B` | `+0x00`: `0x3A`<br>`+0x01` (`uint32`): `ulIdentity`<br>`+0x05` (`byte[32]`): `szAccountName`<br>`+0x25` (`byte[64]`): `Password` (uppercase MD5 hex)<br>`+0x65` (`byte[28]`): `szRoleName` (TCVN3 character name to delete). |
| **`RoleNewDelResponseS2CPacket`** | `0x8C` | `S2C` | Fixed `42B` | `+0x00`: `0x8C`<br>`+0x01` (`uint32`): `ulIdentity = 0`<br>`+0x05` (`uint32`): `nNativePlaceId`<br>`+0x09` (`byte[32]`): `szRoleName` (TCVN3)<br>`+0x29` (`byte`): `bSucceeded` (`1` = success, `0` = failed). When `bSucceeded == 1` for character creation (`0x45`), the gateway immediately sends `NotifyPlayerLoginS2CPacket` (`0x34`). |
| **`SelectCharacterC2SPacket`** | `0x64` | `C2S` | Fixed `41B` | `+0x00`: `0x64`<br>`+0x01` (`uint32`): `ulIdentity = 0`<br>`+0x05` (`int32`): `nNameLen` (`strlen(szRoleName) - 1`, range `1..31`)<br>`+0x09` (`byte[32]`): `szRoleName` with first `nNameLen` bytes obfuscated via the second-layer `0x38BC` 16-bit XOR cipher and the byte at index `nNameLen` in plaintext. |
| **`NotifyPlayerLoginS2CPacket`** | `0x34` | `S2C` | Fixed `60B` | `+0x00`: `0x34`<br>`+0x01` (`uint32`): `ulIdentity`<br>`+0x05` (`byte[16]`): `Guid` (single-use 16-byte session handoff ticket)<br>`+0x15` (`byte[32]`): `szRoleName` (TCVN3)<br>`+0x35` (`byte`): `bPermit` (`1` = permitted, `0` = denied)<br>`+0x36` (`uint32`): `nIPAddr` (IPv4 bytes in network order)<br>`+0x3A` (`uint16`): `nPort` (little-endian GameServer TCP port, default `6666`). |
