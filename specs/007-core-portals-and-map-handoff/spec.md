# Feature Specification: Boundary Portals & Map Handoff

**Feature Branch**: `007-core-portals-and-map-handoff`  
**Created**: 2026-11-11  
**Status**: Draft  
**Input**: Master Roadmap `specs-roadmap.md` (`007`), re-baselined from `specs/_archive_m1_m4/002-spatial-aoi-movement-chat/` and actual codebase.

---

## 1. Executive Summary & Value Proposition

In the vast world of swordsmen and martial arts factions, players constantly journey between starter villages, faction headquarters, expansive fields, and dangerous dungeons. Travel between different maps must occur smoothly without disconnecting the player, losing session state, or resetting transport encryption.

This specification defines the authoritative map portal trap detection, edge-triggered anti-loop protection, town-gate combat state toggles (`FightMode`), seamless same-server in-memory world transitions (`NewWorld`), cross-node cluster redirection, and level-gated travel restrictions for JXServer. Moving characters stepping onto portal cells trigger destination transitions that cleanly transfer zone state between shards on the same node, or hand off to external nodes via single-use cryptographically sealed tickets.

---

## 2. User Scenarios & Acceptance Criteria *(mandatory)*

### User Story 1 — Edge-Triggered Cell Trap Detection & Anti-Loop Protection (Priority: P1)

When a player walks or runs onto a designated portal cell (Trap) at the boundary of a map or city gate, the simulation loop detects the trap identifier on the movement tick, executes the bound travel or state action, and records the active trap ID. As long as the character remains standing on the same trap cell run, subsequent simulation ticks suppress duplicate triggers until the character steps away onto a non-trap cell.

**Why this priority**: Without edge-triggered anti-loop protection, a player standing on a portal cell would trigger hundreds of recursive transitions per minute, crashing the zone or trapping the client in infinite reload loops.

**Independent Test**: Walk a character onto a portal trap cell in Ba Lăng Huyện (Map 53). Confirm that the transition triggers exactly once upon contact. Keep the character stationary on the trap; verify zero duplicate triggers occur. Step off the trap and back on; verify that the trap triggers normally again.

**Acceptance Scenarios**:
1. **Given** a map region containing cell trap definitions, **When** a character steps onto a cell with a non-zero `TrapId`, **Then** the server records `LastTriggeredTrapId = TrapId` and initiates the bound portal or combat state action.
2. **Given** a character that has just triggered a trap and remains on the same trap cell, **When** subsequent 18-Hz simulation ticks evaluate cell occupancy, **Then** the server suppresses duplicate triggers as long as `currentTrapId == LastTriggeredTrapId`.
3. **Given** a character standing on a trap cell, **When** the character moves onto a normal cell with `TrapId == 0`, **Then** the server resets `LastTriggeredTrapId = 0`, re-arming trap detection.

---

### User Story 2 — Same-Node In-Memory Map Transitions (NewWorld) (Priority: P1)

When a portal trap targets a destination map hosted in-memory on the same world server node, the server transfers the player's zone state from the source map worker to the destination map worker without dropping the TCP connection, without resetting the stream cipher, and without performing a full login handshake. The client receives the new map identity, updated arrival coordinates, and full appearance data for entities in the new 3x3 region neighborhood.

**Why this priority**: Fast, seamless transitions between maps hosted on the same server eliminate loading screen delays and ensure uninterrupted gameplay.

**Independent Test**: Walk a player through a town portal leading to an adjacent field map hosted on the same server. Verify that the client receives `0x49` (`s2c_syncworld`) and `0x4E` (`s2c_syncnpcminplayer`) with the arrival coordinates, discovers new entities in the destination map's 3x3 AOI, and continues communicating over the existing cipher stream without disconnecting.

**Acceptance Scenarios**:
1. **Given** an active player stepping onto a portal trap targeting another local map, **When** the transition executes, **Then** the server removes the player from the source map's spatial grid, transfers player state to the destination map worker, transmits `s2c_syncworld` (`0x49`), transmits self-position sync (`0x4E`), and synchronizes the destination's 3x3 region entities (`0x4C` / `0x4D`).
2. **Given** an in-memory map transition, **When** wire packets are exchanged on the active connection, **Then** the server preserves the rolling `KSG_DecodeEncode` cipher state and does NOT emit `s2c_syncend` (`0x43`).

---

### User Story 3 — Town-Gate FightMode Toggles & Boundary Nudging (Priority: P2)

At the boundary gates connecting peaceful starter villages to combat-enabled wilderness fields, special gate traps toggle the character's combat readiness (`FightMode` `0 <-> 1`) and nudge the character slightly forward across the gate boundary onto the appropriate side of the threshold.

**Why this priority**: Towns are safe havens where combat is prohibited, while the wilderness allows martial skill usage. Boundary traps enforce this ruleset seamlessly as players walk through city gates.

**Independent Test**: Walk a character out of Ba Lăng Huyện through the south gate. Verify that `FightMode` toggles from `0` (peaceful) to `1` (combat active), and the character is positioned outside the gate. Walk back in through the gate; verify `FightMode` reverts to `0`.

**Acceptance Scenarios**:
1. **Given** a character with `FightMode == 0` walking out of a town gate, **When** stepping onto the gate trap, **Then** the server updates `FightMode = 1`, nudges the character's coordinates past the gate threshold, and synchronizes the new posture and state.
2. **Given** a character with `FightMode == 1` walking into town, **When** stepping onto the gate trap, **Then** the server updates `FightMode = 0`, nudges the character into the safe zone, and halts active combat stances.

---

### User Story 4 — Cross-Node Cluster Redirection & Level-Gate Bouncing (Priority: P2)

When a player steps onto a portal whose destination map is hosted on an external world server node, or when the player's level is insufficient for the destination map, the server enforces cluster routing rules. For external maps, the server generates a cryptographically sealed single-use ticket and sends a cluster redirect packet. For under-leveled players, the server bounces the character back to safe coordinates on the source map and issues an informative system message.

**Why this priority**: Protects low-level players from wandering into deadly high-level zones and enables horizontal scaling across distributed server clusters.

**Independent Test**: Attempt to enter a level-20 dungeon portal with a level-5 character. Verify that the character is bounced back to their starting position and receives an advisory system message. Then step onto an external node portal with a qualified character, verifying receipt of `0x35` with valid handoff ticket GUID and plain little-endian port.

**Acceptance Scenarios**:
1. **Given** a character whose level is lower than the minimum required level for a portal, **When** the character steps onto the portal trap, **Then** the server cancels the transition, repositions the character back to a safe coordinate on the source map, and sends an advisory notification.
2. **Given** a qualified character stepping onto a portal leading to an external server node, **When** the portal activates, **Then** the server transmits `s2c_notifyplayerexchange` (`0x35`) containing a single-use handoff ticket `GUID`, the target server's IPv4 address, and the target port in plain Little-Endian format (`6666 = 0x1A0A`).

---

## 3. Edge Cases & Boundary Conditions

1. **Simultaneous Transition Requests**: A player clicks movement across a portal while simultaneously casting a skill or taking damage; the transition safely completes without leaving orphaned missile or combat state.
2. **Target Map Offline or Unloaded**: A player steps onto a portal whose target map is not active on any cluster node; the server keeps the player safely on the current map and issues an advisory error.
3. **Rapid Boundary Pacing**: A player steps onto a gate trap, turns around, and steps back within 2 frames; `LastTriggeredTrapId` handles rapid resets cleanly as long as non-trap cells are traversed between triggers.
4. **Handoff Ticket Expiry**: A client receives `0x35` redirect but fails to connect to the target server within the ticket validity window (60 seconds); the target server rejects the expired ticket cleanly.

---

## 4. Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: On server startup, the engine MUST parse map region cell trap data (`Region_S.dat` Section 1: 12-byte `KTrapFileHead` + 8-byte `KSPTrap` cell runs) and map trap scripts (`script/**/trap/*.lua`) indexed by 32-bit `g_FileName2Id` path hash.
- **FR-002**: During Phase 500 movement simulation ticks, the engine MUST check whether moving entities occupy a cell with a non-zero `TrapId`.
- **FR-003**: The engine MUST enforce edge-triggered anti-loop tracking on player sessions (`LastTriggeredTrapId`), executing trap logic only when `currentTrapId != LastTriggeredTrapId`, and resetting `LastTriggeredTrapId = 0` when stepping onto a cell with `TrapId == 0`.
- **FR-004**: When a gate trap (`PortalTrapActionKind.ToggleFightStateAndSetPos`) triggers, the engine MUST toggle `PlayerZoneState.FightMode` (`0 <-> 1`), nudge the character coordinates to `(TargetCellX * 32, TargetCellY * 32)`, and broadcast self-position sync (`0x4E`).
- **FR-005**: When a cross-map portal trap (`PortalTrapActionKind.ChangeMapPortal`) triggers and the player's level is less than `MinLevel`, the engine MUST bounce the player to `BounceBackCell`, sync coordinates via `0x4E`, and send a `0xFB` system notice.
- **FR-006**: When a cross-map portal triggers for an eligible player and the destination map is hosted locally, the engine MUST transfer `PlayerZoneState` in-memory via `IZoneClusterRouter.TryTransferPlayerAcrossMaps`, emitting `0x49` (`s2c_syncworld`), `0x4E` (`s2c_syncnpcminplayer`), and destination 3x3 AOI entity sync (`0x4C` / `0x4D`) without resetting the TCP cipher stream or sending `0x43`.
- **FR-007**: When the destination map is hosted on an external server node, the server MUST emit `s2c_notifyplayerexchange` (`0x35`, 25 bytes) carrying a 16-byte handoff `GUID`, target IPv4 address, and target port in plain little-endian (`6666 = 0x1A0A`, NOT `htons`).
- **FR-008**: Upon completing a map transition, the engine MUST update the character's durable world map ID and pixel coordinates so subsequent logins resume at the destination.

---

### Key Entities

- **ITrapScriptCatalog**: Read-only catalog mapping 32-bit trap identifiers to `PortalTrapBindingTemplate` destinations and rules.
- **PortalTrapBindingTemplate**: Definition capturing target map ID, target coordinates, required minimum level, fight mode action, and bounce-back coordinates.
- **IZoneClusterRouter**: Sharded router responsible for determining map locality, routing commands across workers, and transferring live player states between shards.
- **AttachTransferredPlayerZoneCommand**: Internal zone command attaching a transferred player session from another shard into the destination map's simulation loop.

---

## 5. Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of characters stepping onto configured portal traps transition to destination coordinates in under 500 milliseconds without connection interruption.
- **SC-002**: Zero duplicate trap executions occur while a character remains standing on an active portal cell run.
- **SC-003**: In-memory same-node transitions maintain 100% cipher continuity with zero dropped or corrupted packets.
- **SC-004**: 100% of under-leveled transition attempts are rejected, safely bouncing characters to valid walkable coordinates.
- **SC-005**: Cross-node redirect packets (`0x35`) encode the destination port in plain little-endian format with a valid cryptographic ticket.
