# Comprehensive Specification & Architecture Survey Report

**State Folder**: `d:\Projects\jx-server\.agents\teamwork\spec_miner_survey\`  
**Target File**: `handoff.md`  
**Date**: 2026-11-11  
**Author**: Specification & Architecture Surveyor (Teamwork Specialist)  
**Governing Documents**:
- `d:\Projects\jx-server\.agents\teamwork\ORIGINAL_REQUEST.md`
- `docs/specs-roadmap.md` (`001`–`030`)
- `.specify/memory/constitution.md` (`v1.2.0`)
- `docs/data-architecture-standard.md` (`JXData Standard v1`)
- `specs/004-zone-modular-spine/contracts/` (`zone-modular-spine-and-harness.md`, `domain-outbound-sinks.md`, `character-persistence-contracts.md`)
- Existing Specs: `specs/001-gateway-world-entry`, `specs/002-spatial-aoi-movement-chat`, `specs/003-combat-missiles-loot-inventory`, `specs/004-zone-modular-spine`

---

## 1. Observation

### 1.1 Existing Codebase & Verification Baseline
- **Solution & Test Execution**: Running `dotnet test "d:\Projects\jx-server\JXServer.slnx"` verified that **97 of 97 existing tests PASS with 0 failures** across 3 test assemblies:
  - `JXServer.Protocol.Tests.dll`: 59 passed (102 ms)
  - `JXServer.ZoneEngine.Tests.dll`: 24 passed (4 s)
  - `JXServer.Integration.Tests.dll`: 14 passed (21 s)
  - Zero behavioral regressions exist in the current baseline.
- **Monolithic `MapZoneWorker.cs` Status**: Located at `src/JXServer.ZoneEngine/WorldZones/MapZoneWorker.cs`, currently spanning **2,975 lines of code**. It bundles:
  1. Zone command channel drainage and direct inline execution of 25 distinct zone commands across world sessions, movement, combat, items, and chat.
  2. Single inline 18-Hz tick loop executing combat status effects, monster AI, missile flight, ground object expiration, movement/posture updates, and session heartbeat timeouts in hardcoded order.
  3. Inline chat handling (`c2s_playertalk`, `c2s_playersendchat`, `c2s_chatsetchannel`) directly coupling chat to core zone execution.
  4. Monolithic outbound notifications coupled via `IPlayerOutboundSink` without domain segregation.
- **Legacy File Parsers in Runtime `src/JXServer.GameAssets`**:
  - `Archives/PakFilePathHasher.cs`, `Archives/UclNrv2bDecompressor.cs`, `Archives/XPackArchiveReader.cs`
  - `Configuration/IniFileParser.cs`, `Configuration/TabFileParser.cs`
  These files are legacy file readers that must be removed from runtime to comply with Constitution Principle V and `docs/data-architecture-standard.md`, while runtime catalogs consume `artifacts/*.pack` (MemoryPack) or `data/**/*.json`.
- **Existing Specification Directory Inventory**:
  - `specs/001-gateway-world-entry` (Contains 10 files: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `tasks.md`, `checklists/requirements.md`, `contracts/application-and-zone-ports.md`, `contracts/bishop-gateway-protocol.md`, `contracts/gameserver-entry-protocol.md`).
  - `specs/002-spatial-aoi-movement-chat` (Contains 10 files: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `tasks.md`, `checklists/requirements.md`, `contracts/aoi-and-movement-protocol.md`, `contracts/chat-protocol.md`, `contracts/zone-spatial-and-chat-ports.md`).
  - `specs/003-combat-missiles-loot-inventory` (Contains 10 files: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `tasks.md`, `checklists/requirements.md`, `contracts/combat-and-skills-protocol.md`, `contracts/inventory-and-ground-loot-protocol.md`, `contracts/zone-combat-and-inventory-ports.md`).
  - `specs/004-zone-modular-spine` (Contains 8 files: `spec.md`, `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, `checklists/requirements.md`, `contracts/character-persistence-contracts.md`, `contracts/domain-outbound-sinks.md`, `contracts/zone-modular-spine-and-harness.md`).

---

## 2. Features Discovered & Probed Inventory

Across the authoritative specifications, reference C++ source code (`jx-win-src`), Linux server binaries (`jx-linux-bin`), and swarm vectors (`jx-swarm`), the following **54 distinct features** have been cataloged and probed:

| # | Category | Feature | Description | Inputs | Outputs | Error Behavior | Discovered Via |
|---|----------|---------|-------------|--------|---------|----------------|----------------|
| 1 | Data Pipeline | JSON Schema 2020-12 Contracts | Validates 100% of game assets under `data/**/*.json` against domain schemas | JSON files under `data/` | Validation success / schema report | Throws schema violation with path & failing keyword | `docs/data-architecture-standard.md`, `schemas/*.schema.json` |
| 2 | Data Pipeline | Dual-Tier MemoryPack Snapshots | Pre-compiled zero-copy binary `.pack` snapshots verified by SHA-256 for sub-ms catalog load | `artifacts/*.pack` + SHA-256 | Immutable `I*Catalog` registries | Throws on checksum mismatch or corrupt pack | `docs/data-architecture-standard.md`, `constitution.md` |
| 3 | Data Pipeline | Immutable Catalogs & Dual IDs | Fast O(1) asset access via numeric `id` and semantic `code` (`FrozenDictionary`) | `id` (int) or `code` (string) | Template record | `TryGet*(...)` returns false | `GameAssets/`, `data-architecture-standard.md` |
| 4 | Data Pipeline | Standalone Offline Exporter CLI | Extracts `.pak`/`.dat`/`.txt`/`.ini`, evaluates Lua static curves, outputs canonical JSON | Legacy server files in `jx-linux-bin` | `data/**/*.json`, `artifacts/*.pack` | Exporter reports parity report with 0 unaccounted | `tools/JXServer.DataExporter`, `docs/data-architecture-standard.md` |
| 5 | Wire Transport | 2-Byte Length-Prefixed Framing | `JxFramePipeProcessor` TCP framing; validates `wPacketLen = payload.Length + 2` (3..65535) | Raw TCP byte stream | Structured payload `ReadOnlyMemory<byte>` | Disconnects client on length < 3 or buffer overflow | `001-gateway-world-entry/contracts/bishop-gateway-protocol.md` |
| 6 | Wire Transport | 44-Byte Handshake & Key Obfuscation | `AccountBeginS2CPacket` (`0x20`); transmits obfuscated `uClientKey` & `uServerKey` | Initial TCP connection | 42-byte payload (44 on wire) | Drops connection on incomplete handshake | `001-gateway-world-entry/contracts/bishop-gateway-protocol.md` |
| 7 | Wire Transport | Rolling `KSG_DecodeEncode` Stream Cipher | Symmetric stream cipher using 5,679-entry `g_uPublicKeys` with LCG mutation | Key `uKey`, byte buffer | Encrypted/decrypted buffer in place | Mismatched key produces malformed opcode, dropped | `001-gateway-world-entry/contracts/bishop-gateway-protocol.md` |
| 8 | Wire Transport | Second-Layer XOR Obfuscators | `0x3042` rolling 16-bit XOR for role list `0x37`; `0x38BC` XOR for role selection `0x64` | Role list payload, character name | Obfuscated byte buffer | Invalid decoding causes character not found / invalid name | `001-gateway-world-entry/contracts/bishop-gateway-protocol.md` |
| 9 | Wire Transport | Boundary Encodings Transcoding | `Tcvn3Encoding` & `GbkEncoding` transcoding at network adapter edge to UTF-8 | Wire bytes (TCVN3/GBK) | UTF-8 C# string | Replaces invalid chars with '?' or fails validation | `Primitives/Encodings/`, `constitution.md` Principle V |
| 10 | Auth & Leases | Account Login (`0x41`) | Authenticates username/MD5 password; auto-provisions in dev or validates against DB | `0x41` fixed 127B payload | `0x41` result packet + `0x37` role list | Returns `0x00050003` (BadPwd), `0x00050004` (Exist), `0x0005000B` (Frozen) | `001-gateway-world-entry/contracts/bishop-gateway-protocol.md` |
| 11 | Auth & Leases | Character Roster Query (`0x37`) | Returns up to 3 character summaries encrypted with `0x3042` XOR cipher | Authenticated account session | `0x37` variable payload (12..117B) | Empty list for new accounts | `001-gateway-world-entry/contracts/bishop-gateway-protocol.md` |
| 12 | Auth & Leases | Character Creation (`0x45`) | Creates character across 5 series & starter villages with base stats & skills | `0x45` variable (21..45B) | `0x8C` confirmation + `0x34` ticket | Returns `0x8C` (`bSucceeded=0`) on duplicate name/invalid params | `001-gateway-world-entry/contracts/bishop-gateway-protocol.md` |
| 13 | Auth & Leases | Character Deletion (`0x3A`) | Deletes character after secondary password MD5 verification; frees name and slot | `0x3A` fixed 129B payload | `0x8C` (`bSucceeded = 1/0`) | Rejects on wrong secondary password | `001-gateway-world-entry/contracts/bishop-gateway-protocol.md` |
| 14 | Auth & Leases | Character Selection (`0x64`) | Selects character by obfuscated name; verifies map and allocates lease handoff ticket | `0x64` fixed 41B payload | `0x34` (`NotifyPlayerLoginS2CPacket`) | `0x34` with `bPermit = 0` if map unloaded & fallback disabled | `001-gateway-world-entry/contracts/bishop-gateway-protocol.md` |
| 15 | Auth & Leases | Monotonic Lease-Epoch Fencing | Fences sessions via `LeaseEpoch`; evicts stale sessions when `bKickOnline != 0` | New login or kick flag | Incremented `LeaseEpoch`, stale eviction | Stale lease writes silently discarded | `001-gateway-world-entry/spec.md`, `004-zone-modular-spine/contracts` |
| 16 | Auth & Leases | Single-Use World Handoff Ticket | 16-byte GUID issued by Bishop (`0x34`) redeemed by GameServer (`0x42`) with 30s TTL | Ticket GUID | Validated `CharacterDto` | Rejects on ticket timeout or superseded lease | `001-gateway-world-entry/contracts/bishop-gateway-protocol.md` |
| 17 | Zone Spine | Modular Command Routing | Single-threaded zero-boxing command dispatcher via `ZoneExtensionRegistry` | `IZoneCommand` | ValueTask execution | Logs `LogUnhandledZoneCommand` (3009) and discards | `004-zone-modular-spine/contracts/zone-modular-spine-and-harness.md` |
| 18 | Zone Spine | Phased 18-Hz Tick Pipeline | Ordered execution phases (`ZoneTickPhase`: StatusEffects=100..SessionHeartbeats=600) | `ZoneExecutionContext` per frame | Phase completion | Subsystem exception caught, logged (3011), loop continues | `004-zone-modular-spine/contracts/zone-modular-spine-and-harness.md` |
| 19 | Zone Spine | Per-Player Feature State Slots | Typed O(1) feature slots (`PlayerFeatureSlotKey<T>`) attached to `PlayerZoneState` | Feature slot instance | Stored state slot | Conflicting keys throw on registration; detach hook invoked | `004-zone-modular-spine/contracts/zone-modular-spine-and-harness.md` |
| 20 | Zone Spine | Per-Session Fault Isolation | Try/catch around commands and per-player tick loops; isolates throwing sessions | Injected exception | Error log with CharacterId & MapId | Healthy players on same map tick unaffected | `004-zone-modular-spine/contracts/zone-modular-spine-and-harness.md` |
| 21 | Zone Spine | Domain-Segregated Outbound Sinks | Decomposes 34 outbound methods into `World`, `Combat`, `Inventory`, `Chat` sinks | Sink method calls | Frames into single FIFO queue | Fallback to NoOp sinks when unsubscribed | `004-zone-modular-spine/contracts/domain-outbound-sinks.md` |
| 22 | Zone Spine | Foundational Persistence Fields | `TaskValues` (sparse 0..65535), `RepositoryPasswordHash`, `IsBroken`, `EnchaseLevel` | Character/Item mutations | EF Core durable relational + JSON columns | Stale epoch rejected; invalid password format throws BusinessException | `004-zone-modular-spine/contracts/character-persistence-contracts.md` |
| 23 | Zone Spine | World Entry Handshake (`0x42`..`0x43`) | Streams initial state (`0x49`, `0x44`, `0x45`, `0x46`, `0x4C`, `0xA4`, `0x43`) | `0x42` ticket GUID | Initial burst + client `0x43` reply | Drops session if ticket invalid or map not found | `001-gateway-world-entry/contracts/gameserver-entry-protocol.md` |
| 24 | Zone Spine | Continuous Heartbeat Pings | 5-second interval server ping `0x82`, client reply `0x70`, server ack `0x99`, close `0x9B` | Periodic timer / client ping | Timestamp echoes | Evicts and saves session on heartbeat timeout | `001-gateway-world-entry/contracts/gameserver-entry-protocol.md` |
| 25 | Zone Spine | Deterministic `ZoneSimulationHarness` | Virtual frame stepping (1,000 frames < 5ms) via `FakeTimeProvider` & fixed `RandomSeed` | Virtual frames, scheduled commands | `PlayerOutboundEventRecorder` | Mismatched frames fail assertion | `004-zone-modular-spine/contracts/zone-modular-spine-and-harness.md` |
| 26 | Spatial AOI | Spatial Region Grid Partitioning | Maps partitioned into 16x32 cell regions (512x1024 Mps); fast O(1) slot lookup | Coordinates `(MpsX, MpsY)` | `SpatialRegionSlot` | Coordinates clamped to map boundaries | `002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` |
| 27 | Spatial AOI | 3x3 Region AOI Visibility | Set difference calculation between old 3x3 and new 3x3 regions on boundary cross | Region crossing | `0x4C` full sync burst to entering entities | Zero allocation via fixed-size destination span | `002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` |
| 28 | Spatial AOI | Entity Compact Sync (`0x4D`/`0x4E`) | Periodic/motion broadcast of neighbor (`0x4D`, 18B) and self (`0x4E`, 13B) posture/vitals | 18-Hz motion or tick | Wire sync packets | Out-of-region entities do not receive broadcast | `002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` |
| 29 | Spatial AOI | On-Demand Entity Detail Queries | `c2s_requestplayer` (`0x48`) & `c2s_requestnpc` (`0x49`) trigger `s2c_syncnpc` (`0x4C`, 61B) | Target runtime `NpcId` | `0x4C` full sync packet | Silently ignored if target entity not found | `002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` |
| 30 | Spatial AOI | Static NPC & Monster Spawning | Instantiates static NPCs/monsters from `*.map.json` and `autoexec` catalogs into regions | Map descriptor | Region entity lists, `StaticNpcs` dict | Corrupt spawn skipped and logged | `002-spatial-aoi-movement-chat/research.md`, `GameAssets/` |
| 31 | Movement | Movement Anti-Cheat Verification | Validates `dwCheckValue`: `((dwCheckValue ^ Token1 ^ Token2) % Key) == 0` | Checksum & tokens | Boolean validation result | Discards move request if checksum invalid | `002-spatial-aoi-movement-chat/research.md` (§4) |
| 32 | Movement | Authoritative Walk (`0x4B` $\rightarrow$ `0x50`) | Authoritative per-frame vector progression (WalkSpeed = 10 Mps/tick); broadcasts `0x50` | `c2s_npcwalk` target `(X, Y)` | `s2c_npcwalk` (`<BIii`) to 3x3 AOI | Clamps at obstacle cell boundary | `002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` |
| 33 | Movement | Authoritative Run (`0x4C` $\rightarrow$ `0x51`) | Authoritative per-frame vector progression (RunSpeed = 20 Mps/tick); broadcasts `0x51` | `c2s_npcrun` target `(X, Y)` | `s2c_npcrun` (`<BiIi`) to 3x3 AOI | Asymmetric wire layout (`DestY, NpcId, DestX`) strictly enforced | `002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` |
| 34 | Movement | Player Jump (`0x4E` $\rightarrow$ `0x54`) | Teleports player over non-barrier space with jump posture (`5`); broadcasts `0x54` | `c2s_npcjump` destination | `s2c_npcjump` (`<BiiI`) to 3x3 AOI | Rejects jump if destination cell is `Obstacle_Full` | `002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` |
| 35 | Movement | Posture & Sitting Regen (`0x71` $\rightarrow$ `0x53`) | Sit posture toggle (`Doing = 4`); sitting regenerates Life/Mana every 18 frames | `c2s_npcsit` flag | `s2c_npcsit` (`0x53`) + `0x46` vital sync | Moving interrupts sitting immediately | `002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` |
| 36 | Portals | Edge-Triggered Cell Trap Detection | Triggers `PortalTrapBindingTemplate` when stepping on cell with `TrapId > 0` | Player `(MpsX, MpsY)` | Trap execution | Anti-loop: `LastTriggeredTrapId` prevents repeated triggers on same trap cell | `002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` |
| 37 | Portals | Town-Gate FightMode Toggles | Bidirectional town-gate transitions toggle `FightMode` between `0` (Peace) and `1` (Fight) | Portal cell step | Position update + `0x4E` sync | Preserves equipment and active buffs | `002-spatial-aoi-movement-chat/research.md` (§2) |
| 38 | Portals | Same-Node Map Handoff (`NewWorld`) | In-memory shard handoff: sends `0x49` + `0x4E` + 3x3 `0x4C` without cipher reset or `0x43` | Target `MapId`, coordinates | Map transition sync packets | Falls back to default village on destination failure | `002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` |
| 39 | Portals | Cross-Node Map Redirect (`0x35`) | Sends `NotifyPlayerExchangeS2CPacket` (`0x35`, 25B, plain LE port) for external shards | External map handoff | `0x35` redirect frame | Returns GUID 0 if destination server unavailable | `002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` |
| 40 | Portals | Level-Gate Rejection & Bounce | Bounces player back to safe coords if `Level < MinRequiredLevel`, sends `0xFB` msg | Low-level player step | Bounce coordinates + system text | Blocks entering high-level maps | `002-spatial-aoi-movement-chat/research.md` (§2) |
| 41 | Inventory | Spatial Container Grids | `pos_hand` (1), `pos_equip` (2, 15 slots), `pos_equiproom` (3, 6x10), `pos_immediacy` (7) | Container, GridX, GridY | Occupancy matrix update | Rejects out-of-bounds or overlapping placements | `003-combat-missiles-loot-inventory/contracts/inventory-and-ground-loot-protocol.md` |
| 42 | Inventory | Item Move & Swap (`0x5B` $\rightarrow$ `0x61` + `0x8E`) | Validates 2D footprint collision, moves/swaps items between grids, unlocks client drag | `c2s_playermoveitem` coords | `s2c_playermoveitem` + `s2c_ItemAutoMove` | Unlocks client on failure; preserves original slots | `003-combat-missiles-loot-inventory/contracts/inventory-and-ground-loot-protocol.md` |
| 43 | Inventory | Item Stack Splitting (`0x85`) | Splits `DivideCount` from stackable item into `pos_hand` slot | `c2s_playerdivideitem` | `0x5F` item sync frames | Rejects if split count >= current stack count or <= 0 | `003-combat-missiles-loot-inventory/contracts/inventory-and-ground-loot-protocol.md` |
| 44 | Inventory | Equipping & Five-Elements Affixes | Recalculates character stats and unlocks 3 hidden affixes per `ms_ActivedEquip` ring | Item equip/unequip | Updated vitals/stats + sync packets | Suspends bonuses if item broken | `003-combat-missiles-loot-inventory/research.md` (§5) |
| 45 | Inventory | Medicine Consumption (`0x59`) | Eats potion, applies 9-frame periodic regeneration tick, decrements stack | `c2s_playereatitem` | `0x46` vitals sync, `0x60` remove if 0 | Rejects on anti-bot token mismatch or cooldown | `003-combat-missiles-loot-inventory/contracts/inventory-and-ground-loot-protocol.md` |
| 46 | Combat | Martial Skill Cast (`0x4D` / `0x87`) | Validates `SkillCheckKey = 0x3AB9`, mana, cooldown, range; broadcasts `0x5A`/`0x85` | `c2s_npcskill` / autoattack | Skill cast broadcast to 3x3 AOI | Rejects on invalid check key, insufficient mana, or CD | `003-combat-missiles-loot-inventory/contracts/combat-and-skills-protocol.md` |
| 47 | Combat | Aura Skill Toggle (`0x6F`) | Sets active right-hand aura skill; applies continuous team/self aura effects | `c2s_changeauraskill` | Updated aura slot & aura broadcasts | Rejects if skill not learned or not an aura | `003-combat-missiles-loot-inventory/contracts/combat-and-skills-protocol.md` |
| 48 | Combat | 64-Direction Missile Simulation | Advances fixed-point missiles via `g_nSin`/`g_nCos[64]`; checks barrier & hit collision | Missile speed, direction, frame | Impact event / AOI damage | Despawns on barrier (`Obstacle_Full`/`Fly`) or max lifetime | `003-combat-missiles-loot-inventory/research.md` (§2) |
| 49 | Combat | Five-Elements Damage & Resistances | 6 damage channels (Physics, Cold, Fire, Light, Poison, Magic); Tương Sinh/Khắc math | Attacker stats, defender resists | Final applied damage per channel | Damage clamped; resistances capped at 75% (max 95%) | `003-combat-missiles-loot-inventory/research.md` (§3) |
| 50 | Combat | Status Effects & Hurt Recoil | Cold slow, Poison DoT interval blending, Stun; broadcasts `0x56` (`do_hurt`, 22B) | Applied elemental damage | `0x56` hurt broadcast + posture change | Reduced by resistance time reduce percentages | `003-combat-missiles-loot-inventory/contracts/combat-and-skills-protocol.md` |
| 51 | Monster AI | 18-Hz Monster AI State Machine | Autonomous state cycle: Patrol -> Aggro -> Attack -> Leash -> Dead/Respawn | Frame tick, nearby players | Movement/skill commands, respawns | Leashes and resets HP if pulled beyond pursuit radius | `003-combat-missiles-loot-inventory/research.md` (§4) |
| 52 | Progression | Level-Up, Stat & Skill Allocation | XP threshold check; attribute point allocation (`0x57`/`0x5D`), skill point (`0x58`/`0x5E`) | Monster kill / alloc commands | `0x44`/`0x46` vitals sync, `0x5D`/`0x5E` | Rejects allocation exceeding unallocated point pool | `003-combat-missiles-loot-inventory/contracts/combat-and-skills-protocol.md` |
| 53 | Loot & Items | KItemGenerator Blue Affix Rolling | Evaluates `treasureDropCount`, rolls 6 affixes (`m_wVersion = 4`), spawns `GroundObject` | Monster kill, killer Luck | `0x64` `s2c_objadd` to 3x3 AOI | Rejects duplicate attribute IDs on same item | `003-combat-missiles-loot-inventory/research.md` (§5) |
| 54 | Chat Module | Multi-Channel Chat Module (016) | Nearby spatial talk (`0x52`/`0x5B`), structured chat (`0x56`/`0x7E`), subscription (`0x60`) | Chat message, channel bitmask | `0x5B`/`0x7E`/`0xFB` packets | Drops message if sender channel unsubscribed | `002-spatial-aoi-movement-chat/contracts/chat-protocol.md`, `004-zone-modular-spine` |

---

## 3. Edge Cases & Exact Observed Behaviors

| # | Feature | Input Condition | Authoritative Observed Behavior | Source Verification |
|---|---------|-----------------|---------------------------------|---------------------|
| 1 | Framing | Packet length `< 3` or `> 65535` | `JxFramePipeProcessor` immediately flags protocol violation, drops connection, logs warning with IP. | `002-core-legacy-wire-protocol`, `FramingAndCipherTests` |
| 2 | Cipher | Incomplete or corrupted 44-byte initial handshake | Decryption fails on subsequent packets; malformed opcode detected; session severed before state mutation. | `002-core-legacy-wire-protocol`, `KsgStreamCipher` |
| 3 | Login | Unrecognized account login with AutoProvisioning = true | Server creates new `Account` with submitted username, MD5 password digest, active status, and opens fresh roster. | `003-core-auth-roster-and-leases`, `spec.md` Clarification 1 |
| 4 | Login | Active session exists and new login arrives with `bKickOnline == 0` | Rejects new login immediately with `LOGIN_R_ACCOUNT_EXIST` (`0x00050004`); preserves existing active session intact. | `003-core-auth-roster-and-leases`, `spec.md` Clarification 3 |
| 5 | Login | Active session exists and new login arrives with `bKickOnline != 0` | Advances `LeaseEpoch`, signals old gateway/zone session eviction, flushes character snapshot, admits new session. | `003-core-auth-roster-and-leases`, `SessionLeaseManager` |
| 6 | World Entry | Expired (TTL > 30s) or stale `LeaseEpoch` handoff ticket presented | GameServer rejects `0x42` `LogicLoginC2SPacket` immediately; closes TCP socket; zero duplicate character presence. | `004-core-zone-spine-and-world-entry`, `SessionLeaseAppService` |
| 7 | Zone Spine | Unregistered `IZoneCommand` dispatched into `MapZoneWorker` | Handler drops command, logs `LogUnhandledZoneCommand` (EventId 3009, Warning), continues draining queue. | `004-core-zone-spine-and-world-entry`, `contracts/zone-modular-spine-and-harness.md` |
| 8 | Zone Spine | Unhandled exception thrown inside a modular command handler | Isolates exception per command; logs `LogZoneCommandFaulted` (EventId 3010, Error) with `CharacterId`; ticks continue. | `004-core-zone-spine-and-world-entry`, `contracts/zone-modular-spine-and-harness.md` |
| 9 | Zone Spine | Unhandled exception in one `IZoneTickSystem` (e.g., Phase 300 Missiles) | Catches exception, logs `LogZoneTickSystemFaulted` (EventId 3011, Error), proceeds to execute subsequent phases (Phase 400..600). | `004-core-zone-spine-and-world-entry`, `contracts/zone-modular-spine-and-harness.md` |
| 10 | Zone Spine | Character detaches with active `PlayerFeatureSlot` instances | Zone loops through all slots, executes `OnPlayerDetached(...)` wrapped in try/catch (EventId 3012), nulls references. | `004-core-zone-spine-and-world-entry`, `PlayerZoneState.DetachAllFeatureSlots` |
| 11 | Persistence | Storing task variable with value `0` | `SetTaskValue(taskId, 0)` removes `taskId` from sparse dictionary so zero-valued entries never bloat persistent JSON. | `004-core-zone-spine-and-world-entry`, `Character.cs:73-80` |
| 12 | Persistence | Setting repository password with non-hex string | `SetRepositoryPasswordHash` throws `BusinessException` (`CharacterInvalidRepositoryCredential`) if not valid 32-char MD5 hex. | `004-core-zone-spine-and-world-entry`, `Character.cs:118-124` |
| 13 | Movement | Check value `dwCheckValue` invalid or 0 | Anti-cheat check fails; server discards walk/run command; character does not move; no desync broadcast. | `006-core-movement-and-obstacles`, `MovementAntiCheatConsts` |
| 14 | Movement | Player runs into an `Obstacle_Full` (barrier) cell | Vector progression halts at boundary cell; switches `Doing = Stand (1)`; self position syncs via `0x4E` and `0x4D`. | `006-core-movement-and-obstacles`, `MapZoneWorker.ServeMove` |
| 15 | Movement | Wire layout difference between Walk (`0x50`) and Run (`0x51`) | `0x50` serializes `<BIii` (`NpcId, DestY, DestX`); `0x51` serializes `<BiIi` (`DestY, NpcId, DestX`). Must match 100%. | `006-core-movement-and-obstacles`, `aoi-and-movement-protocol.md` |
| 16 | Portals | Character crosses portal without minimum required level | Trap execution fails level gate; bounces player to `bounceMpsX, bounceMpsY`; sends `0xFB` system notification. | `007-core-portals-and-map-handoff`, `research.md` (§2) |
| 17 | Portals | Stepping repeatedly on the same portal trap cell | `LastTriggeredTrapId` matches cell `uTrapId`; trigger ignored until player moves to cell with `uTrapId == 0`. | `007-core-portals-and-map-handoff`, `research.md` (§2) |
| 18 | Portals | Destination map hosted on same server vs. different server | Same server: in-memory `AttachTransferredPlayerZoneCommand` + `0x49`/`0x4E` without cipher reset. Different: `0x35` redirect. | `007-core-portals-and-map-handoff`, `contracts/aoi-and-movement-protocol.md` |
| 19 | Inventory | Attempting to place an item where space is already occupied | Grid collision detection rejects move; sends `s2c_ItemAutoMove` (`0x8E`) to unlock client cursor; item stays in source. | `008-core-inventory-and-equipment`, `PlayerInventoryGrid.cs` |
| 20 | Inventory | Equipping item in `pos_hand` disconnects unexpectedly | Logout or zone detachment auto-places cursor item back into first available `pos_equiproom` bag slot or preserves it. | `008-core-inventory-and-equipment`, `PlayerZoneState.cs` |
| 21 | Combat | Skill cast requested during cooldown or insufficient mana | Cast rejected; zero animation or damage broadcast; client receives vital sync `0x46` confirming remaining mana. | `009-core-combat-and-missiles`, `CombatProgressionCommandHandler.cs` |
| 22 | Combat | Cold, Poison, and Stun applied simultaneously | Cold slows speed; Stun freezes action; Poison damage and duration blend with existing DoT via weighted formula. | `009-core-combat-and-missiles`, `research.md` (§3) |
| 23 | Monster AI | Player pulls monster beyond `PursuitRadiusMps` | Monster abandons target; enters `LeashReturn`; regenerates 100% HP; returns to `SpawnMpsX, SpawnMpsY`. | `010-core-monster-ai-and-progression`, `MonsterAiTickSystem.cs` |
| 24 | Loot | Two players click pickup (`0x5A`) on same item at same frame | First processed command claims item atomically on single-threaded zone loop; second receives no-op; zero duplication. | `011-core-loot-and-item-generation`, `InventoryLootCommandHandler.cs` |
| 25 | Loot | Non-killer attempts to pickup loot during protection window | Pickup command rejected while `CurrentFrame < OwnerProtectionExpireFrame` (180 frames = 10s); killer only. | `011-core-loot-and-item-generation`, `GroundObjectSlot.cs` |
| 26 | Chat | Player sends message to channel disabled in their `SubscribedChatChannelMask` | Command discarded by `ChatCommandHandler`; no broadcast emitted; sender does not receive error. | `016-module-chat-channels`, `ChatCommandHandler.cs` |

---

## 4. Exact Mapping of Requirements into the 12 Target Spec Profiles

In accordance with `docs/specs-roadmap.md`, the monolithic specifications (`001`–`004`) are mapped into **12 single-responsibility SpecKit profiles** (`001`–`011` in Phase 1A, and `016` in Phase 2):

```
Target Specs Directory Layout:
specs/
├── 001-core-game-data-pipeline/
├── 002-core-legacy-wire-protocol/
├── 003-core-auth-roster-and-leases/
├── 004-core-zone-spine-and-world-entry/
├── 005-core-spatial-aoi-and-spawns/
├── 006-core-movement-and-obstacles/
├── 007-core-portals-and-map-handoff/
├── 008-core-inventory-and-equipment/
├── 009-core-combat-and-missiles/
├── 010-core-monster-ai-and-progression/
├── 011-core-loot-and-item-generation/
└── 016-module-chat-channels/
```

### Profile 1: `specs/001-core-game-data-pipeline`
- **Single-Responsibility Scope**: `JXData Standard v1` canonical authoring format (`data/**/*.json` + `schemas/*.schema.json`), `MemoryPack` binary delivery snapshots (`artifacts/*.pack`), immutable `I*Catalog` registries in `JXServer.GameAssets`, zero runtime legacy file parsers, and offline `tools/JXServer.DataExporter`.
- **Derived From Existing Specs**:
  - `_archive_m1_m4/003-combat-missiles-loot-inventory/research.md` (§1: Dual-Tier Data Standard)
  - `_archive_m1_m4/003-combat-missiles-loot-inventory/contracts/zone-combat-and-inventory-ports.md` (§1: Catalog interfaces)
  - `docs/data-architecture-standard.md`
- **Contracts to Formulate**:
  - `contracts/canonical-data-schemas.md`
  - `contracts/catalog-registries.md`
  - `contracts/offline-exporter-cli.md`
- **Code Mapping (`src/` & `tests/`)**:
  - `src/JXServer.GameAssets/` (all `*Catalog.cs`, `*Template.cs`, `Snapshots/`)
  - `schemas/*.schema.json` & `data/**/*.json`
  - `tools/JXServer.DataExporter/`
  - `tests/JXServer.GameAssets.Tests/` (or schema validation & snapshot tests)
- **Verification Gate**:
  - 100% schema validation of all JSON documents under `data/` via `dotnet test` or JSON schema validator.
  - Zero legacy file reader types (`IniFileParser`, `TabFileParser`, `XPackArchiveReader`) compiled in runtime `JXServer.GameAssets`.

---

### Profile 2: `specs/002-core-legacy-wire-protocol`
- **Single-Responsibility Scope**: 2-byte length-prefixed TCP framing (`JxFramePipeProcessor`), `ProtocolSizeTable`, 44-byte initial handshake key obfuscation (`AccountBeginS2CPacket` `0x20`), `KsgStreamCipher` (5,679-entry `g_uPublicKeys` table with rolling LCG key mutation), second-layer XOR ciphers (`0x3042` & `0x38BC`), and boundary `TCVN3`/`GBK` $\leftrightarrow$ `UTF-8` transcoders.
- **Derived From Existing Specs**:
  - `_archive_m1_m4/001-gateway-world-entry/contracts/bishop-gateway-protocol.md` (§1: Framing & Cipher)
  - `_archive_m1_m4/001-gateway-world-entry/research.md` (§1: Ground-Truth Binary Protocol)
  - `_archive_m1_m4/002-spatial-aoi-movement-chat/contracts/chat-protocol.md` (Variable framing size table)
- **Contracts to Formulate**:
  - `contracts/framing-and-ciphers.md`
  - `contracts/protocol-size-table.md`
  - `contracts/boundary-transcoding.md`
- **Code Mapping (`src/` & `tests/`)**:
  - `src/JXServer.Adapter.LegacyJX1/Framing/` (`JxFramePipeProcessor.cs`, `ProtocolSizeTable.cs`)
  - `src/JXServer.Adapter.LegacyJX1/Cryptography/` (`KsgStreamCipher.cs`, `SecondLayerXorCipher.cs`)
  - `src/JXServer.Primitives/Encodings/` (`Tcvn3Encoding.cs`, `GbkEncoding.cs`)
  - `tests/JXServer.Protocol.Tests/` (`FramingAndCipherTests.cs`, `EncodingTests.cs`)
- **Verification Gate**:
  - All 59 existing tests in `JXServer.Protocol.Tests` pass 100%.

---

### Profile 3: `specs/003-core-auth-roster-and-leases`
- **Derived From Existing Specs**:
  - `_archive_m1_m4/001-gateway-world-entry/spec.md` (FR-001..FR-008, all Clarifications on auto-provisioning, deletion password, kick-online, starter map fallback)
  - `_archive_m1_m4/001-gateway-world-entry/contracts/bishop-gateway-protocol.md` (§2: Gateway Packets `0x41`, `0x37`, `0x45`, `0x3A`, `0x8C`, `0x64`, `0x34`)
  - `_archive_m1_m4/001-gateway-world-entry/contracts/application-and-zone-ports.md` (`IAccountAppService`, `ICharacterAppService`, `ISessionLeaseAppService`)
  - `_archive_m1_m4/004-zone-modular-spine/contracts/character-persistence-contracts.md` (§4.3: Lease-Epoch fencing in `SessionLeaseAppService`)
- **Single-Responsibility Scope**: Bishop Gateway listener (`5622`), account authentication (`0x41`) with dev auto-provisioning vs strict auth, 3-slot character roster listing (`0x37`), creation across 5 elements and starter villages (`0x45`), deletion (`0x3A`), selection (`0x64`), monotonic `SessionLease` fencing (`bKickOnline`, `LeaseEpoch`), and single-use 16-byte world handoff tickets (`0x34`).
- **Contracts to Formulate**:
  - `contracts/bishop-gateway-protocol.md`
  - `contracts/control-plane-lease-services.md`
- **Code Mapping (`src/` & `tests/`)**:
  - `src/JXServer.Domain/Accounts/`, `src/JXServer.Domain/Characters/`, `src/JXServer.Domain/SessionLeases/`
  - `src/JXServer.Application/Accounts/`, `src/JXServer.Application/Characters/`, `src/JXServer.Application/SessionLeases/`
  - `src/JXServer.Adapter.LegacyJX1/Bishop/` (`BishopGatewayServer.cs`, `BishopClientSession.cs`)
  - `tests/JXServer.Integration.Tests/` (`SessionLeaseFencingIntegrationTests.cs`, `BishopGatewayIntegrationTests.cs`)
- **Verification Gate**:
  - Dual-login race serialization, orphaned ticket expiry, and lease epoch eviction tests pass.

---

### Profile 4: `specs/004-core-zone-spine-and-world-entry`
- **Derived From Existing Specs**:
  - `_archive_m1_m4/004-zone-modular-spine/` (`spec.md`, `contracts/zone-modular-spine-and-harness.md`, `contracts/domain-outbound-sinks.md`, `contracts/character-persistence-contracts.md`)
  - `_archive_m1_m4/001-gateway-world-entry/contracts/gameserver-entry-protocol.md` (World entry burst `0x42` $\rightarrow$ `0x49`, `0x44`, `0x45`, `0x46`, `0x4C`, `0xA4`, `0x43`, and heartbeats `0x82`/`0x70`/`0x99`/`0x9B`)
- **Single-Responsibility Scope**: Open/Closed 18-Hz `MapZoneWorker` spine (`IZoneModule`, `IZoneCommandHandler<T>`, `IZoneTickSystem`, `ZoneTickPhase`, `ZoneExecutionContext`, `ZoneAssetServices`, `PlayerFeatureSlotKey<T>`, per-session fault isolation), domain-segregated outbound channels (`IWorldOutboundSink`, `ICombatOutboundSink`, `IInventoryOutboundSink`, `IChatOutboundSink`), foundational `Character` and `CharacterItem` persistence fields (`TaskValues`, `RepositoryPasswordHash`, `IsBroken`, `EnchaseLevel`), world entry handshake (`0x42` $\rightarrow$ `0x43`), continuous heartbeats, and `ZoneSimulationHarness`.
- **Contracts to Formulate**:
  - `contracts/zone-modular-spine-and-harness.md`
  - `contracts/domain-outbound-sinks.md`
  - `contracts/character-persistence-contracts.md`
  - `contracts/gameserver-entry-and-heartbeat-protocol.md`
- **Code Mapping (`src/` & `tests/`)**:
  - `src/JXServer.ZoneEngine/WorldZones/` (`IZoneModule.cs`, `IZoneCommandHandler.cs`, `IZoneTickSystem.cs`, `ZoneTickPhase.cs`, `ZoneExecutionContext.cs`, `ZoneAssetServices.cs`, `ZoneExtensionBuilder.cs`, `ZoneExtensionRegistry.cs`, `CoreZoneModule.cs`, `WorldSessionCommandHandler.cs`, `SessionHeartbeatZoneTickSystem.cs`, `MapZoneWorker.cs`, `ZoneClusterRouter.cs`)
  - `src/JXServer.ZoneEngine/PlayerSessions/` (`IWorldOutboundSink.cs`, `ICombatOutboundSink.cs`, `IInventoryOutboundSink.cs`, `IChatOutboundSink.cs`, `IPlayerOutboundSink.cs`, `SelectivePlayerOutboundSink.cs`, `PlayerFeatureSlotKey.cs`, `IPlayerFeatureSlot.cs`)
  - `src/JXServer.Adapter.LegacyJX1/GameServer/` (`GameServerClientSession.cs`, `GameServerClientSession.WorldSink.cs`)
  - `tests/JXServer.ZoneEngine.Tests/SimulationHarnesses/` (`ZoneSimulationHarness.cs`, `PlayerOutboundEventRecorder.cs`, `SimulatedPlayerHandle.cs`)
- **Verification Gate**:
  - Step 1,000 virtual frames in < 5ms with `FakeTimeProvider` and fixed seed.
  - Fault containment test: a throwing command or tick step isolates to that player session and logs EventId 3010/3011 without crashing other sessions.

---

### Profile 5: `specs/005-core-spatial-aoi-and-spawns`
- **Derived From Existing Specs**:
  - `_archive_m1_m4/002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` (§1: Entity Detail Request & AOI Sync Packets `0x48`, `0x49`, `0x4C`, `0x4D`, `0x4E`)
  - `_archive_m1_m4/002-spatial-aoi-movement-chat/research.md` (§1: `Region_S.dat` NPC Section; §3: 3x3 Region AOI Partitioning)
- **Single-Responsibility Scope**: `SpatialRegionGrid` (`16x32` cells / `512x1024` Mps), entering/leaving `3x3` region set-difference visibility (`0x4C`, `0x4D`, `0x4E`), on-demand entity queries (`0x48`, `0x49`), and static NPC/monster placement from `*.map.json` (`spawnGroups`, `global-spawns.npcs.json`, `event-spawns.npcs.json`).
- **Contracts to Formulate**:
  - `contracts/spatial-grid-and-aoi-protocol.md`
  - `contracts/entity-spawn-contracts.md`
- **Code Mapping (`src/` & `tests/`)**:
  - `src/JXServer.ZoneEngine/SpatialGrid/` (`SpatialRegionGrid.cs`, `SpatialRegionSlot.cs`)
  - `src/JXServer.ZoneEngine/NpcEntities/` (`NpcZoneState.cs`)
  - `src/JXServer.ZoneEngine/WorldZones/WorldSessionCommandHandler.cs` (`RequestEntitySyncZoneCommand`)
  - `src/JXServer.GameAssets/NpcTemplates/` (`NpcTemplateCatalog.cs`)
- **Verification Gate**:
  - 3x3 region boundary crossing diff tests and multi-entity visibility synchronization.

---

### Profile 6: `specs/006-core-movement-and-obstacles`
- **Derived From Existing Specs**:
  - `_archive_m1_m4/002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` (§2: Movement & Posture Packets `0x4B`, `0x4C`, `0x50`, `0x51`, `0x4E`, `0x54`, `0x71`, `0x53`)
  - `_archive_m1_m4/002-spatial-aoi-movement-chat/research.md` (§4: Authoritative Movement, Obstacle Validation & Posture Regen)
- **Single-Responsibility Scope**: Anti-cheat `dwCheckValue` verification, deterministic per-frame walk/run progression (`0x4B` $\rightarrow$ `0x50`, `0x4C` $\rightarrow$ `0x51`), jump (`0x4E` $\rightarrow$ `0x54`), sit/stand posture and vital regeneration (`0x71` $\rightarrow$ `0x53`), and `Obstacle_Full` path clamping.
- **Contracts to Formulate**:
  - `contracts/movement-and-posture-protocol.md`
  - `contracts/obstacle-clamping-rules.md`
- **Code Mapping (`src/` & `tests/`)**:
  - `src/JXServer.Primitives/WorldZones/MovementAntiCheatConsts.cs`
  - `src/JXServer.ZoneEngine/WorldZones/MovementCommandHandler.cs` (`MovePlayerZoneCommand`, `JumpPlayerZoneCommand`, `TogglePlayerSitZoneCommand`)
  - `src/JXServer.ZoneEngine/WorldZones/MovementAndPostureZoneTickSystem.cs` (Phase 500)
- **Verification Gate**:
  - Obstacle clamping unit tests, check value anti-cheat tests, and asymmetric `0x50` vs `0x51` wire serialization tests.

---

### Profile 7: `specs/007-core-portals-and-map-handoff`
- **Derived From Existing Specs**:
  - `_archive_m1_m4/002-spatial-aoi-movement-chat/contracts/aoi-and-movement-protocol.md` (§3: Map Transitions `0x49`/`0x4E` vs `0x35`)
  - `_archive_m1_m4/002-spatial-aoi-movement-chat/research.md` (§2: Trap Script Hashing & Portal Actions; §5: Same-Server vs Cross-Server Transitions)
- **Single-Responsibility Scope**: Edge-triggered cell `TrapId` detection (`LastTriggeredTrapId` anti-loop), town-gate `FightMode` (`0 \leftrightarrow 1`) toggles, level-gate checks and bounce mechanics, same-node in-memory `NewWorld` transitions (`0x49` + `0x4E` without cipher reset), and cross-node redirect (`0x35` `NotifyPlayerExchangeS2CPacket`).
- **Contracts to Formulate**:
  - `contracts/portal-traps-and-handoff-protocol.md`
  - `contracts/cluster-router-transfer-port.md`
- **Code Mapping (`src/` & `tests/`)**:
  - `src/JXServer.ZoneEngine/WorldZones/MovementAndPostureZoneTickSystem.cs` (trap detection)
  - `src/JXServer.ZoneEngine/WorldZones/ZoneClusterRouter.cs` (`TryTransferPlayerAcrossMaps`)
  - `src/JXServer.GameAssets/WorldMaps/TrapScriptCatalog.cs`
- **Verification Gate**:
  - Anti-loop trap verification, level gate bounce, and seamless same-node transfer without TCP disconnection or cipher reset.

---

### Profile 8: `specs/008-core-inventory-and-equipment`
- **Derived From Existing Specs**:
  - `_archive_m1_m4/003-combat-missiles-loot-inventory/contracts/inventory-and-ground-loot-protocol.md` (§2: Inventory Sync, Pickup, Move, Consume & Drop `0x5F`, `0x68`, `0x5B`, `0x61`, `0x8E`, `0x59`, `0x60`, `0x5E`, `0x83`, `0x85`)
  - `_archive_m1_m4/003-combat-missiles-loot-inventory/research.md` (§5: Five-Elements Activation Matrix & 6x10 Inventory Grid)
- **Single-Responsibility Scope**: Spatial container grids (`pos_hand`, `pos_equip` 15 parts, `pos_equiproom` `6x10`, `pos_immediacy` belt), `width x height` footprint collision, item move/swap/split/drop (`0x5B`, `0x85`, `0x5E`, `0x83`), equipping stat and Five-Elements hidden affix activation chain (`KItemList::ms_ActivedEquip`), potion regen (`0x59`, 9-frame ticks), and Town Portal (`Thổ Địa Phù` via `0x87` sub 0x02).
- **Contracts to Formulate**:
  - `contracts/inventory-and-container-protocol.md`
  - `contracts/five-elements-equipment-activation.md`
- **Code Mapping (`src/` & `tests/`)**:
  - `src/JXServer.ZoneEngine/Items/` (`PlayerInventoryGrid.cs`, `PlayerItemSlot.cs`, `InventoryLootCommandHandler.cs`)
  - `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.InventorySink.cs`
- **Verification Gate**:
  - 2D footprint collision matrix tests, 5-element hidden affix ring activation tests, and cursor-hand disconnect recovery tests.

---

### Profile 9: `specs/009-core-combat-and-missiles`
- **Derived From Existing Specs**:
  - `_archive_m1_m4/003-combat-missiles-loot-inventory/contracts/combat-and-skills-protocol.md` (§1: Skill Casting, Auto-Attack & Aura; §2: Combat Hurt, Death, Status Effects)
  - `_archive_m1_m4/003-combat-missiles-loot-inventory/research.md` (§2: 64-Direction Trig & Missiles; §3: Five-Elements Combat & Resistances)
- **Single-Responsibility Scope**: Skill execution (`0x4D` with `0x3AB9` check key, wrapped `0x87`), aura switching (`0x6F`), cooldown/mana/range checks, deterministic 64-direction fixed-point `ActiveMissileSlot` simulation (`SixtyFourDirectionTrigTable`), 6 damage channels (Physics, Cold, Fire, Light, Poison, Magic), Five-Elements damage & resistance math, and Cold/Poison/Stun status effects (`0x56` `do_hurt`).
- **Contracts to Formulate**:
  - `contracts/skill-execution-and-missiles-protocol.md`
  - `contracts/five-elements-combat-damage-formulas.md`
- **Code Mapping (`src/` & `tests/`)**:
  - `src/JXServer.Primitives/Trigonometry/SixtyFourDirectionTrigTable.cs`
  - `src/JXServer.ZoneEngine/Combat/` (`MissileSimulationTickSystem.cs`, `StatusEffectZoneTickSystem.cs`, `MissileZoneTickSystem.cs`, `CombatProgressionCommandHandler.cs`, `CombatConsts.cs`)
  - `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.CombatSink.cs`
- **Verification Gate**:
  - 64-direction trigonometric vector tests, 5-element damage & resistance parity tests, and status effect interval blending tests.

---

### Profile 10: `specs/010-core-monster-ai-and-progression`
- **Derived From Existing Specs**:
  - `_archive_m1_m4/003-combat-missiles-loot-inventory/contracts/combat-and-skills-protocol.md` (§2: NPC Death & Revive `0x57`, `0x74`, `0x89`; §3: Exp, Level-Up & Allocation `0x5C`, `0x5B`, `0x57`, `0x5D`, `0x58`, `0x5E`)
  - `_archive_m1_m4/003-combat-missiles-loot-inventory/research.md` (§4: Monster AI State Machine, Exp & Revival)
- **Single-Responsibility Scope**: 18-Hz monster state machine (`Patrol \rightarrow Aggro \rightarrow Attack \rightarrow Leash \rightarrow Dead/Respawn`, `MonsterAiTickSystem` Phase 200), XP and level-up thresholds, attribute (`0x57` $\rightarrow$ `0x5D`) and skill point (`0x58` $\rightarrow$ `0x5E`) allocation, player death (`0x57`), and town revival (`0x74` $\rightarrow$ `0x89`).
- **Contracts to Formulate**:
  - `contracts/monster-ai-state-machine.md`
  - `contracts/progression-and-revival-protocol.md`
- **Code Mapping (`src/` & `tests/`)**:
  - `src/JXServer.ZoneEngine/Combat/` (`MonsterAiTickSystem.cs`, `MonsterAiZoneTickSystem.cs`, `CombatProgressionCommandHandler.cs`)
  - `src/JXServer.GameAssets/NpcTemplates/`
- **Verification Gate**:
  - Patrol-aggro-leash-respawn cycle in `ZoneSimulationHarness`, XP overflow across multiple levels, and death/revival state reset.

---

### Profile 11: `specs/011-core-loot-and-item-generation`
- **Derived From Existing Specs**:
  - `_archive_m1_m4/003-combat-missiles-loot-inventory/contracts/inventory-and-ground-loot-protocol.md` (§1: Ground Objects `0x64`, `0x67`, `0x4A`, `0x72`, and pickup `0x5A`)
  - `_archive_m1_m4/003-combat-missiles-loot-inventory/research.md` (§5: Weighted Loot Drops & Blue 6-Affix Generator)
- **Single-Responsibility Scope**: Weighted `LootTable` evaluation (`treasureDropCount`), `KItemGenerator` (`m_wVersion = 4`) white and 6-affix Blue equipment rolling (`weightsByEquipPart`, `weightsBySeries`, Luck, seed), `GroundObjectSlot` lifecycle (`0x64`, `0x67`), killer ownership protection window (180 frames = 10s), ground item despawn (1080 frames = 60s), and atomic pickup (`0x5A`).
- **Contracts to Formulate**:
  - `contracts/ground-loot-objects-protocol.md`
  - `contracts/item-generation-and-loot-tables.md`
- **Code Mapping (`src/` & `tests/`)**:
  - `src/JXServer.GameAssets/Items/ItemGenerator.cs`
  - `src/JXServer.GameAssets/LootTables/LootTableCatalog.cs`
  - `src/JXServer.ZoneEngine/Items/` (`GroundObjectSlot.cs`, `GroundObjectTickSystem.cs`, `GroundObjectZoneTickSystem.cs`, `InventoryLootCommandHandler.cs`)
- **Verification Gate**:
  - 10,000-item affix distribution unit tests, concurrent same-frame pickup race tests (zero dupes).

---

### Profile 12: `specs/016-module-chat-channels`
- **Derived From Existing Specs**:
  - `_archive_m1_m4/002-spatial-aoi-movement-chat/contracts/chat-protocol.md` (Entire file: `0x60`, `0x52`, `0x5B`, `0x56`, `0x7E`, `0xFB`)
  - `_archive_m1_m4/002-spatial-aoi-movement-chat/research.md` (§6: Multi-Channel Chat Framing & Routing)
  - `_archive_m1_m4/004-zone-modular-spine/contracts/domain-outbound-sinks.md` (`IChatOutboundSink`, `GameServerClientSession.ChatSink.cs`)
- **Single-Responsibility Scope**: **Golden Reference Plug-in Module**: Nearby spatial speech (`0x52` $\rightarrow$ `0x5B`), structured multi-channel routing (`0x56` $\rightarrow$ `0x7E` across Nearby, World, Whisper, Team, Faction, Tong, Screen), channel subscription bitmasks (`0x60`), system broadcasts (`0xFB`), cluster-wide chat routing (`DeliverClusterChatZoneCommand`), registered as decoupled `ChatZoneModule : IZoneModule`.
- **Contracts to Formulate**:
  - `contracts/chat-channels-wire-protocol.md`
  - `contracts/chat-zone-module-registration.md`
- **Code Mapping (`src/` & `tests/`)**:
  - `src/JXServer.ZoneEngine/Chats/` (`ChatCommandHandler.cs`, `ChatZoneModule.cs`, `ChatZoneModuleRegistrationExtensions.cs`)
  - `src/JXServer.ZoneEngine/PlayerSessions/IChatOutboundSink.cs`
  - `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.ChatSink.cs`
- **Verification Gate**:
  - Multi-channel routing parity tests; whisper echo and filtering tests; verification that `MapZoneWorker.cs` contains 0 lines of chat code.

---

## 5. Ground Truth Preservation Plan (`specs/_archive_m1_m4/`)

To guarantee **zero regression** and **zero data loss**:

1. **Step 0 — Non-Destructive Archiving**:
   - Create `specs/_archive_m1_m4/`.
   - Copy or move `specs/001-gateway-world-entry`, `specs/002-spatial-aoi-movement-chat`, `specs/003-combat-missiles-loot-inventory`, and `specs/004-zone-modular-spine` into `specs/_archive_m1_m4/`.
   - All 38 existing specification files are retained with byte-for-byte exactness.
2. **Step 1 & 2 — 100% Content Retention Rule**:
   - Every hex test vector (e.g. `0x24A1`, `0x1D78`, `0x3AB9`, `0x3042`, `0x38BC`, `0x00010032`, `0xFB`), byte offset table, Little-Endian struct layout, C++ formula (`g_nSin`, `g_nCos`, `g_GetDirIndex`, `CalcDamage`, `ReceiveDamage`, `ms_ActivedEquip`), and clarification Q&A from `_archive_m1_m4/` is distributed into its corresponding single-responsibility target spec under `specs/`.
   - No specification detail is dropped or simplified.
3. **Step 3 — Verification & Post-Convergence Teardown**:
   - Each of the 12 new specs undergoes `/speckit-analyze` and `/speckit-converge` verification against actual C# source code and project constitution.
   - Once all 12 specs are verified as `100% Converged`, `specs/_archive_m1_m4/` is automatically purged.

---

## 6. Logic Chain

1. **Observation**: The current codebase passes 97/97 tests, but `MapZoneWorker.cs` is a 2,975-line monolithic class bundling all core commands, tick phases, chat, and sinks.
2. **Observation**: `specs/004-zone-modular-spine/contracts/` already specifies the exact contracts for decoupling `MapZoneWorker.cs` into `IZoneModule`, `IZoneCommandHandler`, `IZoneTickSystem`, `PlayerFeatureSlotKey`, and domain outbound sinks.
3. **Observation**: `docs/specs-roadmap.md` defines a 30-spec roadmap where Phase 1A consists of re-baselining existing verified features into 11 single-responsibility core specs (`001`–`011`) plus the golden reference chat module (`016`).
4. **Logical Inference**: By systematically refactoring the existing code to use the modular spine, we can keep all existing logic intact (zero regression), while separating `MapZoneWorker.cs` into single-responsibility handlers and tick systems.
5. **Logical Inference**: By preserving the existing specs in `_archive_m1_m4/` during the restructuring, the 12 new specs can be populated with 100% fidelity to the original byte layouts, formulas, and test vectors.
6. **Conclusion**: The survey is complete, and the exact architectural boundaries and specification mappings for all 12 target specs are fully specified and ready for execution.

---

## 7. Caveats

- **Scope Boundary**: As surveyed, gameplay rules for future milestones (`012`–`015` in Phase 1B and `017`–`028` in Phase 2, such as NPC dialog scripts, shops, storage box UI, PK state machine, and guilds) are deliberately out of scope for this re-baselining phase and belong to their respective dedicated specs.
- **Assumed Clean Compilation**: The refactored modular spine code must maintain `TreatWarningsAsErrors=true` (0 warnings) and strictly respect 1 type per `.cs` file.

---

## 8. Conclusion

The specification and architecture survey confirms:
1. Complete ground-truth coverage across wire protocol byte layouts, stream ciphers, 64-dir trig tables, Five-Elements formulas, and lease fencing.
2. Exact 1:1 mapping from existing monolithic specifications (001–004) into the 12 standardized SpecKit target directories (`001`–`011`, `016`).
3. Complete readiness to execute the Zero-Regression Re-baseline plan:
   - Step 0: Move existing specs to `specs/_archive_m1_m4/`.
   - Step 1: Implement Modular Spine, domain sinks, persistence attributes, and simulation harness on existing code.
   - Step 2: Generate the 12 standardized SpecKit profiles with full contracts and tasks.
   - Step 3: Run convergence audits, reach 100% Converged status across all 12 specs, and clean up `_archive_m1_m4/`.

---

## 9. Verification Method

- **Build Verification**:
  ```powershell
  dotnet build "d:\Projects\jx-server\JXServer.slnx" -c Debug
  ```
  *Requirement: Exits with code 0 and 0 warnings.*
- **Unit & Integration Test Verification**:
  ```powershell
  dotnet test "d:\Projects\jx-server\JXServer.slnx" -c Debug
  ```
  *Requirement: All 97 tests pass with 0 failures.*
- **Simulation Harness Verification**:
  ```powershell
  dotnet test "d:\Projects\jx-server\tests\JXServer.ZoneEngine.Tests\JXServer.ZoneEngine.Tests.csproj" --filter "FullyQualifiedName~SimulationHarness"
  ```
  *Requirement: Steps 1,000 frames in < 5ms with deterministic output.*
