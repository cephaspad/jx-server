# Feature Specification: Foundational Gateway Authentication, Character Lifecycle & World Entry

**Feature Branch**: `001-gateway-world-entry`

**Created**: 2026-11-08

**Status**: Draft

**Input**: User description: "Milestone 1: Foundational static world asset loading, gateway account authentication, character roster management (list, create across starter villages and five elemental series, delete, select), lease-fenced session handoff eliminating stuck accounts, and initial world map entry with continuous heartbeat synchronization compatible with the original game client and autonomous bots."

## Clarifications

### Session 2026-11-08

- Q: Khi bật chế độ tự động cấp phát tài khoản (auto-provisioning) phục vụ phát triển và kiểm thử `jx-swarm`, hệ thống Gateway cần xử lý như thế nào khi nhận yêu cầu đăng nhập (`0x41`) của một tên tài khoản chưa tồn tại trong cơ sở dữ liệu? (FR-003) → A: Option A — Tự động tạo mới và lưu tài khoản kèm mã băm mật khẩu MD5 ngay ở lần đăng nhập đầu tiên; các lần đăng nhập tiếp theo kiểm tra mật khẩu khớp với bản ghi đã lưu.
- Q: Khi người chơi hoặc bot gửi yêu cầu xóa nhân vật (`0x3A`, có chứa trường mật khẩu cấp 2 băm MD5 và tên nhân vật), hệ thống cần kiểm tra mật khẩu xóa và xử lý dữ liệu/tên nhân vật đã xóa như thế nào? (FR-006) → A: Option A — Kiểm tra mật khẩu cấp 2 nếu tài khoản đã cài đặt (nếu chưa cài thì chấp nhận mật khẩu chính/phiên đã xác thực), xóa nhân vật và giải phóng ngay cả ô trống (slot) lẫn tên nhân vật để có thể tái sử dụng ngay.
- Q: Khi một tài khoản đang có phiên hoạt động (ở Gateway hoặc trong Bản đồ) mà nhận được một yêu cầu đăng nhập mới hợp lệ (`0x41`), hệ thống sẽ quyết định giữa việc đá phiên cũ (`Lease Takeover`) và trả về mã lỗi "Tài khoản đang online" (`LOGIN_R_ACCOUNT_EXIST` `0x00050004`) như thế nào? (FR-008) → A: Option A — Xử lý theo cờ `bKickOnline` (`+0x6F`): nếu `bKickOnline != 0` thì tăng `Lease Epoch`, thu hồi/lưu phiên cũ ngay và cho phiên mới vào; nếu `bKickOnline == 0` thì từ chối phiên mới với mã `LOGIN_R_ACCOUNT_EXIST` (`0x00050004`).
- Q: Khi tạo nhân vật mới (`0x45`) hoặc chọn nhân vật vào game (`0x64`), nếu thôn tân thủ (`m_NativePlaceId`) hoặc bản đồ đang đứng của nhân vật là một thôn hợp lệ trong cấu hình gốc nhưng hiện chưa được bật trong `worldset.ini` của máy chủ, hệ thống cần xử lý như thế nào? (FR-005) → A: Option A — Cho phép vào thẳng nếu bản đồ đó đang được nạp trong `worldset.ini`; nếu bản đồ hợp lệ nhưng chưa được nạp thì hỗ trợ cấu hình tự động chuyển hướng (fallback) về thôn mặc định đang mở (Ba Lăng Huyện `Map 53`) hoặc từ chối (`bSucceeded = 0` / `bPermit = 0`) khi tắt chế độ fallback.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gateway Authentication, Character Selection & World Entry (Priority: P1)

A player using the original game client (or an autonomous headless bot) connects to the login gateway, authenticates with an account username and password, receives their existing character roster, selects a character to enter the game, transitions seamlessly to the world server using the issued session handoff ticket, receives complete initial world, vital, and skill synchronization at their character's map and coordinates, and remains online with continuous heartbeat verification.

**Why this priority**: Authenticating an account, selecting a character, and entering an active map without connection drops or protocol mismatches is the foundational entry point required before any in-game movement, combat, or social interaction can occur.

**Independent Test**: Can be fully tested end-to-end by launching the server with a provisioned account containing an existing character, connecting via either the original game client or a single autonomous bot session, selecting the character, and verifying that the character stands at the expected map coordinates with accurate health, mana, stamina, and skill list while maintaining an uninterrupted heartbeat for at least 5 minutes.

**Acceptance Scenarios**:

1. **Given** a registered active account with at least one existing character, **When** the client connects to the gateway and submits valid credentials, **Then** the gateway establishes an encrypted session, confirms login success, and returns the account's character roster (up to 3 characters with name, level, elemental series, gender, and appearance summary).
2. **Given** an authenticated gateway session with a displayed character roster, **When** the client selects one of the account's characters, **Then** the server allocates a world session lease, issues a handoff notification containing the world server address, port, and single-use session ticket, and awaits the client's arrival on the world server.
3. **Given** a valid, unexpired session handoff ticket, **When** the client connects to the world server and presents the ticket, **Then** the world server attaches the character to its designated map instance, transmits the map identity, character vitals, base attributes, learned skills, and synchronization completion signal, and begins regular heartbeat exchanges.
4. **Given** an unauthenticated client or a client submitting an invalid password, frozen account, or incompatible protocol version, **When** the client attempts to log in at the gateway, **Then** the gateway returns the exact corresponding rejection code and denies access to the character roster.

---

### User Story 2 - Character Creation, Starter Village Assignment & Deletion (Priority: P2)

A player or autonomous bot logging into an account with fewer than 3 characters creates a new character by specifying a valid Vietnamese character name, gender, elemental series (Kim, Mộc, Thủy, Hỏa, or Thổ), and starter village (such as Ba Lăng Huyện), immediately entering the game world at that village's valid spawn coordinates with authentic level-1 attributes and base skills, or deletes an unwanted character from the roster.

**Why this priority**: New accounts start with zero characters; without character creation and starter village initialization, neither human players nor newly provisioned bot swarms can enter the game world for the first time.

**Independent Test**: Can be tested independently by logging into a fresh account with zero characters, submitting a character creation request for each of the five elemental series, verifying the new character spawns inside the chosen starter village with authentic initial attributes and persisted state, and verifying character deletion removes the character from the roster.

**Acceptance Scenarios**:

1. **Given** an authenticated account with fewer than 3 existing characters, **When** the client submits a character creation request with a unique, valid name, compatible gender and elemental series, and a configured starter village, **Then** the server initializes the character from the corresponding elemental template, persists the new character, confirms creation success, and automatically issues a world server handoff ticket so the character enters the starter village.
2. **Given** an authenticated account attempting to create a character, **When** the submitted character name already exists on the server, violates length constraints, or contains prohibited filter terms, **Then** the server rejects the creation request with a specific failure indicator and preserves the account's existing roster unchanged.
3. **Given** an authenticated account with an existing character, **When** the client submits a valid character deletion request matching the account's secondary password (or primary password/authenticated session if no secondary password is set), **Then** the server permanently removes the character from the active roster, confirms deletion success, and immediately frees both the character slot and the character name for future creation.

---

### User Story 3 - Lease-Fenced Session Takeover & Stuck-Account Elimination (Priority: P3)

When a player or bot experiences an abrupt network disconnect or logs in again with the "kick existing session" option enabled while a prior session is still lingering, the server atomically increments the account's session lease epoch, immediately terminates and saves the superseded session, and allows the new login to proceed cleanly without trapping the user in a perpetual "account is already in use" lock.

**Why this priority**: One of the most severe operational weaknesses of the legacy server architecture is accounts getting stuck online after disconnects or map handoff failures; solving this at the session foundation guarantees high availability for both human players and large bot swarms.

**Independent Test**: Can be tested by logging an account into the world, simulating an abrupt ungraceful network drop (or initiating a second login with the kick-online flag), and verifying that the old session is fenced and evicted while the new login succeeds within seconds without manual administrator intervention.

**Acceptance Scenarios**:

1. **Given** an account that currently has an active or lingering session in the gateway or world, **When** a new valid login request (`0x41`) arrives for that same account, **Then** if the kick-online flag (`bKickOnline`) is non-zero, the server advances the account's session lease epoch, immediately disconnects the old client connection, persists the character's latest state, and allows the new session to proceed; if `bKickOnline` is zero, the server preserves the existing session and rejects the new login with the account-already-online code (`0x00050004`).
2. **Given** an account whose session handoff ticket was issued under an older lease epoch, **When** a delayed or replayed world-entry request arrives carrying that superseded ticket, **Then** the world server rejects the stale ticket immediately and prevents duplicate character instances from spawning in the world.
3. **Given** an in-game character that sends a clean logout command or stops responding to server heartbeats beyond the configured timeout window, **When** the world server detects the logout or timeout, **Then** the server saves the character's state, releases the active session lease immediately, and marks the account ready for subsequent logins.

---

### User Story 4 - Static World Map & Configuration Asset Loading (Priority: P4)

When the server starts up, it reads and validates the configured world map list, map region obstacle matrices, starter village spawn points, and base character/skill templates directly from the original game asset files so that entering characters are placed on verified, walkable coordinates with accurate initial statistics.

**Why this priority**: Preserving compatibility with the original game assets ensures the server operates as a true drop-in replacement without requiring manual conversion or duplication of existing map and setting files.

**Independent Test**: Can be tested by starting the server pointed at the original asset directory and verifying through startup health reports and character login tests that all configured starter maps (including Ba Lăng Huyện - Map 53), region grids, and five-element character templates load accurately.

**Acceptance Scenarios**:

1. **Given** a valid directory of original server map and setting assets, **When** the server initializes, **Then** it loads the active map list, parses region dimensions and cell obstacle matrices for each enabled map, and loads character base templates and name filter rules before opening client listener ports.
2. **Given** a missing or corrupted critical asset file (such as the world map list or base character template), **When** the server attempts to start, **Then** it halts startup before accepting player connections and reports the exact missing or malformed asset path to the operator.

---

### Edge Cases

- **Simultaneous Dual Login Race**: Two clients attempt to authenticate with the exact same account credentials at the exact same millisecond; the session coordinator serializes the requests per account, granting the newest lease epoch to one session and cleanly rejecting or kicking the other so two instances of the same character can never exist in the world simultaneously.
- **Orphaned Handoff Ticket (Client Drops Between Gateway and World Server)**: A client authenticates at the gateway and selects a character (allocating a handoff ticket) but loses internet connectivity before connecting to the world server; the ticket expires automatically after a short configurable grace window (e.g., 30 seconds), releasing the account lease so the player can log in again immediately.
- **Malformed Packet Length or Invalid Opcode Flood**: A client or external scanner sends truncated frames, oversized payloads, or unknown opcodes to the gateway or world server; the server immediately drops the offending connection and logs the protocol violation without affecting any other connected players or world zones.
- **Legacy Vietnamese Encoding & Null-Termination Boundaries**: Character names and account identifiers sent by the original client use legacy single-byte Vietnamese encoding (`TCVN3` / `GBK`) inside fixed-length buffers; the server accurately converts valid names to readable Unicode internally, enforces exact byte-length limits (1 to 31 bytes for account names, valid byte ranges for character names), and strips trailing bytes after the first null terminator.
- **Invalid Starter Village or Gender-Element Combination**: A modified client attempts to create a character in a non-starter map (such as a high-level dungeon) or with a disallowed elemental series value; the server validates the request against allowed starter villages and elemental rules and rejects the forged creation packet.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The server MUST accept legacy client connections on the configured gateway port (default `5622`) and world server port (default `6666`), executing the initial 44-byte handshake key exchange, 2-byte length-prefixed framing, rolling stream cipher encryption/decryption, and second-layer payload obfuscation with byte-for-byte fidelity.
- **FR-002**: The gateway MUST authenticate account login requests (`0x41`) by verifying protocol version compatibility, account existence, password digest match, and account lock/freeze status, returning the corresponding login result packet (`0x41`) to the client.
- **FR-003**: For development and swarm testing environments, the server MUST support an optional configurable auto-provisioning mode alongside standard strict account verification: when auto-provisioning is enabled, a login request (`0x41`) for an unrecognized username MUST automatically register and persist a new active account using the submitted username and MD5 password digest, and all subsequent logins for that account MUST verify the submitted password digest against the stored credential. When auto-provisioning is disabled, unrecognized usernames MUST be rejected with the invalid account/password result code (`0x00050003`).
- **FR-004**: Upon successful gateway authentication, the server MUST retrieve and return the account's character roster (`0x37`) containing up to 3 characters with their name, gender, elemental series, level, and appearance identifiers encoded in the expected second-layer obfuscated format.
- **FR-005**: The gateway MUST process new character creation requests (`0x45`) by validating character slot availability (maximum 3 characters per account), character name uniqueness, byte length, prohibited name filter rules, elemental series (`0` = Kim, `1` = Mộc, `2` = Thủy, `3` = Hỏa, `4` = Thổ), gender, and allowed starter village (`m_NativePlaceId`): if the requested starter village is valid and active in `worldset.ini`, the character spawns at that village's configured coordinates; if the requested starter village is a valid starter village ID but its map is not currently loaded in `worldset.ini`, the server MUST either redirect the character to the configured default active starter village (such as Ba Lăng Huyện - Map `53`) when fallback mode is enabled or reject the request (`0x8C` with `bSucceeded = 0`) when strict map mode is enabled. Upon validation, the server initializes the character's base attributes, vitals, and starter skills from the static player templates before returning creation confirmation (`0x8C`) and issuing a world handoff notification (`0x34`).
- **FR-006**: The gateway MUST process character deletion requests (`0x3A`) for existing characters belonging to the authenticated account by verifying the submitted password digest against the account's secondary password if configured (or accepting the primary password/authenticated session when no secondary password is set), permanently removing the character record so both the account slot and global character name are immediately freed for reuse, and returning the deletion confirmation packet (`0x8C`).
- **FR-007**: When an authenticated client selects an existing character (`0x64`) or completes new character creation (`0x45`), the session coordinator MUST verify that the character's target map is active in `worldset.ini` (or apply the configured fallback starter village such as Map `53` if unloaded map fallback is enabled; otherwise return `0x34` with `bPermit = 0`), issue a single-use 16-byte session handoff ticket bound to a monotonically increasing lease fencing epoch, and send the world server redirect notification (`0x34` with XOR-encoded port) to the client.
- **FR-008**: The session coordinator MUST enforce lease fencing epochs across all gateway and world sessions based on the `bKickOnline` flag (`+0x6F`) in `0x41`: when `bKickOnline != 0` (or when recovering from an expired/timed-out connection), the server MUST atomically increment the account's lease epoch, invalidate any prior session or pending handoff ticket, persist the evicted character's state, and admit the new session; when `bKickOnline == 0` and an active non-expired lease exists, the server MUST preserve the active session and return `LOGIN_R_ACCOUNT_EXIST` (`0x00050004`).
- **FR-009**: When a client connects to the world server and submits a valid logic login packet (`0x42`) with its 16-byte handoff ticket within the ticket's validity window, the world server MUST bind the session to the character's target map zone and transmit the initial world synchronization sequence: world map identity (`0x49`), current player full data (`0x44`), current player skill list (`0x45`), current player normal vitals (`0x46`), and initial synchronization completion (`0x43`).
- **FR-010**: Upon receiving the client's synchronization completion reply (`0x43`), the world server MUST transition the character session to active in-world status inside the map's 18-Hz simulation loop and maintain connection liveness through periodic server ping requests (`0x82`), client ping replies (`0x70`), and server ping acknowledgments (`0x99`).
- **FR-011**: When an active client sends a clean close connection packet (`0x9B`) or exceeds the heartbeat timeout threshold, the server MUST remove the character from the active map zone, persist any updated character state to durable storage, and immediately release the account's session lease.
- **FR-012**: On startup, the server MUST load and validate the configured map list (`worldset.ini`), map binary headers (`*.wor`), region obstacle grids (`*.dat`), starter village spawn definitions, and base player attribute templates from the static game asset directory, converting legacy text encodings (`TCVN3` / `GBK`) to standard Unicode at the asset boundary.
- **FR-013**: All account records, character profiles, and session states MUST be stored in durable storage using standard Unicode text while preserving exact round-trip fidelity when transcoding back to legacy single-byte encodings for client packets.

### Key Entities *(include if feature involves data)*

- **Account**: Represents a player or bot user identity with unique username, primary password hash digest, optional secondary password hash digest, account status (Active, Frozen, Locked), remaining play time/points, and current authoritative session lease epoch.
- **Character (Role)**: Represents a playable character belonging to an Account (up to 3 per account), holding a unique character name, gender, elemental series, level, experience, current map ID, world coordinates (X, Y), vital stats (Health, Mana, Stamina), base attributes (Strength, Dexterity, Vitality, Energy), money, and learned skills.
- **SessionLease**: Represents the active ownership lock and monotonically increasing fencing epoch for an online or transitioning Account, including the temporary single-use 16-byte handoff ticket (`GUID`), assigned world zone, and expiration timestamps.
- **MapZoneDefinition**: Represents a pre-loaded static game map defined in the server configuration, including its numeric Map ID, display name, region grid bounds, and walkable/obstacle cell matrix.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Both the original graphical game client (`game.exe`) and the autonomous headless client (`jx-swarm`) can complete the full login, character creation/selection, and world-entry flow into Ba Lăng Huyện (Map `53`) in under 2 seconds on a local network with 100% protocol compatibility.
- **SC-002**: 100 concurrent autonomous bot accounts from `jx-swarm` can authenticate, create characters if needed, enter the game world, and maintain stable 18-Hz heartbeat sessions for 10 continuous minutes with zero unexpected disconnects or protocol framing errors.
- **SC-003**: 100% of simulated abrupt client disconnects or forced re-logins (`bKickOnline = 1`) recover and allow immediate re-entry within 3 seconds, achieving zero stuck accounts ("account already in use" deadlocks).
- **SC-004**: 100% of malformed, truncated, or unauthenticated network packets sent to the gateway or world server ports are rejected and isolated to the offending connection with zero server crashes or tick-rate degradation for online players.

## Assumptions

- The original static server assets (`worldset.ini`, `/maps/*.wor`, `/maps/*/*.dat`, `/settings/npcs.txt`, and `/settings/player/` templates) are available on disk at a configurable asset root path (defaulting to the local `jx-linux-bin/server1` directory during development).
- Passwords transmitted by the legacy client are 32-character uppercase hexadecimal MD5 digests (`KSG_PASSWORD`) as established by the original client protocol.
- In Milestone 1, entering characters spawn at their starter village or saved map coordinates and maintain an active in-world session with full self-synchronization (`0x44`, `0x45`, `0x46`, `0x49`, `0x43`) and heartbeat pings; multi-player neighbor broadcasting (`3x3` region visibility), walking/running movement, and chat channels are delivered in Milestone 2.
