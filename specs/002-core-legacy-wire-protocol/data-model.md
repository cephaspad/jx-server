# Data Model: Core Legacy Wire Protocol & Ciphers

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  
**Research**: [research.md](research.md)  

---

## 1. Wire Frame & Packet Structures

### 1.1 Outer TCP Wire Frame Layout

```text
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|       wFrameLength (LE)       |   Opcode      |               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+               +
|                                                               |
|        Encrypted or Plaintext Application Payload...          |
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

- **`wFrameLength`** (`uint16`, 2 bytes): Little-endian total frame length (`PayloadLength + 2`). Valid range: `3..65535`.
- **`Opcode`** (`uint8`, 1 byte): Identifies the application message type.
- **`Payload`** (`byte[]`, variable length): Message contents, encrypted via `KSG_DecodeEncode` (except for initial `0x20` handshake).

---

### 1.2 `AccountBeginS2CPacket` (`0x20`, 44 Bytes Wire Frame)

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct AccountBeginS2CPacket
{
    public const int WireFrameSize = 44;
    public const int PayloadSize = 42;
    public const byte Opcode = 0x20;

    public ushort FrameLength => 44;
    public byte PacketOpcode => 0x20;
    public byte ReservedZero => 0x00;
    // 6 bytes random padding
    public uint ObfuscatedClientKey; // at offset +0x08
    // 5 bytes random padding
    public uint ObfuscatedServerKey; // at offset +0x11
    // 21 bytes random padding
}
```

---

## 2. Cryptographic State & Key Structures

### 2.1 Public Key Substitution Table (`KsgPublicKeyTable`)
- **Key Count**: 5,679 entries of 32-bit unsigned integers (`uint32`).
- **Binary Size**: 22,716 bytes.
- **Integrity**: Verified via SHA-256 (`5491f08643c87d4ad6c08e3b5f44457acbc78a3cd0d288b6d6f4d99dafe66047`).
- **First Key**: `0x853B9129u`.

### 2.2 Per-Connection Cipher State
- **`uClientKey`** (`uint32`): Rolling key used by server to decrypt incoming C2S frames.
- **`uServerKey`** (`uint32`): Rolling key used by server to encrypt outgoing S2C frames.
- **Key Mutation Function**:
  $$\text{NextKey}(K) = (\text{RotateLeft}(K, 1) \cdot 0x10A860C1u + 0xEE57FD49u) \pmod{2^{32}}$$

---

## 3. Protocol Validation Tables (`ProtocolSizeTable`)

The protocol size validation table enforces packet size constraints prior to payload dispatch:

| Opcode | Packet Name | Fixed / Variable | Valid Size (Payload Bytes) |
| :--- | :--- | :--- | :--- |
| `0x20` | `s2c_accountbegin` | Fixed | 42 |
| `0x41` | `c2s_accountlogin` / `s2c_accountlogin` | Fixed | 127 |
| `0x37` | `s2c_roleslist` | Variable | $\ge 12$ ($10 + 2 + 35 \times \text{RoleCount}$) |
| `0x45` | `c2s_createrole` | Variable | $21..45$ ($15 + \text{NameByteLen}$) |
| `0x3A` | `c2s_deleterole` | Fixed | 129 |
| `0x8C` | `s2c_rolenewdelresponse` | Fixed | 42 |
| `0x64` | `c2s_selectrole` | Fixed | 41 |
| `0x34` | `s2c_notifyplayerlogin` | Fixed | 60 |
| `0x42` | `c2s_logiclogin` | Fixed | 17 |
| `0x49` | `s2c_syncworld` | Fixed | 34 |
| `0x44` | `s2c_synccurplayer` | Fixed | 68 |
| `0x45` | `s2c_synccurplayerskill` | Fixed | 483 |
| `0x46` | `s2c_synccurplayernormal` | Fixed | 14 |
| `0x4C` | `s2c_syncnpcfull` | Variable | $\ge 35$ ($34 + \text{NameLen}$) |
| `0x4E` | `s2c_syncnpcminplayer` | Fixed | 13 |
| `0xA4` | `s2c_disabledfuncflag` | Fixed | 5 |
| `0x43` | `s2c_syncend` / `c2s_syncend` | Variable | 1 (S2C) / 9 (C2S) |
| `0x82` | `s2c_ping` | Fixed | 5 |
| `0x70` | `c2s_pingreply` | Fixed | 9 |
| `0x99` | `s2c_pingreply` | Fixed | 5 |
| `0x9B` | `c2s_closeconnection` | Fixed | 1 |

---

## 4. Boundary Encodings Representation

### 4.1 TCVN3 (ABC) Character Map
- Single-byte character set mapping Vietnamese lowercase vowels with diacritics (`à`, `á`, `ả`, `ã`, `ạ`, `â`, etc.) into bytes `0x80`..`0xFE`.
- Standard ASCII characters `0x20`..`0x7E` map 1:1.
- Null terminator `0x00` terminates all strings; bytes after `0x00` in fixed buffers are ignored.

### 4.2 KSG Password Digest
- 32-character uppercase hexadecimal ASCII string representing MD5 hash of user plaintext password (e.g., `"E10ADC3949BA59ABBE56E057F20F883E"`).
