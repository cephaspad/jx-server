# Feature Specification: Spatial 3x3 Region AOI, Movement, Map Transitions & Multi-Channel Chat

**Feature Branch**: `002-spatial-aoi-movement-chat`

**Created**: 2026-11-09

**Status**: Draft

**Input**: User description: "Milestone 2: Spatial 3x3 region Area-of-Interest (AOI) visibility and entity synchronization, static NPC and monster spawning from map region data and NPC templates, obstacle-validated walk/run/jump/sit movement with anti-cheat verification, boundary portal trap map transitions, and multi-channel player chat compatible with the original game client and autonomous bot swarms."

## Clarifications

### Session 2026-11-09

- Q: How should static NPC display names and spawn sources be resolved when binary `KSPNpc.szName` in `Region_S.dat` contains raw Chinese GBK strings while `settings/npcs.txt` and `script/global/autoexec.lua` contain localized Vietnamese names? → A: Parse static NPCs from both `Region_S.dat` (`NPC_SECTION`: 12-byte `KNpcFileHead` + `60 + nScriptNameLen`-byte `KSPNpc` records) and `script/global/autoexec.lua` (`adddialognpc` entries), resolving display names from `settings/npcs.txt` (`TCVN3` → `UTF-8`) by `nTemplateID` whenever `autoexec.lua` does not provide an explicit override.
- Q: How should region trap identifiers (`uTrapId` from `TRAP_SECTION`) be resolved and what trap actions must Milestone 2 execute? → A: Scan and hash trap scripts under `script/**/trap/*.lua` using `g_FileName2Id` (`PakFilePathHasher.ComputeHash`) alongside configurable trap bindings, supporting both **Town Gate Fight-State Traps** (toggling `FightMode` `0 <-> 1` and nudging position on the same map via `SetPos(cellX, cellY)` / `0x4E`) and **Cross-Map Portal Traps** (`NewWorld(targetMapId, cellX, cellY)` with minimum level checks that bounce under-leveled players back via `SetPos` and send a `0xFB` system message).
- Q: How should the 18-Hz zone loop advance walking/running coordinates and enforce obstacle checks along the movement path? → A: Upon validating `dwCheckValue` and initial cell walkability, immediately broadcast `s2c_npcwalk` (`0x50`) or `s2c_npcrun` (`0x51`) to the `3x3` region AOI, then step the character along the direction vector each 18-Hz tick at their `WalkSpeed` (default `10` Mps/tick) or `RunSpeed` (default `20` Mps/tick), halting at the last walkable coordinate (`do_stand` + `0x4E` sync) if a stepped cell has `Obstacle_Full == 1` or when reaching the destination.
- Q: How should `3x3` region AOI neighbor discovery, periodic updates, and departure/disconnection be synchronized? → A: On world entry or region boundary crossing, compute the entering/leaving `3x3` region set difference, send full `s2c_syncnpc` (`0x4C`, `61B`) for all players and NPCs in newly entered regions to the player (and broadcast the player's `0x4C` to observers in newly entered regions), broadcast periodic `s2c_syncnpcmin` (`0x4D`, `18B`) / `s2c_syncnpcminplayer` (`0x4E`, `13B`) within the current `3x3` AOI, and answer on-demand `c2s_requestnpc` (`0x49`) / `c2s_requestplayer` (`0x48`) queries with `0x4C`.
- Q: How should variable-length chat frames (`0x52`, `0x56`) be validated by the protocol framing layer and how should cross-map chat channels be routed? → A: Extend `ProtocolSizeTable` to validate variable-length C2S opcodes (`0x52`, `0x56`) via their uint16 length header (`wLengthMinusOne + 1 == payload.Length`), broadcast `0x52` (`-> 0x5B`) and `0x56` Nearby (`Channel 2`/`7` `-> 0x7E`) inside the speaker's `3x3` region AOI, and route `World` (`Channel 1`), `Whisper` (`Channel 3`), `Team` (`Channel 4`), and `Faction` (`Channel 5`) across all active `MapZoneWorker` shards via `IZoneClusterRouter` respecting each recipient's `0x60` (`c2s_chatsetchannel`) subscription bitmask.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Spatial 3x3 Region Visibility & Neighbor Synchronization (Priority: P1)

When multiple players (using the original game client or autonomous bots) and non-player characters (NPCs) occupy the same map, each player automatically sees all other players and NPCs within their surrounding 3x3 region Area of Interest (AOI). As players enter the world, move across region boundaries, or log out, surrounding observers immediately receive accurate appearance, position, posture, and departure updates without needing to reload the map.

**Why this priority**: A shared multiplayer world requires players and bots to see one another and nearby town NPCs; 3x3 region spatial partitioning is the core foundation for movement broadcasting, local chat, and future combat interactions.

**Independent Test**: Can be fully tested by connecting two or more clients/bots to Ba Lăng Huyện (Map `53`) at the same spawn region, verifying that each client immediately receives the full identity and coordinates of nearby players and static town NPCs, and confirming that when one player logs out or moves beyond the 3x3 region boundary, they disappear cleanly from the other player's view.

**Acceptance Scenarios**:

1. **Given** an active map containing static NPCs and at least one online player, **When** a second player completes world entry into the same 3x3 region neighborhood, **Then** both players receive each other's presence and position updates, and the entering player receives synchronization for all static NPCs within their 3x3 region neighborhood.
2. **Given** a client that receives a compact position update for an unfamiliar player or NPC within its 3x3 region view, **When** the client requests full entity details for that identifier, **Then** the server responds with the entity's full name, camp, five-elements series, level, posture, and appearance attributes.
3. **Given** two players standing within the same 3x3 region neighborhood, **When** one player moves into a region outside the other's 3x3 neighborhood or disconnects from the game, **Then** the newly entered regions' entities are synchronized to the moving player while observers who are no longer within the 3x3 neighborhood stop receiving periodic position broadcasts for that player.

---

### User Story 2 - Obstacle-Validated Movement, Posture & Step Progression (Priority: P2)

A player or autonomous bot issues commands to walk, run, jump, or sit/stand within an active map. The server verifies the action's security checksum, validates the movement path against the map's obstacle grid, advances the character's position deterministically across simulation ticks at their movement speed, and broadcasts the movement or posture change to all players within the surrounding 3x3 region neighborhood.

**Why this priority**: Walking, running, and meditating (sitting) are the primary real-time actions players perform to navigate towns, reach NPCs, and recover vital stats.

**Independent Test**: Can be tested independently by logging a bot or game client into Map `53`, issuing walk, run, jump, and sit commands toward both walkable coordinates and blocked obstacle cells, and verifying that valid movements advance the character's position and broadcast to nearby observers while blocked or tampered commands are rejected.

**Acceptance Scenarios**:

1. **Given** an active in-world character standing on a walkable cell, **When** the client sends a valid walk (`0x4B`) or run (`0x4C`) command with a valid security check value toward a reachable destination, **Then** the server updates the character's movement state, broadcasts the walk (`0x50`) or run (`0x51`) trajectory to all observers in the 3x3 region neighborhood, advances the character's world coordinates each simulation tick, and updates region membership whenever the character crosses a region boundary.
2. **Given** an active in-world character, **When** the client attempts to walk or run into a full-obstacle cell or submits a movement command with an invalid security check value, **Then** the server rejects the invalid movement or halts progression at the obstacle boundary and keeps the character at their last valid walkable coordinate.
3. **Given** an active in-world character, **When** the client sends a sit/stand toggle command (`0x71`) or a jump command (`0x4E`) to a valid walkable coordinate, **Then** the server transitions the character's posture or jump destination and broadcasts the corresponding posture (`0x53`) or jump (`0x54`) update to the 3x3 region neighborhood, while sitting characters periodically regenerate health and mana over time.

---

### User Story 3 - Static NPC & Monster Spawning from Map Assets (Priority: P3)

When a map initializes on server startup, the server reads the NPC template catalog and the map's region NPC placement records to spawn all configured town dialog NPCs (such as Xa Phu, Chưởng Quầy, Hiệu Thuốc) and field monsters at their exact designated coordinates, camps, levels, and five-elements series so the world is populated as designed.

**Why this priority**: Populating towns with service NPCs and outskirts with monsters is essential for world authenticity, target selection, and upcoming dialog and combat milestones.

**Independent Test**: Can be tested by starting the server with Ba Lăng Huyện (Map `53`), entering the map with a client or bot, and verifying that all static NPCs defined in the map's region files appear at their exact coordinates with their authentic Vietnamese names, levels, and camps.

**Acceptance Scenarios**:

1. **Given** the static NPC template table (`npcs.txt`) and a map's region placement sections (`Region_S.dat` NPC records), **When** the map zone initializes, **Then** the server instantiates each static NPC with its template attributes, level, camp, five-elements series, and world coordinates inside its corresponding spatial region.
2. **Given** a player walking through a town or field on an active map, **When** a region containing static NPCs enters the player's 3x3 region view (or when the client explicitly queries an NPC's identity via `0x49`), **Then** the server transmits the NPC's synchronization data (`0x4C` / `0x4D`) so the client renders the NPC at the correct location with its proper name and relation state.

---

### User Story 4 - Map Boundary Trap Detection &Seamless Map Transitions (Priority: P4)

When a player walks or runs onto a map portal cell (Trap) at the edge of a town or field, the server detects the trap trigger on the simulation tick, prevents infinite re-trigger loops on adjacent trap cells, and seamlessly transitions the character to the destination map and arrival coordinates (maintaining the existing connection when the destination map is hosted on the same world server, or issuing a cross-server transfer notification when hosted on another node).

**Why this priority**: Players and bots must be able to travel between connected maps (for example, walking out of a starter village gate or moving between adjacent regions) without manual reconnection or account lockups.

**Independent Test**: Can be tested by walking a character onto a configured portal trap cell in Map `53` and verifying that the character transitions immediately to the target destination coordinates (receiving `0x49` world sync and `0x4E` self-position sync on the destination map) without dropping the session.

**Acceptance Scenarios**:

1. **Given** a map region containing cell trap definitions loaded from the map's region data and mapped to a destination map and coordinates, **When** a moving player steps onto a trap cell for the first time, **Then** the server records the triggered trap identifier on the character and initiates a transition to the destination map and coordinates.
2. **Given** a character transitioning to a destination map that is active on the same world server, **When** the transition executes, **Then** the server removes the character from the source map's region grid, attaches the character to the destination map's region grid, sends the new world identity (`0x49`), updated player coordinates (`0x4E`), and nearby 3x3 region entities (`0x4C` / `0x4D`) over the existing connection without requiring a new login handshake.
3. **Given** a character that has just arrived on a destination cell or remains standing on the same trap cell ID, **When** subsequent simulation ticks evaluate trap collisions, **Then** the server suppresses duplicate triggers until the character steps onto a non-trap cell (resetting the last triggered trap state).

---

### User Story 5 - Multi-Channel Player Chat & System Broadcasts (Priority: P5)

Players and autonomous bots communicate with one another in real time using nearby spatial speech (visible to players within the 3x3 region neighborhood) and structured chat channels including World/Public chat, Private/Whisper messages to a specific online player, Faction chat, and System announcements, with full Vietnamese text fidelity.

**Why this priority**: Multi-channel chat enables social interaction among human players, coordination across bot swarms, and administrative system notifications.

**Independent Test**: Can be tested by connecting two clients/bots to the server, exchanging nearby talk messages (`0x52` $\rightarrow$ `0x5B`), world channel messages, and private whisper messages (`0x56` $\rightarrow$ `0x7E`), and verifying that recipients receive the exact sender name and Vietnamese message content according to their active channel subscriptions (`0x60`).

**Acceptance Scenarios**:

1. **Given** two or more players located within the same 3x3 region neighborhood on a map, **When** one player sends a nearby talk message (`0x52`), **Then** all players within that 3x3 region neighborhood receive the speech bubble broadcast (`0x5B`) containing the speaker's runtime identifier and message text, while players outside the 3x3 neighborhood do not receive it.
2. **Given** online players subscribed to chat channels (`0x60`), **When** a player sends a structured chat message (`0x56`) on the Nearby, World, Faction, or Private/Whisper channel, **Then** the server delivers the formatted chat packet (`0x7E`) to the appropriate recipients (3x3 region for Nearby, all subscribed online players for World, matching faction members for Faction, or the specific target player for Whisper).
3. **Given** an administrative or system event, **When** the server dispatches a system info message (`0xFB`), **Then** target clients receive and display the formatted system announcement in their chat window.

---

### Edge Cases

- **High-Density Region Boundary Crossing**: 50 or more players simultaneously cross between adjacent regions in Ba Lăng Huyện; the region grid updates membership and computes entering/leaving region diffs without locks or race conditions.
- **Forged or Desynchronized Anti-Cheat Checksum (`dwCheckValue`)**: A client submits a walk (`0x4B`) or run (`0x4C`) packet whose `dwCheckValue` fails validation against the session's `c2s_syncend` tokens; the server ignores the movement command, logs a security warning, and preserves the character's authoritative position without crashing the zone loop.
- **Out-of-Bounds or Unwalkable Destination Coordinates**: A client sends a walk/run/jump destination outside the map's region bounds or deep inside an impassable obstacle cluster; the server clamps or stops progression at the first blocked cell along the path and synchronizes the character's authoritative position back to the client.
- **Portal Trap to an Unloaded Map**: A player steps onto a portal trap whose target map ID is not currently loaded on the server; the server keeps the player safely at their current position on the source map, resets trap state when they step away, and optionally sends a system notification rather than disconnecting or stranding the character.
- **Oversized, Empty, or Unsubscribed Chat Payloads**: A client sends a chat packet with a mismatched length header, zero-length message, missing null terminator, or targets an offline whisper recipient; the server safely rejects malformed frames and ignores undeliverable messages without affecting server stability.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each active map zone MUST partition its world space into a 2D grid of Regions (each covering `16 x 32` cells or `512 x 1024` world pixels) based on the map's `.wor` bounding rectangle (`left, top, right, bottom`) and maintain dynamic per-region membership lists for players and NPCs.
- **FR-002**: Whenever a player completes world entry, moves into a new region, or changes map, the zone engine MUST compute the entering/leaving set difference of the player's surrounding 3x3 region Area of Interest (up to 9 regions centered on the player's current region), transmit full `s2c_syncnpc` (`0x4C`, 61 bytes) for all players and NPCs residing in newly entered regions to the player, and broadcast the player's `s2c_syncnpc` (`0x4C`) to observers in those newly entered regions.
- **FR-003**: The server MUST support full entity synchronization (`0x4C` `s2c_syncnpc`, packing `NpcSettingIdx = ((uint)Level << 16) | (uint)(ushort)TemplateId`), periodic compact neighbor synchronization (`0x4D` `s2c_syncnpcmin`, 18 bytes), and self-position synchronization (`0x4E` `s2c_syncnpcminplayer`, 13 bytes) within the active 3x3 region AOI, and MUST respond to client entity detail requests (`0x49` `c2s_requestnpc` and `0x48` `c2s_requestplayer`, 5 bytes each) with `0x4C` for any valid entity within the requester's active map.
- **FR-004**: On startup, the asset loader MUST parse the static NPC template catalog (`settings/npcs.txt`), the binary `TRAP_SECTION` (Section `1`: 12-byte `KTrapFileHead` + 8-byte `KSPTrap` horizontal cell runs) and `NPC_SECTION` (Section `2`: 12-byte `KNpcFileHead` + variable-length `60 + nScriptNameLen`-byte `KSPNpc` records) inside each map region's `Region_S.dat` file, and the additional town dialog NPC spawns in `script/global/autoexec.lua` (`adddialognpc`), spawning all static NPCs and monsters into their respective map regions with their template ID, level, camp, five-elements series, movement speed, and localized Vietnamese display name (preferring the explicit `autoexec.lua` name or `settings/npcs.txt` `TCVN3` name over raw GBK `KSPNpc.szName`).
- **FR-005**: The world server MUST validate the 4-byte `dwCheckValue` field on incoming walk (`0x4B`, key `0x24A1`) and run (`0x4C`, key `0x1D78`) packets against the two 32-bit client tokens recorded from `c2s_syncend` (`0x43`) using the canonical check formula (`dwCheckValue != 0` and `((dwCheckValue ^ token1 ^ token2) % key) == 0`), rejecting commands that fail verification.
- **FR-006**: When a valid walk (`0x4B`) or run (`0x4C`) command is received, the zone engine MUST validate cell walkability against the region's `OBSTACLE_SECTION` matrix, transition the character into walking or running posture toward the destination, immediately broadcast `s2c_npcwalk` (`0x50`, packing `NpcID, nMpsY, nMpsX`) or `s2c_npcrun` (`0x51`, packing `nMpsY, NpcID, nMpsX`) to all players in the 3x3 region neighborhood, and advance the character's authoritative coordinates along the movement vector each 18-Hz tick at their `WalkSpeed` (default `10` Mps/tick) or `RunSpeed` (default `20` Mps/tick), stopping (`do_stand` + `0x4E` sync) at the last passable coordinate if a stepped cell has `Obstacle_Full == 1` or when reaching the destination.
- **FR-007**: The world server MUST process character jump commands (`0x4E` `c2s_npcjump` $\rightarrow$ `0x54` `s2c_npcjump`) and posture sit/stand commands (`0x71` `c2s_npcsit` $\rightarrow$ `0x53` `s2c_npcsit`), interrupting sit state automatically when the character initiates movement and periodically regenerating health and mana while the character remains seated.
- **FR-008**: On startup, the server MUST index map trap scripts (`script/**/trap/*.lua`) by their 32-bit `g_FileName2Id` path hash (`PakFilePathHasher.ComputeHash`) alongside configurable trap bindings. During each 18-Hz movement tick, the zone engine MUST check whether a moving player's current cell contains a non-zero `uTrapId`, enforce edge-triggered anti-loop tracking (`LastTriggeredTrapId` only fires when `trapId != LastTriggeredTrapId` and resets to `0` when stepping onto a non-trap cell), and execute either a **Town Gate Fight-State Trap** (toggling `FightMode` `0 <-> 1` and nudging position via `SetPos(cellX, cellY)` / `0x4E`) or a **Cross-Map Portal Trap** (`NewWorld(targetMapId, cellX, cellY)` with minimum level validation that bounces under-leveled players back via `SetPos` and sends a `0xFB` system message).
- **FR-009**: The server MUST support in-memory same-server map transitions (and same-map teleports) by transferring the character's zone state from the source map region to the destination map region, transmitting `s2c_syncworld` (`0x49`, 34 bytes) when the map ID changes, transmitting `s2c_syncnpcminplayer` (`0x4E`, 13 bytes) with the arrival coordinates, and synchronizing the destination's 3x3 region entities (`0x4C` / `0x4D`) without sending `s2c_syncend` (`0x43`) or resetting the TCP stream cipher. When a transition targets a map hosted on an external world server node, the server MUST support issuing `s2c_notifyplayerexchange` (`0x35`, 25 bytes with plain little-endian port and a fresh handoff `GUID`).
- **FR-010**: The protocol framing layer (`ProtocolSizeTable`) MUST validate variable-length chat C2S packets (`0x52` `c2s_playertalk` and `0x56` `c2s_playersendchat`) using their 16-bit length field (`wLengthMinusOne + 1 == payload.Length`), and the world server MUST broadcast `s2c_playertalk` (`0x5B`) to all players within the speaker's 3x3 region neighborhood.
- **FR-011**: The server MUST process channel subscription masks (`0x60` `c2s_chatsetchannel`, 5 bytes) and structured chat messages (`0x56` `c2s_playersendchat`), routing `s2c_chatgetsomeone` (`0x7E`) to the 3x3 region neighborhood for Nearby chat (`Channel 2`/`7`), and across all active `MapZoneWorker` shards via `IZoneClusterRouter` for World chat (`Channel 1`), Private/Whisper chat (`Channel 3`), Team chat (`Channel 4`), and Faction chat (`Channel 5`) according to each recipient's `0x60` subscription bitmask, as well as supporting server system announcements (`0xFB` `s2c_extend` / `SendSystemInfo`).
- **FR-012**: Whenever a player disconnects, logs out, or transitions to another map, their latest authoritative world map ID and pixel coordinates (`MpsX`, `MpsY`) MUST be persisted to durable storage so subsequent logins spawn at their updated location.

### Key Entities *(include if feature involves data)*

- **SpatialRegionSlot**: Represents a single `512 x 1024` pixel (`16 x 32` cell) partition of a map zone, holding its `16 x 32` obstacle matrix, `16 x 32` cell trap identifiers, active player list, and active NPC list.
- **NpcTemplate**: Represents a read-only NPC archetype loaded from `settings/npcs.txt`, defining template ID, display name, entity kind (Monster, Dialog NPC, Partner), default camp, five-elements series, walk speed, run speed, and base life statistics.
- **NpcZoneState**: Represents a live non-player character instance spawned inside a map zone, holding its unique runtime `NpcId`, template reference, level, camp, five-elements series, current world coordinates (`MpsX`, `MpsY`, `RegionX`, `RegionY`), posture (`Doing`), and current/maximum health.
- **PortalTrapBindingTemplate**: Represents the mapping between a region cell's `TrapId` (loaded from `Region_S.dat` Section `1` or map trap configuration) and its target destination (`TargetMapId`, `TargetMpsX`, `TargetMpsY`, and target fight state).
- **DeliverClusterChatZoneCommand**: Represents a routed chat communication containing sender runtime `NpcId`, sender character name (Unicode), channel identifier, optional target/sentence prefix, and message body.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 50 concurrent autonomous bots from `jx-swarm` and a human player using `game.exe` in Ba Lăng Huyện (Map `53`) can simultaneously see one another, walk/run across region boundaries, and exchange chat messages while maintaining the server's 18-Hz simulation loop (`~55.5ms` per tick) with 99th-percentile tick duration under 15 milliseconds.
- **SC-002**: 100% of static NPCs defined in the loaded map regions (including town service NPCs and surrounding monsters in Map `53`) spawn at their exact configured coordinates and display their authentic Vietnamese names and camps on connected clients within 1 second of entering a 3x3 region neighborhood.
- **SC-003**: 100% of walk/run commands targeting impassable obstacle cells or carrying invalid anti-cheat check values are stopped or rejected on the server, preventing any client from clipping through map barriers.
- **SC-004**: Characters stepping onto configured map boundary portal traps transition to the destination map and arrival coordinates in under 500 milliseconds without dropping their network session or causing duplicate trap triggers.
- **SC-005**: 100% of nearby speech (`0x5B`), world chat (`0x7E`), and private whisper (`0x7E`) messages preserve full Vietnamese text fidelity and are delivered exclusively to the intended recipients (3x3 region observers, subscribed global players, or the designated whisper target) in under 200 milliseconds.

## Assumptions

- Combat skill execution (`0x4D` `c2s_npcskill`), 64-direction missile trajectories, monster AI aggro/chasing, death/loot drops, and equipment generation belong to Milestone 3; in Milestone 2, static NPCs and monsters spawn at their designated coordinates and are visible/selectable within the 3x3 region AOI in standing posture.
- Full interactive NPC dialog scripting (`0x6E` `c2s_dialognpc` $\rightarrow$ `0x63` `s2c_scriptaction` menus), party/team formation (`0x53` / `0x69`), friend list persistence, and player-to-player trade belong to Milestone 4; in Milestone 2, portal trap transitions are driven by a data-driven trap destination catalog loaded from server settings/map trap files alongside the binary `TRAP_SECTION` in `Region_S.dat`.
- All wire packet byte layouts (`0x48`, `0x49`, `0x4B`, `0x4C`, `0x4D`, `0x4E`, `0x50`, `0x51`, `0x52`, `0x53`, `0x54`, `0x56`, `0x5B`, `0x60`, `0x71`, `0x7E`, `0xFB`, and `0x35`) follow the verified ground-truth specifications in `jx-swarm/docs` and `jx-win-src` / `jx-linux-bin`.
