# Phase 0 Research: Foundational Gateway Authentication, Character Lifecycle & World Entry (`001-gateway-world-entry`)

**Date**: 2026-11-08  
**Feature**: [spec.md](spec.md)

## 1. Ground-Truth Binary Protocol & Cipher Verification

- **Decision**: Implement the exact Kingsoft 2-byte length-prefixed TCP framing, 44-byte initial handshake (`0x20` `s2c_accountbegin`), 5,679-entry `g_uPublicKeys` `KSG_DecodeEncode` stream cipher, and second-layer payload obfuscators (`0x3042` for role list `0x37` and `0x38BC` for role selection `0x64`) inside `JXServer.Adapter.LegacyJX1`.
- **Rationale**: Verified directly against `jx-swarm/docs/00-network-core/framing-and-cipher.md`, `jx-swarm/docs/01-session-lifecycle/bishop-gateway.md`, `jx-swarm/docs/01-session-lifecycle/gameserver-session-and-sync.md`, `jx-win-src/Headers/KProtocol.h`, and `jx-linux-bin` binaries (`bishop_y` and `jx_linux_y`).
  - On connection accept, the server generates random 32-bit `uClientKey` and `uServerKey`, obfuscates each key via the inverse of client deobfuscation (swap byte 0 and byte 3, bitwise NOT, add `0x2E6D2399`, XOR with `0x2E6D23CF`), and transmits the 42-byte `0x20` payload (`wFrameLength = 44`) in plaintext.
  - All subsequent incoming `c2s_*` frames are decrypted with `uClientKey` and outgoing `s2c_*` frames are encrypted with `uServerKey` using the LCG key mutation `_rol(uKey, 1) * 0x10A860C1u + 0xEE57FD49u` after each frame.
  - Note on `s2c_notifyplayerlogin` (`0x34`, `60 bytes`): Verified from `bishop_y` @ `0x08055e4c`, `game.exe` @ `0x0052bd20`, and `JXSwarm.HeadlessClient/Sessions/BishopSession.cs` that offset `+0x36` is the 4-byte IPv4 address in network byte order and offset `+0x3A` is the plain little-endian `uint16` GameServer TCP port (e.g., `6666` = `0x1A0A`).
- **Alternatives Considered**: Custom JSON/WebSocket framing for Phase 1 was rejected because Phase 1 must work unmodified with `game.exe` and `JXSwarm.Drone.Host`.

---

## 2. Character Creation Templates & Starter Village Spawn Resolution

- **Decision**: Load initial character attributes, vitals, and starter skills directly from `server1/settings/npc/player/newplayerini00.ini` through `newplayerini09.ini` (`templateIndex = Series * 2 + Gender`), `level_add.txt`, `revivalid.ini`, `revivepos.ini`, and `nativeplacelist.ini` (plus name filter patterns from `gateway/settings/goddess_rolename.flt`).
- **Rationale**: Inspection of `d:\Projects\jx-linux-bin\server1\settings\npc\player\` confirmed exact starter values per `(ElementSeries, CharacterGender)` pair:
  - `00`/`01` (`Kim` Male/Female): Power `35`, Agility `25`, Outer `25`, Inside `15`, MaxLife `204`, MaxInner `16`, MaxStamina `100`, 7 starter skills (`53`, `1`, `2`, `229`, `230`, `231`, `232` at level `1`).
  - `02`/`03` (`Mộc` Male/Female): Power `20`, Agility `35`, Outer `20`, Inside `25`, MaxLife `103`, MaxInner `77`, MaxStamina `100`.
  - `04` (`Thủy` Male): Power `25`, Agility `25`, Outer `25`, Inside `25`, MaxLife `100`, MaxInner `200`, MaxStamina `100`.
  - `05` (`Thủy` Female): Power `25`, Agility `25`, Outer `25`, Inside `25`, MaxLife `153`, MaxInner `77`, MaxStamina `100`.
  - `06`/`07` (`Hỏa` Male/Female): Power `30`, Agility `20`, Outer `30`, Inside `20`, MaxLife `213`, MaxInner `41`, MaxStamina `100`.
  - `08`/`09` (`Thổ` Male/Female): Power `20`, Agility `15`, Outer `25`, Inside `40`, MaxLife `76`, MaxInner `163`, MaxStamina `100`.
  - Starter village coordinates are resolved via `revivalid.ini` $\rightarrow$ `revivepos.ini`: for example, Map `53` (Ba Lăng Huyện) maps to `RevivalId = 19` $\rightarrow$ `MpsX = 52032, MpsY = 101696` (`RegionX = 101, RegionY = 99`, packed `RegionId = (99 << 16) | 101 = 6488165`).
- **Alternatives Considered**: Hardcoding stat tables in C# was rejected in favor of loading from the `/settings/npc/player/` INI/TXT files (with verified fallback defaults for unit tests) to honor Constitution Principle III.

---

## 3. Lease-Fenced Session Coordinator (Eliminating Stuck Accounts)

- **Decision**: Manage account login state and world handoff tickets through `SessionLease` (an ABP DDD Aggregate Root coordinated by `SessionLeaseManager` and `SessionLeaseAppService`) using a monotonically increasing `LeaseEpoch` (`long`) and single-use `HandoffTicketGuid` (`Guid`).
- **Rationale**: In legacy JX Linux, `PaySys`, `Bishop`, `Goddess`, and `GameServer` track online status across separate processes without fencing tokens; if a TCP drop occurs during map transfer, the account remains locked. By assigning a monotonic `LeaseEpoch` on every authentication (and incrementing it immediately when `bKickOnline = 1` or when a heartbeat times out), any lingering connection or stale world-entry ticket carrying an older `LeaseEpoch` is immediately evicted and saved before the new session attaches.
- **Alternatives Considered**: Distributed time-locks (waiting 5 minutes on `0x9D` lock account) were rejected because they reproduce the legacy stuck-account bug.

---

## 4. Sharded 18-Hz Zone Loop & ORM Separation

- **Decision**: Represent each loaded map in `JXServer.ZoneEngine` as an isolated single-threaded `MapZoneWorker` driven by `PeriodicTimer` / `TimeProvider` at `18 Hz` (`~55.5ms` per tick) and fed by an unbounded single-reader `Channel<IZoneCommand>`. Represent connected players inside the zone as lightweight `PlayerZoneState` slots rather than EF Core `Character` entities.
- **Rationale**: Complies with Constitution Principles I and VI. Keeping EF Core entities out of the 18-Hz tick thread guarantees zero DbContext concurrency exceptions and sub-millisecond tick execution.
- **Alternatives Considered**: Actor-per-player or sharing `DbContext` across map threads was rejected due to lock contention and GC overhead.

---

## 5. Persistence Provider: EF Core 10 with SQL Server & SQLite

- **Decision**: Use `Microsoft.EntityFrameworkCore.SqlServer` (`10.0.0`) as the primary production database provider and `Microsoft.EntityFrameworkCore.Sqlite` (`10.0.0`) / `InMemory` for fast local development and automated test suites, matching `jx-swarm`.
- **Rationale**: First-party .NET 10 support, native JSON column mapping for character skill slots, and 100% operational alignment with the existing `jx-swarm` workspace.
- **Alternatives Considered**: PostgreSQL was considered earlier and replaced by SQL Server + SQLite per project owner decision.
