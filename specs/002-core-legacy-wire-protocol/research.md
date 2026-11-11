# Research & Ground-Truth Rationale: Core Legacy Wire Protocol & Ciphers

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  
**Ground-Truth Sources**: `jx-swarm/docs/00-network-core/framing-and-cipher.md`, `SwordOnline/Sources/Core/Src/KProtocol.cpp`, `KPakFile.cpp`  

---

## 1. Architectural Decisions & Cryptographic Analysis

### Decision 1: Outer TCP Length-Prefixed Framing (`wFrameLength = PayloadLength + 2`)
- **Legacy Finding**: In Kingsoft's network architecture, every TCP frame begins with a 2-byte Little-Endian unsigned integer `wFrameLength`. Crucially, `wFrameLength` includes its own 2-byte header size.
  - Formula: $\text{PayloadLength} = \text{wFrameLength} - 2$.
  - Minimum valid frame size is 3 bytes (2-byte header + 1-byte opcode).
  - Maximum standard frame size is 65,535 bytes.
- **Pipeline Implementation**: Using `System.IO.Pipelines.PipeReader` ensures incoming fragmented socket data is assembled into complete frames with zero memory copies, advancing the reader cursor only when full frames are consumed.

### Decision 2: 44-Byte Unencrypted Handshake (`AccountBeginS2CPacket`, Opcode `0x20`)
- **Wire Structure**:
  - `+0x00` (`2B` uint16): Wire frame length (`44`).
  - `+0x02` (`1B` uint8): Opcode `0x20` (`s2c_accountbegin`).
  - `+0x03` (`1B` uint8): `0x00`.
  - `+0x04` (`6B`): Random padding bytes.
  - `+0x0A` (`4B` uint32): `ObfuscatedClientKey` (used by server to decrypt incoming C2S frames).
  - `+0x0E` (`5B`): Random padding bytes.
  - `+0x13` (`4B` uint32): `ObfuscatedServerKey` (used by server to encrypt outgoing S2C frames).
  - `+0x17` (`21B`): Random padding bytes.
- **Obfuscation Formula**:
  - Plaintext key $K$ undergoes outer byte swap (bytes 0 and 3):
    $K_{swapped} = ((K \gg 24) \& 0xFF) \mid (K \& 0x00FFFF00) \mid ((K \ll 24) \& 0xFF000000)$
  - Bitwise NOT: $K_{inverted} = \sim K_{swapped}$
  - Additive constant and XOR mask: $K_{obfuscated} = (K_{inverted} + 0x2E6D2399) \oplus 0x2E6D23CF$
- **Deobfuscation Formula**:
  - $T = \sim ((K_{obfuscated} \oplus 0x2E6D23CF) - 0x2E6D2399)$
  - $K_{plain} = ((T \gg 24) \& 0xFF) \mid (T \& 0x00FFFF00) \mid ((T \ll 24) \& 0xFF000000)$

### Decision 3: Rolling Symmetric Stream Cipher (`KSG_DecodeEncode`)
- **Key Table**: Backed by `g_uPublicKeys`, containing 5,679 32-bit unsigned integers (22,716 bytes, SHA-256 `5491f08643c87d4ad6c08e3b5f44457acbc78a3cd0d288b6d6f4d99dafe66047`).
- **Algorithm**:
  1. **32-Bit Double-Word Loop**: Processes `uDwordCount = Length >> 2` in reverse countdown order (`(uDwordCount - 1) down to 0`):
     - Index: $idx = (ebx + uWorkingKey) \pmod{5679}$
     - Key derivation: $uWorkingKey = g\_uPublicKeys[idx] + 0x2E6D23C1$
     - XOR transformation: $Dword_{out} = Dword_{in} \oplus uWorkingKey$
  2. **8-Bit Tail Loop**: If `uRemainCount = Length & 3` is non-zero:
     - Tail mask derivation: $uTailMask = g\_uPublicKeys[uRemainCount \pmod{5679}] \oplus uWorkingKey$
     - Applies remaining 1 to 3 bytes: $Byte_i \oplus= (uTailMask \gg (8 \cdot i)) \& 0xFF$
  3. **Per-Packet Key Mutation**: After encrypting/decrypting each packet:
     $uKey = \text{RotateLeft}(uKey, 1) \cdot 0x10A860C1 + 0xEE57FD49$

### Decision 4: Second-Layer Linear Congruential XOR Ciphers
- **Role List Obfuscation (`0x37`)**:
  - Initial 16-bit seed $S = 0x3042$.
  - For each 16-bit word $W$ in the role data payload:
    $W \oplus= S$
    $S = (S \cdot 31 + 0x0805) \& 0xFFFF$
- **Role Selection Obfuscation (`0x64`)**:
  - Initial 16-bit seed $S = 0x38BC$.
  - For each byte $B$ from index 0 to $nNameLen - 1$:
    $B \oplus= (S \& 0xFF)$
    $S = (S \cdot 31 + 0x0805) \& 0xFFFF$

### Decision 5: Boundary Encodings (`TCVN3` & `GBK`)
- **TCVN3 (ABC Standard)**: Single-byte character set used in Vietnamese client builds. Non-ASCII vowels with accents occupy byte values `0x80` through `0xFE`. The server converts these to standard UTF-8 characters upon receiving packets and converts back upon sending.
- **Null-Termination Handling**: Legacy fixed-length string buffers (e.g., 32-byte role name buffers) must be read up to the first null byte (`0x00`), ignoring trailing uninitialized buffer junk.
- **GBK (CP936)**: Used for CJK asset paths, NPC headers, and Chinese client builds.
