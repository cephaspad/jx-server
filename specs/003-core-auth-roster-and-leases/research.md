# Research & Ground-Truth Rationale: Core Authentication, Character Roster & Session Leases

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  
**Ground-Truth Sources**: `jx-swarm/docs/01-session-lifecycle/bishop-gateway.md`, `SwordOnline/Sources/Core/Src/KAccountUser.cpp`, `KRoleUser.cpp`  

---

## 1. Architectural Decisions & Ground-Truth Findings

### Decision 1: Monotonic Session Lease-Epoch Fencing
- **Problem**: In legacy JXLinux (`bishop_y` and `goddess_y`), player sessions were tracked using mutable in-memory flags without fencing tokens. If a client disconnected without sending a TCP close frame (`0x9B`), the gateway remained locked until a slow 5-minute timeout elapsed. Reconnecting users were greeted with `LOGIN_R_ACCOUNT_EXIST` (`0x00050004`), creating massive frustration and automated test failures.
- **Selected Solution**:
  - Each account maintains an authoritative 64-bit integer `LeaseEpoch` initialized to `1`.
  - When a new login request (`0x41`) arrives with `bKickOnline != 0`, the server increments `LeaseEpoch`, invalidates any prior session tickets, signals the active session to disconnect and save, and immediately grants access to the new session.
  - When `bKickOnline == 0` and an active lease is verified, the server preserves the active session and returns `0x00050004`.
  - All operations for an account are serialized under a per-account key lock (`SemaphoreSlim`), completely eliminating dual-login race conditions.

### Decision 2: 3-Slot Character Roster Data Layout (`0x37`)
- **Legacy Layout**:
  - Opcode `0x37` (`s2c_roleslist`) contains a header followed by `RoleCount` (1 byte), `RefuseCount` (1 byte, always `0`), and $N$ character entries of exactly 35 bytes each:
    - `szName[32]` (32-byte TCVN3 null-terminated character name)
    - `Sex` (1 byte: `0`=Male, `1`=Female)
    - `Series` (1 byte: `0..4`)
    - `Level` (1 byte: `1..255`)
  - The entire data section starting at offset `+0x0A` is encrypted with the secondary 16-bit linear congruential XOR cipher (initial seed `0x3042`).
  - Maximum 3 characters per account (`MaxRolesPerAccount = 3`).

### Decision 3: Character Creation Validation & Immediate World Handoff
- **Creation Packet (`0x45`, `c2s_createrole`)**:
  - Carries `RoleNo` (gender), `Series` (element), `NativePlaceId` (starter village map ID: `53`, `20`, `99`, `100`, `101`, `121`, `153`, `174`), and null-terminated character name (`6..30` TCVN3 bytes).
  - Validates character name uniqueness across the entire server, verifies against prohibited word filter (`goddess_rolename.flt`), and validates that the account currently has fewer than 3 characters.
- **Response Sequence**:
  - On success, the gateway sends `RoleNewDelResponseS2CPacket` (`0x8C` with `bSucceeded = 1`), immediately followed by `NotifyPlayerLoginS2CPacket` (`0x34` with `bPermit = 1`) carrying the single-use 16-byte handoff ticket and game server endpoint.
  - This matches the legacy client behavior where a newly created character enters the game world directly without returning to the login menu.

### Decision 4: Character Deletion Security & Immediate Name Reclamation
- **Deletion Packet (`0x3A`, `c2s_deleterole`)**:
  - Carries username, password digest, and character name.
  - Verification: If the account has configured a secondary password (`SecondaryPasswordHashUpperMd5`), the submitted digest must match it. If no secondary password has been configured, the primary account password authorizes deletion.
  - Deletion Semantics: The character is permanently removed from the database (`ICharacterRepository.DeleteAsync`), immediately freeing both the slot index (`0..2`) and the global character name for reuse.

### Decision 5: Unloaded Starter Village Fallback Policy
- **Clarification Resolution**:
  - If a player creates a character or logs into a character stationed in a starter village whose map is not enabled in `worldset.ini`:
    - When `FallbackUnloadedStarterVillage = true`, the server redirects the character to Ba Lăng Huyện (Map 53).
    - When `FallbackUnloadedStarterVillage = false`, the server rejects the entry (`bSucceeded = 0` / `bPermit = 0`).
