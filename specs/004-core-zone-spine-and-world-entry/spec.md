# Feature Specification: Core Zone Modular Spine, World Entry & Simulation Harness

**Feature Directory**: `specs/004-core-zone-spine-and-world-entry`  
**Status**: Approved  
**Applies To**: Sharded 18-Hz Zone Spine, World Entry Handshake (`0x42` $\rightarrow$ `0x43`), Heartbeat Synchronization, Domain Outbound Channels, Foundational Persistence, and Deterministic Simulation Harness  

---

## 1. Executive Summary & Value Proposition

In the legacy server architecture, game world simulation was bound to monolithic loop logic where command dispatching, state updates, network broadcasting, and persistence were intermingled in a single unyielding thread. Adding new gameplay mechanics required modifying core zone tick loops, risking whole-server crashes upon encountering unexpected exceptions. Furthermore, testing real-time world mechanics (such as multi-second cooldowns, status effect expirations, and monster respawns) required waiting on real-world wall clocks over live network sockets, severely slowing automated validation.

The **Core Zone Modular Spine, World Entry & Simulation Harness** specification delivers an extensible, deterministic foundation for all in-world simulation:
1. **Open/Closed Modular Zone Spine**: A single-threaded 18-Hz simulation loop allowing independent gameplay modules to register typed command handlers, phased tick subsystems, and per-player feature state slots without editing the core zone loop, backed by per-session fault containment.
2. **Deterministic High-Speed Simulation Harness**: An in-memory harness with controllable virtual time and inspectable outbound player events capable of stepping `1,000` simulation frames (~`55.5` seconds of virtual game time) in under `5` milliseconds with zero network or database dependencies.
3. **Domain-Segregated Outbound Channels**: Segregated notification contracts for world presence, combat, inventory, and chat, providing clean boundaries for downstream module expansion.
4. **Foundational Character & Item Persistence**: Durable persistence for quest progress variables (`TaskValues` `0..65535`), repository protection credential digests, item broken-durability status, and item crafting enhancement progress.
5. **Authentic World Entry & Heartbeats**: The complete world entry handshake sequence (`0x42` logic login $\rightarrow$ `0x49` world map $\rightarrow$ `0x44` player attributes $\rightarrow$ `0x45` skill list $\rightarrow$ `0x46` vitals $\rightarrow$ `0x4C` appearance $\rightarrow$ `0xA4` flags $\rightarrow$ `0x43` sync completion) and continuous 18-Hz heartbeat pings (`0x82`/`0x70`/`0x99`).

---

## 2. User Stories & Acceptance Scenarios

### User Story 1 - Open/Closed Modular Extension Spine & Fault Isolation (Priority: P1)

As a feature engineer developing gameplay modules, I want to plug custom zone command handlers, ordered per-tick simulation subsystems, and isolated per-player feature state slots into a map zone without modifying the core 18-Hz zone loop, so that new modules can be developed and verified in isolation while unexpected faults in one player's session never crash the zone or corrupt other players' states.

**Why this priority**: Decoupling feature extensions from the core 18-Hz zone loop eliminates merge bottlenecks and ensures whole-server stability across all parallel gameplay modules.

**Independent Test**: Can be validated by registering an independent module into an active map zone, dispatching its commands, verifying per-frame subsystem execution, injecting an unhandled exception into one player's session, and confirming that the fault is isolated to that session while all other players continue simulation normally.

**Acceptance Scenarios**:
1. **Given** an initialized map zone with registered core capabilities plus a custom gameplay module, **When** a player submits the module's command, **Then** the zone routes the command to the module's handler and executes its tick subsystem in its designated phase without modifying core zone loop logic.
2. **Given** multiple players active in a map zone, **When** a plugged-in command handler or tick step encounters an unhandled runtime exception for one player session, **Then** the zone isolates the fault, records diagnostic telemetry, and completes the frame for all other players.
3. **Given** a player with active module feature state who disconnects or transfers maps, **When** the departure occurs, **Then** the zone automatically invokes cleanup on all attached feature slots, leaving zero orphaned state in memory.

---

### User Story 2 - Deterministic High-Speed Simulation Test Harness (Priority: P1)

As a quality engineer or core developer, I want a self-contained in-memory zone simulation harness with controllable virtual time, frame-accurate command injection, and an outbound event recorder, so that I can step `1,000` simulation frames (~`55.5` seconds of virtual time) in under `5` milliseconds to deterministically verify multi-second cooldowns, status ailments, and entity lifecycles without network sockets or wall-clock delays.

**Why this priority**: Deterministic virtual time stepping provides an immediate, massive acceleration for regression testing and continuous integration without flaky wall-clock sleeps.

**Independent Test**: Can be tested by launching an in-memory map zone in the harness, scheduling commands at future frame indices, advancing `1,000` frames, asserting that total execution finishes in under 5 milliseconds, and confirming that identical initial seeds produce 100% identical outbound event sequences.

**Acceptance Scenarios**:
1. **Given** an in-memory map zone initialized in the simulation harness with zero live network sockets, **When** advancing the zone by `1,000` consecutive simulation frames, **Then** the entire batch completes in under `5` milliseconds with zero wall-clock waiting.
2. **Given** time-dependent status effects, monster respawns, and loot timers scheduled in the harness, **When** virtual frames are advanced, **Then** every mechanic triggers on its exact designated frame boundary regardless of host execution speed.
3. **Given** two separate runs of the simulation harness initialized with identical map data, seeds, and command schedules, **When** stepped forward, **Then** both runs produce identical final entity states and identical outbound event logs.

---

### User Story 3 - Authentic World Entry & Continuous Heartbeat Synchronization (Priority: P1)

As a player or autonomous bot presenting a valid world handoff ticket, I want to connect to the game server, complete the initial world synchronization burst, and maintain continuous heartbeat exchanges, so that my character stands at the correct map coordinates with full attributes and skills and remains stably online.

**Why this priority**: World entry is the foundational bridge connecting control-plane authentication to real-time zone simulation.

**Independent Test**: Can be tested by presenting a valid 16-byte handoff ticket via `0x42`, verifying that the server responds with the complete sync burst (`0x49`, `0x44`, `0x45`, `0x46`, `0x4C`, `0xA4`, `0x43`), completing the `0x43` client handshake, and verifying that periodic server pings (`0x82`) and client replies (`0x70` $\rightarrow$ `0x99`) maintain the session.

**Acceptance Scenarios**:
1. **Given** a client presenting a valid, unexpired handoff ticket (`0x42`), **When** verified, **Then** the world server attaches the character to the target map and transmits the full world synchronization burst in strict sequential order.
2. **Given** an in-flight synchronization sequence, **When** the client returns the sync completion confirmation (`0x43`), **Then** the server activates the character in the 18-Hz simulation loop and begins regular heartbeat exchanges.
3. **Given** an active world session, **When** the client replies to server ping requests (`0x82` $\rightarrow$ `0x70`), **Then** the server acknowledges (`0x99`) and maintains session liveness; if client heartbeats cease beyond the timeout window, the server evicts and persists the character.

---

### User Story 4 - Domain-Segregated Outbound Channels & Foundational Persistence (Priority: P2)

As an architect or modular feature team, I want server-to-player notifications partitioned into cohesive domain channels (world visibility, combat, inventory, chat) and durable storage for foundational character and item attributes (quest task variables, repository security credentials, item broken-durability status, item crafting progress), so that downstream modules can emit events and persist data without collisions or storage migrations.

**Why this priority**: Establishing cohesive domain notification sinks and completing the foundational persistence baseline upfront prevents breaking storage changes once parallel feature development begins.

**Independent Test**: Can be validated by mutating task variables (`0..65535`), setting repository credential digests, marking items as broken at zero durability, and advancing crafting levels (`0..10`), asserting that all attributes persist losslessly across logout and re-login under lease fencing.

**Acceptance Scenarios**:
1. **Given** zone subsystems dispatching player updates, **When** emitted, **Then** each subsystem dispatches exclusively through its designated domain channel while clients receive outbound messages in FIFO order.
2. **Given** a character updating quest task progress across identifiers `0` through `65,535`, **When** persisted and reloaded, **Then** non-zero task variables reload with exact signed integer values while zero-valued entries are pruned to keep storage compact.
3. **Given** character items whose durability hits zero (entering broken status) or whose crafting enhancement level advances (`0..10`), **When** persisted, **Then** both attributes reload identically with backward-compatible defaults for legacy records.

---

## 3. Edge Cases

- **Throwing Tick Subsystem or Command Handler**: A plugged-in module throws an unhandled exception during its tick execution; the zone catches the exception, logs a structured diagnostic, and continues executing subsequent phases and players without stalling the 18-Hz loop.
- **Stale Handoff Ticket or Superseded Lease Epoch**: A delayed client attempts to enter the world with a ticket whose lease epoch has been incremented by a newer login; the world server rejects the ticket immediately, preventing duplicate character instances.
- **Batched Frame Stepping in Harness with Expiring Timers**: Stepping 1,000 frames in a single batch vs. stepping 1 frame 1,000 times produces bit-for-bit identical final world state and event logs.
- **Missing Optional Outbound Domain Sink**: A module emits an event on an optional domain channel not subscribed by a lightweight bot session; the session cleanly ignores the event without throwing errors.
- **Boundary Task Identifiers and Zero Resets**: Task values written to task ID 0 or 65,535 and subsequently reset to 0 are pruned from storage while consistently returning 0 upon query.

---

## 4. Functional Requirements

- **FR-001**: Each map zone MUST support modular command handler registration and deterministic routing so independent gameplay modules can handle zone commands without editing core loop logic.
- **FR-002**: Each map zone MUST execute an ordered 18-Hz simulation pipeline allowing modules to register per-frame subsystems at deterministic execution phases (StatusEffects=100, MonsterAi=200, Missiles=300, GroundObjects=400, MovementAndPosture=500, SessionHeartbeats=600).
- **FR-003**: Zone initialization MUST reject conflicting command handlers, duplicate subsystem names, and duplicate feature slot keys with clear diagnostic errors.
- **FR-004**: Each active player presence in a map zone MUST support typed, array-indexed per-player feature state slots with automatic cleanup guaranteed whenever a player leaves the zone, transfers maps, or disconnects.
- **FR-005**: Each map zone MUST enforce per-session fault isolation across command handlers and per-player simulation steps so an unexpected exception in one session never halts the zone loop or corrupts state for other players.
- **FR-006**: The system MUST provide an in-memory zone simulation test harness capable of instantiating map zones, placing synthetic entities, scheduling commands at exact frames, and stepping single or batched simulation frames with zero network or database dependencies.
- **FR-007**: Stepping `1,000` consecutive simulation frames in an initialized map zone within the simulation harness MUST complete in under `5` milliseconds of host execution time.
- **FR-008**: The simulation harness MUST record all outbound notifications per simulated player in exact emission sequence, capturing frame numbers and domain tags, and allow inspecting, filtering, and clearing recorded events.
- **FR-009**: Outbound player notifications MUST be partitioned into cohesive domain channels: (1) World visibility and entry, (2) Combat and vitals, (3) Inventory and ground objects, and (4) Chat and messaging, sharing a single FIFO socket channel.
- **FR-010**: Character persistence records MUST durably store a sparse collection of quest task progress variables (`0..65535`) mapping to signed integer values, returning `0` for unassigned keys and omitting zero-valued entries from persistent storage.
- **FR-011**: Character persistence records MUST durably store optional repository security credentials strictly as a 32-character uppercase MD5 cryptographic digest, defaulting to null/unlocked.
- **FR-012**: Character item persistence records MUST durably store explicit broken-durability status flags (defaulting to intact) and crafting enhancement progress levels (`0..10`, defaulting to 0).
- **FR-013**: The world server MUST execute the authentic world entry sequence upon receiving a valid handoff ticket (`0x42` $\rightarrow$ `0x49`, `0x44`, `0x45`, `0x46`, `0x4C`, `0xA4`, `0x43`) and transition the character to active in-world status upon receiving `0x43` confirmation.
- **FR-014**: The world server MUST maintain connection liveness via periodic 5-second server ping requests (`0x82`), client replies (`0x70`), and server ping acknowledgments (`0x99`), evicting and persisting sessions that exceed the 15-second heartbeat timeout.

---

## 5. Key Entities

- **MapZoneWorker**: The single-threaded 18-Hz simulation shard orchestrating command routing, phased tick systems, and player state management for a specific subworld map.
- **ZoneExtensionRegistry**: An immutable registry storing compiled command routes (`FrozenDictionary<Type, ZoneCommandRoute>`), ordered tick systems, and player feature slot keys.
- **PlayerZoneState**: The authoritative in-memory representation of an active player inside a map zone, encapsulating position, vitals, attributes, outbound sinks, and attached feature slots.
- **PlayerFeatureSlotKey**: A typed, uniquely named key providing $O(1)$ indexed access to feature state attached to a player.
- **ZoneSimulationHarness**: An in-memory harness wrapping a map zone with controllable virtual time (`FakeTimeProvider`), deterministic random seeds, and outbound event recorders.
- **Domain Outbound Sinks**: Cohesive notification interfaces (`IWorldOutboundSink`, `ICombatOutboundSink`, `IInventoryOutboundSink`, `IChatOutboundSink`) implemented by client sessions.

---

## 6. Success Criteria

- **SC-001**: An independent gameplay extension module can be registered and verified end-to-end in a map zone with `0` modifications to the core zone loop, command router, or core player record.
- **SC-002**: An injected fault in a single player's modular command handler or feature step leaves 100% of other players in the same map zone completely unaffected.
- **SC-003**: Stepping `1,000` consecutive simulation frames in the simulation harness finishes in under `5` milliseconds with zero live sockets, zero wall-clock delays, and zero external database queries.
- **SC-004**: Running any multi-frame simulation scenario twice from identical initial state and random seed yields 100% identical final world states and identical outbound event logs.
- **SC-005**: Reference game clients (`game.exe`) and bot swarms (`jx-swarm`) complete world entry into Ba Lăng Huyện (Map 53) and maintain stable 18-Hz heartbeat sessions with zero framing errors.
- **SC-006**: 100% of modifications to task variables (`0..65535`), repository password hashes, item broken flags, and item crafting levels persist and reload losslessly across map transfers and logout cycles under lease fencing.
