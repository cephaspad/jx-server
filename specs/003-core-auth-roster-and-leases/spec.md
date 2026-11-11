# Feature Specification: Core Authentication, Character Roster & Session Leases

**Feature Directory**: `specs/003-core-auth-roster-and-leases`  
**Status**: Approved  
**Applies To**: Bishop Login Gateway (`TCP Port 5622`), Account Authentication, 3-Slot Character Lifecycle, Monotonic Session Leases, and World Handoff Tickets  

---

## 1. Executive Summary & Value Proposition

In the legacy architecture, the Bishop gateway and Goddess database coordinator suffered from frequent operational deadlocks: client network drops left session locks active in memory, trapping accounts in perpetual "Account is already online" (`0x00050004`) states that required manual operator intervention or complete server restarts. Furthermore, simultaneous dual logins could trigger database race conditions leading to character duplication or corrupted state snapshots.

The **Core Authentication, Character Roster & Session Leases** specification establishes a robust control-plane authentication and session lifecycle architecture:
1. **Bishop Gateway Endpoint (`TCP 5622`)**: Authenticates legacy client credentials (`0x41`), supports automated provisioning for testing environments, and enforces protocol version checks.
2. **3-Slot Character Lifecycle Management**: Supports querying character rosters (`0x37`), creating new characters (`0x45`) across the 5 elemental series and configured starter villages with prohibited name filtering, and deleting unwanted characters (`0x3A`) with secondary password verification.
3. **Monotonic Session Lease-Epoch Fencing**: A distributed lease coordinator enforcing monotonic epochs per account, executing immediate eviction and state persistence upon forced re-login (`bKickOnline = 1`), and completely eliminating stuck-account conditions.
4. **Single-Use World Handoff Tickets**: Generates cryptographically secure, time-bounded (30-second TTL) 16-byte handoff tickets (`0x34`) that authorize seamless transition to game world instances.

---

## 2. User Stories & Acceptance Scenarios

### User Story 1 - Account Authentication & Character Roster Presentation (Priority: P1)

As a player or autonomous bot connecting to the Bishop gateway, I want to authenticate with my account credentials and receive my account's character roster, so that I can review my characters and select one to play.

**Why this priority**: Account authentication is the mandatory access control gate protecting player profiles and authorizing access to the game world.

**Independent Test**: Can be tested by connecting a client to port 5622, submitting valid and invalid account credentials, verifying that valid credentials return login confirmation and the account's character roster (up to 3 characters) with accurate names, series, genders, and levels, while invalid credentials return exact rejection codes.

**Acceptance Scenarios**:
1. **Given** an existing active account, **When** the client submits a valid username and password digest via `0x41`, **Then** the gateway confirms login success and returns the character roster (`0x37`) formatted with secondary XOR obfuscation.
2. **Given** a login attempt with an incorrect password, frozen account status, or incompatible protocol version, **When** processed, **Then** the gateway returns the corresponding rejection code (`0x00050003`, `0x0005000B`, or `0x0005000A`) and denies access to the roster.
3. **Given** development or swarm testing mode with auto-provisioning enabled, **When** a non-existent username logs in, **Then** the gateway automatically persists a new active account with the submitted password digest and returns an empty roster.

---

### User Story 2 - Character Creation, Starter Village Allocation & Deletion (Priority: P1)

As an authenticated user with fewer than 3 characters, I want to create a new character by choosing a unique name, gender, elemental series, and starter village, or delete an existing character by verifying my security credentials, so that I can manage my account's character roster.

**Why this priority**: New accounts start with zero characters; character creation and deletion are essential for player onboarding and roster management.

**Independent Test**: Can be validated by creating characters across all 5 elemental series, asserting that initial stats, vitals, and starter skills match canonical templates, verifying duplicate name rejection, and executing character deletion with immediate slot and name reclamation.

**Acceptance Scenarios**:
1. **Given** an account with fewer than 3 characters, **When** submitting a character creation request (`0x45`) with a valid unique name and allowed series/village, **Then** the server persists the new character, confirms creation success (`0x8C`), and issues a world handoff ticket (`0x34`).
2. **Given** a creation request with a name that already exists, exceeds length limits, or matches prohibited word filters, **When** submitted, **Then** the server rejects the request with failure status and leaves the account roster unchanged.
3. **Given** an account requesting character deletion (`0x3A`), **When** the submitted password matches the account's secondary password (or authenticated primary password if unset), **Then** the server permanently removes the character and immediately frees the name and slot for reuse.

---

### User Story 3 - Monotonic Lease Fencing & Stuck-Account Elimination (Priority: P1)

As a player reconnecting after an abrupt network drop or logging in with the kick-online option enabled, I want the server to advance my account's lease epoch, immediately evict the lingering session, and issue a fresh handoff ticket, so that I can re-enter the game world immediately without getting stuck online.

**Why this priority**: Eliminating stuck accounts is a primary operational requirement for player retention and uninterrupted automated testing swarms.

**Independent Test**: Can be tested by establishing an active world session, initiating a second login with `bKickOnline = 1`, and verifying that the old connection is terminated, latest character state is persisted, and the new session enters the world within 3 seconds.

**Acceptance Scenarios**:
1. **Given** an account with an active session, **When** a new login arrives with `bKickOnline = 1`, **Then** the server increments the account's lease epoch, closes the prior connection, and grants access to the new session.
2. **Given** an active session, **When** a new login arrives with `bKickOnline = 0`, **Then** the server preserves the existing session and rejects the new login with `0x00050004` (`AccountAlreadyOnline`).
3. **Given** two simultaneous login requests for the exact same account arriving in the same millisecond, **When** processed, **Then** the session coordinator serializes the requests, granting the newest epoch to one and rejecting or kicking the other, ensuring two instances of the character never exist concurrently.

---

## 3. Edge Cases

- **Orphaned Handoff Ticket**: A client selects a character and receives a handoff ticket (`0x34`) but drops connectivity before connecting to the game server; the ticket expires after 30 seconds, automatically freeing the session lease.
- **Delayed Stale Ticket Presentation**: A client attempts to redeem an expired or superseded handoff ticket after a re-login has already occurred; the game server verifies the ticket's lease epoch and rejects the stale ticket.
- **Secondary Password Absent vs Configured**: If an account has never configured a secondary password, character deletion validates against the primary account password; once configured, only the valid secondary password digest authorizes deletion.
- **Unloaded Starter Village Fallback**: A client requests character creation in a starter village whose map is currently inactive; if fallback mode is enabled, the server redirects the character to Ba Lăng Huyện (Map 53); otherwise, creation is rejected.

---

## 4. Functional Requirements

- **FR-001**: The Bishop gateway MUST listen on TCP port 5622 and execute framing, handshake, and stream cipher negotiation per Spec 002.
- **FR-002**: The gateway MUST validate account login requests (`0x41`), verifying protocol version (`0x00000041`), username (`1..31` bytes), and 32-character uppercase MD5 password digest.
- **FR-003**: The gateway MUST support an auto-provisioning mode that automatically creates and persists new accounts upon first login when enabled, while strictly rejecting unknown accounts when disabled.
- **FR-004**: Upon successful authentication, the gateway MUST return the account's character roster (`0x37`) containing up to 3 character summaries, obfuscated using the secondary 16-bit XOR cipher (seed `0x3042`).
- **FR-005**: The gateway MUST process character creation requests (`0x45`), enforcing a 3-character slot limit, global name uniqueness, byte length (`6..30` TCVN3 bytes), prohibited name filters, and 5-element series compatibility (`0..4`).
- **FR-006**: Upon successful character creation, the server MUST initialize the character from canonical templates, persist the entity, confirm creation (`0x8C`), and issue a world handoff ticket (`0x34`).
- **FR-007**: The gateway MUST process character deletion requests (`0x3A`), verifying secondary or primary password digests, permanently deleting the character, and freeing the name and slot immediately.
- **FR-008**: When an authenticated user selects a character (`0x64`), the gateway MUST verify that the target map is valid, allocate a monotonic session lease epoch, generate a single-use 16-byte handoff ticket with a 30-second TTL, and return the world server endpoint via `0x34`.
- **FR-009**: The session coordinator MUST enforce monotonic lease epochs per account: when `bKickOnline = 1`, the coordinator increments the epoch, notifies active sessions to evict and save, and admits the new session.
- **FR-010**: When `bKickOnline = 0` and an active lease exists, the gateway MUST reject the new login with `LOGIN_R_ACCOUNT_EXIST` (`0x00050004`).
- **FR-011**: All simultaneous login requests for the same account MUST be strictly serialized using per-account synchronization locks to prevent concurrent lease races.

---

## 5. Key Entities

- **Account Aggregate**: Manages account credentials, primary/secondary password digests, account status (`Active`, `Frozen`, `Locked`), and current authoritative lease epoch.
- **Character Aggregate**: Represents a playable character owned by an account (up to 3 per account), encapsulating role name, gender, series, level, coordinates, vitals, attributes, and learned skills.
- **SessionLease Aggregate**: Represents active ownership and lease-epoch fencing for an account across gateway and world states, managing the single-use 16-byte handoff ticket and expiration timestamps.
- **World Handoff Ticket**: A single-use 16-byte cryptographic GUID bound to an account, character, target map, and lease epoch, valid for 30 seconds.

---

## 6. Success Criteria

- **SC-001**: 100 concurrent bot accounts from `jx-swarm` authenticate, create characters, and receive world handoff tickets on port 5622 in under 3 seconds with zero protocol framing errors.
- **SC-002**: 100% of forced re-logins (`bKickOnline = 1`) cleanly evict superseded sessions and allow new world entry within 3 seconds, achieving zero stuck accounts.
- **SC-003**: Simultaneous dual-login races for identical accounts result in exactly one admitted session with a higher lease epoch, with zero duplicate character instances or database record corruptions.
- **SC-004**: Character creation and deletion operations permanently free and reallocate slots and character names with 100% immediate consistency.
