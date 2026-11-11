# Contract 01: TCP Wire Framing, Handshake & Stream Ciphers

**Feature**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)  
**Ground-Truth Reference**: `jx-swarm/docs/00-network-core/framing-and-cipher.md`, `SwordOnline/Sources/Core/Src/KProtocol.cpp`  

---

## 1. Outer TCP Frame Contract

- **Header**: 2-byte Little-Endian unsigned integer `wFrameLength`.
- **Length Relationship**:
  $$\text{wFrameLength} = \text{PayloadLength} + 2$$
- **Boundaries**:
  - Minimum wire length: 3 bytes (`PayloadLength = 1`, single opcode byte).
  - Maximum standard wire length: 65,535 bytes (`PayloadLength = 65,533`).
- **Processing Semantics (`JxFramePipeProcessor`)**:
  - If available bytes $< 2$, wait for more data.
  - Read `wFrameLength`. If available bytes $< \text{wFrameLength}$, wait for more data.
  - Slice exactly `PayloadLength` bytes starting at offset 2.
  - Advance pipe reader cursor past `wFrameLength`.

---

## 2. Handshake Protocol Contract (`AccountBeginS2CPacket`, `0x20`)

- **Payload Size**: Exactly 42 bytes (44 bytes total on wire).
- **Encryption**: Strictly unencrypted (plaintext).
- **Byte Offsets**:
  - `+0x00` (`1B`): `0x20` (opcode)
  - `+0x01` (`1B`): `0x00` (reserved)
  - `+0x02` (`6B`): Pseudo-random padding
  - `+0x08` (`4B`): `ObfuscatedClientKey` (Little-Endian uint32)
  - `+0x0C` (`5B`): Pseudo-random padding
  - `+0x11` (`4B`): `ObfuscatedServerKey` (Little-Endian uint32)
  - `+0x15` (`21B`): Pseudo-random padding

### Key Obfuscation Mathematical Formula
Given a 32-bit integer $K$ with bytes $(b_0, b_1, b_2, b_3)$ in little-endian order:
1. Outer Byte Swap:
   $$K_{swap} = (b_3 \ll 24) \mid (b_1 \ll 8) \mid (b_2 \ll 16) \mid b_0$$
2. Inversion:
   $$K_{inv} = \sim K_{swap}$$
3. Additive Offset and XOR:
   $$K_{obfuscated} = (K_{inv} + 0x2E6D2399u) \oplus 0x2E6D23CFu$$

---

## 3. `KSG_DecodeEncode` Stream Cipher Contract

- **Public Key Table**: 5,679 uint32 values loaded from `docs/fixtures/keys/g_uPublicKeys.bin` (`SHA-256: 5491f08643c87d4ad6c08e3b5f44457acbc78a3cd0d288b6d6f4d99dafe66047`).
- **Main 32-Bit Loop**:
  - Iterates $ebx$ from $(\text{DwordCount} - 1)$ down to $0$.
  - Index calculation:
    $$uIndex = (ebx + uWorkingKey) \pmod{5679}$$
  - Mask derivation:
    $$uWorkingKey = g\_uPublicKeys[uIndex] + 0x2E6D23C1u$$
  - In-place XOR on little-endian 32-bit dword:
    $$\text{Dword}_{offset} \oplus= uWorkingKey$$
- **Tail 8-Bit Loop**:
  - If $\text{RemainCount} = \text{Length} \& 3 \ne 0$:
    $$uTailIndex = \text{RemainCount} \pmod{5679}$$
    $$uTailMask = g\_uPublicKeys[uTailIndex] \oplus uWorkingKey$$
  - In-place XOR for each trailing byte $i \in [0, \text{RemainCount} - 1]$:
    $$\text{Byte}_{offset + i} \oplus= (uTailMask \gg (8 \cdot i)) \& 0xFFu$$
- **Packet Key Mutation**:
  $$uKey = (\text{RotateLeft}(uKey, 1) \cdot 0x10A860C1u + 0xEE57FD49u) \pmod{2^{32}}$$

---

## 4. Second-Layer 16-Bit XOR Obfuscators

### 4.1 Role List Obfuscator (`0x37`)
- Initial seed: `0x3042`
- Applied to the data payload starting at byte offset 10 (`RoleCount` + `RoleEntries`):
  ```csharp
  ushort seed = 0x3042;
  for (int i = 0; i < length - 1; i += 2)
  {
      ushort word = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(i, 2));
      BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(i, 2), (ushort)(word ^ seed));
      seed = unchecked((ushort)(seed * 31 + 0x0805));
  }
  ```

### 4.2 Role Selection Name Obfuscator (`0x64`)
- Initial seed: `0x38BC`
- Applied to the first $nNameLen$ bytes of the role name buffer:
  ```csharp
  ushort seed = 0x38BC;
  for (int i = 0; i < nNameLen; i++)
  {
      span[i] ^= (byte)(seed & 0xFF);
      seed = unchecked((ushort)(seed * 31 + 0x0805));
  }
  ```
