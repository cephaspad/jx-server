# Tasks: Core Legacy Wire Protocol & Ciphers

**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  

---

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (`[US1]`, `[US2]`, `[US3]`)
- Every task specifies the exact target file path(s) in `d:\Projects\jx-server`

---

## Phase 1: Setup & Cryptographic Fixtures

- [X] T001 [P] [US2] Embed the verified 5,679-entry public key binary table (`22,716` bytes, SHA-256 `5491f08643c87d4ad6c08e3b5f44457acbc78a3cd0d288b6d6f4d99dafe66047`) in `docs/fixtures/keys/g_uPublicKeys.bin`
- [X] T002 [P] [US1] Define authoritative legacy opcode constants in `src/JXServer.Adapter.LegacyJX1/Packets/ProtocolOpcodes.cs`
- [X] T003 [P] [US1] Implement opcode size and boundary validation table in `src/JXServer.Adapter.LegacyJX1/Framing/ProtocolSizeTable.cs`

---

## Phase 2: TCP Stream Framing (`System.IO.Pipelines`)

- [X] T004 [P] [US1] Implement 2-byte length-prefixed streaming frame parser and writer `JxFramePipeProcessor` in `src/JXServer.Adapter.LegacyJX1/Framing/JxFramePipeProcessor.cs`
- [X] T005 [P] [US2] Implement unencrypted 44-byte handshake frame structure `AccountBeginS2CPacket` (`0x20`) in `src/JXServer.Adapter.LegacyJX1/Packets/AccountBeginS2CPacket.cs`

---

## Phase 3: Cryptographic Ciphers & Obfuscators

- [X] T006 [P] [US2] Implement immutable public key table loader `KsgPublicKeyTable` with SHA-256 integrity verification in `src/JXServer.Adapter.LegacyJX1/Ciphers/KsgPublicKeyTable.cs`
- [X] T007 [P] [US2] Implement 32-bit handshake key obfuscation and deobfuscation math in `src/JXServer.Adapter.LegacyJX1/Ciphers/HandshakeKeyObfuscator.cs`
- [X] T008 [P] [US2] Implement symmetric `KSG_DecodeEncode` 32-bit dword and 8-bit tail stream cipher in `src/JXServer.Adapter.LegacyJX1/Ciphers/KsgStreamCipher.cs`
- [X] T009 [P] [US3] Implement secondary 16-bit linear congruential XOR ciphers (`0x3042` and `0x38BC`) in `src/JXServer.Adapter.LegacyJX1/Ciphers/SecondLayerXorCipher.cs`

---

## Phase 4: Boundary Character Encodings & Digest Helpers

- [X] T010 [P] [US3] Implement bidirectional single-byte `TCVN3` $\leftrightarrow$ `UTF-8` transcoder with fixed-buffer padding in `src/JXServer.Primitives/Encodings/Tcvn3Encoding.cs`
- [X] T011 [P] [US3] Implement `GBK` (CP936) encoding helper for CJK asset paths and strings in `src/JXServer.Primitives/Encodings/GbkEncoding.cs`
- [X] T012 [P] [US3] Implement 32-character uppercase MD5 password hasher and validator `KsgPasswordHasher` in `src/JXServer.Primitives/Encodings/KsgPasswordHasher.cs`

---

## Phase 5: Verification & Unit Tests

- [X] T013 [P] [US1] Write unit tests for fragmented, batched, and malformed frame handling in `tests/JXServer.Protocol.Tests/FramingAndCipherTests.cs`
- [X] T014 [P] [US2] Write unit tests verifying round-trip `KSG_DecodeEncode` across all byte alignments (1, 4, 9, 17, 41, 127, 483 bytes) in `tests/JXServer.Protocol.Tests/FramingAndCipherTests.cs`
- [X] T015 [P] [US2] Write unit tests verifying `AccountBeginS2CPacket` handshake key obfuscation round-trip in `tests/JXServer.Protocol.Tests/FramingAndCipherTests.cs`
- [X] T016 [P] [US3] Write unit tests verifying `SecondLayerXorCipher` for role list and role selection name masking in `tests/JXServer.Protocol.Tests/FramingAndCipherTests.cs`
- [X] T017 [P] [US3] Write unit tests for Vietnamese diacritic string encoding and null-termination stripping in `tests/JXServer.Protocol.Tests/FramingAndCipherTests.cs`
