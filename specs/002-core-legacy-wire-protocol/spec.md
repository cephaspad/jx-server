# Feature Specification: Core Legacy Wire Protocol & Ciphers

**Feature Directory**: `specs/002-core-legacy-wire-protocol`  
**Status**: Approved  
**Applies To**: Wire Framing, Cryptographic Stream Ciphers, Handshake Key Exchange, Secondary Obfuscation, and Boundary Character Encodings  

---

## 1. Executive Summary & Value Proposition

To achieve zero-modification compatibility with original game client executables (`game.exe`) and autonomous testing swarms (`jx-swarm`), the server must implement the exact binary wire protocol, cryptographic transformations, and boundary text encodings established by Kingsoft's historical networking layer.

The **Core Legacy Wire Protocol & Ciphers** specification defines the foundational wire transport layer:
1. **2-Byte Length-Prefixed Framing**: Streaming reassembly that isolates complete protocol frames from fragmented or batched TCP byte streams, validating payload bounds and preventing denial-of-service vulnerabilities.
2. **Initial Session Handshake (`0x20`)**: A 44-byte unencrypted preamble establishing obfuscated 32-bit cryptographic keys for independent client-to-server and server-to-client traffic streams.
3. **Rolling Symmetric Stream Cipher (`KSG_DecodeEncode`)**: In-place XOR-based stream encryption operating over a static 5,679-key substitution table, rotated deterministically per packet.
4. **Second-Layer Obfuscators**: Secondary 16-bit linear congruential generator (LCG) ciphers protecting sensitive character lists and role selection payloads against eavesdropping.
5. **Boundary-Only Text & Password Transcoding**: Bidirectional transcoding between legacy single-byte encodings (`TCVN3` and `GBK`) and standard Unicode (`UTF-8`), paired with 32-character uppercase MD5 password hashing.

---

## 2. User Stories & Acceptance Scenarios

### User Story 1 - Deterministic Frame Reassembly & Protocol Bounds Enforcement (Priority: P1)

As a network gateway or game server transport listener, I want to reassemble fragmented or batched incoming TCP stream bytes into complete, verified application frames based on a 2-byte length prefix and protocol size boundaries, so that malformed frames, incomplete buffers, and oversized denial-of-service attempts are isolated and rejected before reaching gameplay handlers.

**Why this priority**: Correct TCP stream framing is the mandatory gateway for all client-server communications; without robust boundary enforcement, socket connections crash or hang upon encountering network packet fragmentation.

**Independent Test**: Can be tested independently by feeding fragmented, coalesced, truncated, and oversized binary buffers into the stream processor, asserting that valid frames emerge in order, fragmented bytes are buffered until complete, and out-of-bounds frames trigger immediate disconnects.

**Acceptance Scenarios**:
1. **Given** a TCP stream delivering a packet split across multiple socket reads, **When** partial bytes arrive, **Then** the framing processor buffers incoming bytes without allocating redundant heap buffers until the full length indicated by the 2-byte header is reached.
2. **Given** a TCP stream delivering multiple coalesced frames in a single socket read, **When** processed, **Then** the processor parses each complete frame in strict sequential order.
3. **Given** a malformed frame declaring a length smaller than the protocol header or exceeding the maximum permissible frame threshold, **When** processed, **Then** the processor rejects the frame, flags the connection, and isolates the failure.

---

### User Story 2 - Bit-Exact Handshake & Rolling Stream Cipher Negotiation (Priority: P1)

As a connecting client or automated bot, I want the server to send an initial unencrypted 44-byte handshake preamble containing obfuscated session keys, and thereafter encrypt and decrypt all application frames in-place using the symmetric 5,679-entry key substitution table, so that communications remain synchronized, secure, and bit-for-bit compatible with the reference client.

**Why this priority**: The legacy client rejects connections and terminates immediately if the initial handshake structure, key deobfuscation math, or subsequent stream cipher transformations deviate by even a single bit.

**Independent Test**: Can be verified by running symmetric round-trip test vectors across varied payload lengths (1 byte to 1,024 bytes) against known reference byte sequences, confirming that encrypted payloads match historical vectors and decrypt back to the exact plaintext.

**Acceptance Scenarios**:
1. **Given** a newly accepted TCP connection, **When** initialized, **Then** the server transmits the 44-byte handshake packet containing obfuscated client and server keys.
2. **Given** an established session with agreed cryptographic keys, **When** a payload is transmitted in either direction, **Then** the stream cipher transforms the payload in-place using the static key table and rotates the session key deterministically.
3. **Given** any arbitrary binary payload, **When** encrypted and immediately decrypted using the matching key state, **Then** the resulting byte sequence matches the original input byte-for-byte.

---

### User Story 3 - Secondary Obfuscation & Boundary Encoding Transcoding (Priority: P2)

As an authentication service or character management coordinator, I want sensitive character roster lists and character selection packets to undergo secondary 16-bit linear congruential XOR masking, and all character names to transcode between legacy single-byte encodings and standard Unicode, so that characters with Vietnamese diacritics display identically to historical servers.

**Why this priority**: Character names and account credentials utilize legacy character sets; accurate transcoding is essential to prevent text corruption, question-mark replacement, or display glitches in legacy client user interfaces.

**Independent Test**: Can be tested by serializing character names containing complex Vietnamese diacritics into legacy byte buffers, applying secondary XOR obfuscation, deobfuscating, and decoding back to Unicode, confirming that the decoded string matches the source character-for-character.

**Acceptance Scenarios**:
1. **Given** a character roster packet containing character names and summary attributes, **When** prepared for wire transmission, **Then** the payload data section is masked with the secondary 16-bit XOR cipher.
2. **Given** an obfuscated character selection packet, **When** received from a client, **Then** the server deobfuscates the leading bytes using the secondary cipher before reading the character name.
3. **Given** character names encoded in legacy Vietnamese (`TCVN3`) or Chinese (`GBK`) format with null-byte padding, **When** transcoded at the network boundary, **Then** the server produces clean Unicode text with trailing nulls stripped, and reverse transcoding preserves exact byte parity.

---

## 3. Edge Cases

- **Zero-Length or 1-Byte Payloads**: A malicious or corrupt client sends a 2-byte length prefix declaring 0 or 1 byte total length; the processor rejects the frame since valid frames require at least an opcode byte.
- **Odd-Length Buffers in 32-Bit Cipher Loop**: The stream cipher operates primarily in 32-bit double-word chunks; payloads with lengths not divisible by 4 (1, 2, or 3 trailing bytes) must be processed through the exact 8-bit tail masking algorithm to prevent memory corruption or buffer overruns.
- **Single-Character Role Names in Secondary Cipher**: When a character name length equals 1 byte, the selection obfuscator must handle the zero-length obfuscation loop gracefully without negative loop boundary errors.
- **Corrupted Public Key Table**: If the static 5,679-key substitution table file is tampered with or truncated on disk, the system must fail validation immediately upon startup.
- **Mixed CJK and Vietnamese Byte Sequences**: When legacy text fields contain unmapped characters or combined byte sequences, boundary transcoders must fail safely without throwing unhandled exceptions.

---

## 4. Functional Requirements

- **FR-001**: The transport framing layer MUST parse incoming TCP streams using a 2-byte little-endian length prefix where `Length = PayloadLength + 2`, accepting valid frame sizes between 3 and 65,535 bytes.
- **FR-002**: The transport framing layer MUST enforce protocol size boundaries, rejecting frames whose declared length violates opcode-specific minimum or maximum constraints.
- **FR-003**: Upon accepting a new TCP connection, the server MUST transmit an unencrypted 44-byte handshake frame (`0x20`) carrying obfuscated 32-bit client and server keys.
- **FR-004**: Handshake key obfuscation and deobfuscation MUST execute byte-swapping of outer bytes 0 and 3, bitwise inversion, additive offset `0x2E6D2399`, and XOR masking with `0x2E6D23CF` with 100% mathematical fidelity.
- **FR-005**: All post-handshake frames MUST be encrypted and decrypted using the symmetric `KSG_DecodeEncode` stream cipher backed by the verified 5,679-entry public key substitution table (`SHA-256: 5491f08643c87d4ad6c08e3b5f44457acbc78a3cd0d288b6d6f4d99dafe66047`).
- **FR-006**: The stream cipher MUST rotate session keys per packet using the linear congruential transformation `uKey = RotateLeft(uKey, 1) * 0x10A860C1 + 0xEE57FD49`.
- **FR-007**: The transport layer MUST support secondary 16-bit XOR masking for character roster responses (`0x37`, seed `0x3042`) and character selection requests (`0x64`, seed `0x38BC`).
- **FR-008**: The encoding subsystem MUST provide lossless bidirectional transcoding between legacy single-byte Vietnamese encoding (`TCVN3`) and standard Unicode (`UTF-8`), correctly parsing and stripping null-terminator bytes.
- **FR-009**: The encoding subsystem MUST provide bidirectional transcoding for legacy Chinese encoding (`GBK` / CP936) for asset paths and protocol strings.
- **FR-010**: Passwords MUST be validated and processed as 32-character uppercase hexadecimal MD5 digests matching historical client conventions.

---

## 5. Key Entities

- **Wire Frame**: A length-prefixed binary message consisting of a 2-byte little-endian header followed by an encrypted or plaintext application payload starting with a 1-byte opcode.
- **Handshake Packet**: A 44-byte unencrypted packet (`AccountBeginS2CPacket`) establishing independent client and server session keys upon connection establishment.
- **Public Key Substitution Table**: An immutable array of 5,679 32-bit unsigned integers used by the stream cipher to derive per-dword masking keys.
- **Stream Cipher State**: A per-connection cryptographic state maintaining the current 32-bit rolling key and packet sequence for in-place payload transformation.
- **Secondary Masking State**: A temporary 16-bit linear congruential generator used to obfuscate specific payload regions.
- **Boundary Transcoder**: A stateless text converter translating between legacy character sets (`TCVN3`, `GBK`) and Unicode strings at network and file boundaries.

---

## 6. Success Criteria

- **SC-001**: 100% of symmetric cipher and handshake test vectors pass automated unit verification with byte-for-byte exactness.
- **SC-002**: The stream framing processor reassembles fragmented and coalesced TCP frames with zero data loss and rejects malformed frames with immediate connection isolation.
- **SC-003**: In-place stream cipher encryption and decryption achieves zero additional heap allocations per frame.
- **SC-004**: Boundary text transcoding round-trips 100% of standard Vietnamese character names between `TCVN3` and `UTF-8` without character distortion or mojibake.
- **SC-005**: Both reference game clients (`game.exe`) and bot swarms (`jx-swarm`) establish encrypted sessions without framing or cryptographic negotiation failures.
