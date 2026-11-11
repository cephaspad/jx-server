# Feature Specification: Spatial 3x3 Region AOI Visibility, Entity Synchronization & Static NPC/Monster Spawns

**Feature Branch**: `005-core-spatial-aoi-and-spawns`  
**Created**: 2026-11-11  
**Status**: Draft  
**Input**: Master Roadmap `specs-roadmap.md` (`005`), re-baselined from `specs/_archive_m1_m4/002-spatial-aoi-movement-chat/` and actual codebase.

---

## 1. Executive Summary & Value Proposition

In an authentic massive multiplayer virtual world, players must see and interact with nearby characters, town service personnel, and monsters without overwhelming network bandwidth or client rendering budgets. 

This specification defines the authoritative spatial partitioning, Area-of-Interest (AOI) visibility management, entity presence synchronization, on-demand detail querying, and static world population for JXServer. Each active map zone is partitioned into a uniform 2D grid of spatial regions. As players explore the world, cross region boundaries, or disconnect, the engine dynamically calculates the entering and leaving differences across a surrounding 3x3 region neighborhood. Surrounding players receive full appearance updates upon discovery, periodic compact position broadcasts during presence, and immediate removal upon departure. Furthermore, upon map initialization, all static town NPCs and field monsters defined in map region assets are populated at their exact coordinates with authentic names, levels, and camps.

---

## 2. User Scenarios & Acceptance Criteria *(mandatory)*

### User Story 1 — Spatial 3x3 Region Neighborhood Visibility & Boundary Synchronization (Priority: P1)

When a player enters a map or travels through the world, they must automatically discover and maintain awareness of all other players and non-player characters (NPCs) situated within their immediate 3x3 region neighborhood. When an entity crosses a region boundary, players entering that entity's 3x3 neighborhood immediately receive full entity appearance and posture state, while players who are no longer in the 3x3 neighborhood stop receiving position updates for that entity.

**Why this priority**: Spatial partitioning is the absolute foundational gate for all subsequent multiplayer interactions (movement broadcasting, combat, targeting, local chat, and trade). Without deterministic AOI visibility, entities cannot be seen or targeted.

**Independent Test**: Connect two clients to the same map at coordinates within the same 3x3 region neighborhood. Verify that both clients immediately see each other's full appearance. Have one character move across a region boundary away into a non-adjacent region; verify that the observer receives departure/stops receiving position broadcasts, and upon returning, visibility is seamlessly restored.

**Acceptance Scenarios**:
1. **Given** an active map containing static NPCs and at least one online player, **When** a new player enters the world within the same 3x3 region neighborhood, **Then** both players receive full appearance and coordinate synchronization for each other, and the entering player receives full appearance synchronization for all static NPCs within their 3x3 neighborhood.
2. **Given** two players standing within the same 3x3 neighborhood, **When** one player moves across a region boundary into an adjacent region that remains within the 3x3 neighborhood, **Then** periodic compact position updates continue uninterrupted without duplicate full sync broadcasts.
3. **Given** two players standing within the same 3x3 neighborhood, **When** one player moves into a region outside the observer's 3x3 neighborhood or logs out, **Then** the observer stops receiving position updates for that player, and the moving player receives full appearance data for newly discovered entities in the newly entered regions.

---

### User Story 2 — On-Demand Entity Detail Resolution (Priority: P1)

When a game client or autonomous bot observes a compact entity update or targets an entity whose full visual or character profile is not yet cached locally, the client can submit an on-demand detail query specifying the runtime entity identifier. The server verifies that the entity exists on the active map and immediately responds with the complete entity appearance, name, camp, series, level, and current posture.

**Why this priority**: To minimize network overhead, periodic spatial updates convey compact coordinates and vitals. Clients require an authoritative, low-latency detail resolution mechanism to inspect unfamiliar entities, display target nameplates, and render equipment/series visual effects.

**Independent Test**: Connect a client to an active map. Dispatch an explicit entity query for a nearby static NPC or player identifier. Verify that the server returns the complete entity snapshot with exact name, camp, level, and series within 1 simulation tick.

**Acceptance Scenarios**:
1. **Given** an active entity (player or NPC) on the current map, **When** a client sends an on-demand entity detail request for that entity's identifier, **Then** the server responds with a full entity synchronization packet containing the entity's name, coordinates, level, camp, series, posture, and appearance metadata.
2. **Given** a client requesting details for an entity identifier that does not exist or has already despawned, **When** the server processes the query, **Then** the server safely ignores the invalid request without disconnecting the client or throwing exceptions.

---

### User Story 3 — Static Town NPC & Field Monster Spawning from Map Assets (Priority: P2)

When a map zone initializes, the engine reads the map's region placement records and the static NPC template catalog, instantiating all configured town dialog NPCs (such as Xa Phu, Chưởng Quầy, Hiệu Thuốc) and field monsters into their designated spatial regions with exact coordinates, authentic localized names, levels, camps, and five-elements series.

**Why this priority**: A game map without authentic service NPCs and monsters is lifeless and non-functional. Spawning must happen deterministically at startup so that initial world entry and exploration find all world actors in place.

**Independent Test**: Start the server with Ba Lăng Huyện (Map 53). Query all spawned entities in the map's spatial grid. Verify that all static service NPCs and monsters defined in the map assets are present at their exact coordinates with correct localized display names and camps.

**Acceptance Scenarios**:
1. **Given** static NPC template definitions and map region placement records, **When** the map zone initializes, **Then** every configured static NPC and monster is instantiated as an active zone entity with its template attributes, level, camp, series, and pixel coordinates, and registered into its corresponding spatial region slot.
2. **Given** a map zone where town NPCs have localized display names defined in configuration or script overrides, **When** the NPCs are spawned, **Then** the engine resolves the localized name in preference to legacy raw encoding strings, ensuring proper name display on connected clients.

---

## 3. Edge Cases & Boundary Conditions

1. **Four-Corner Region Boundaries**: An entity standing within a few pixels of the intersection of 4 regions oscillates back and forth. The spatial grid must handle region transitions deterministically without duplicate registrations or leaking references.
2. **High-Density Player Clustering**: Over 50 players congregate in a single region or across adjacent town regions. The spatial grid membership lookups and AOI set-difference calculations must execute in under 15 milliseconds without memory allocations.
3. **Entity Detachment / Disconnection**: When a player abruptly disconnects, their presence must be immediately purged from their host spatial region slot so that neighboring players do not observe ghost entities.
4. **Out-of-Bounds Entities**: An entity placed or moving outside the defined bounding rectangle of the map's region grid must be safely clamped or rejected rather than causing out-of-range indexing.
5. **Simultaneous Region Transitions**: Two players in adjacent regions cross into each other's regions on the same tick. Both must receive appropriate entering/leaving updates without deadlocks or race conditions.

---

## 4. Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The map zone MUST partition its navigable space into a 2D uniform grid of spatial regions, where each region spans exactly 16 x 32 cells (512 x 1024 world pixels), bounded by the map's coordinate limits.
- **FR-002**: The spatial grid MUST maintain active membership lists for players and NPCs per region slot, updating membership whenever an entity's coordinates cross from one region to another.
- **FR-003**: The engine MUST calculate the Area-of-Interest (AOI) as the 3x3 block of regions centered on the entity's current region (up to 9 regions total).
- **FR-004**: Whenever a player enters the map or moves into a new region, the engine MUST calculate the set difference between the new 3x3 region set and the old 3x3 region set (`New3x3 \ Old3x3`), dispatching full entity appearance synchronization for all newly discovered entities to the moving player, and broadcasting the moving player's full appearance to observers residing in those newly entered regions.
- **FR-005**: For entities remaining within an observer's 3x3 neighborhood across ticks, the engine MUST support periodic compact position broadcasts conveying world coordinates, posture, camp, facing direction, and vital percentage.
- **FR-006**: The engine MUST provide authoritative self-position synchronization to the client, conveying current sub-cell pixel offsets, cell coordinates within the region, posture, and facing direction.
- **FR-007**: The system MUST process client on-demand entity detail queries for both player and NPC entities, verifying entity presence on the active map and returning full entity appearance and attribute synchronization.
- **FR-008**: On map zone startup, the engine MUST load static NPC placements from map region data and template catalogs, instantiating static NPCs and monsters with their template ID, level, camp, five-elements series, movement speed, and localized display names.
- **FR-009**: When an entity departs a map, logs out, or moves out of an observer's 3x3 neighborhood, the engine MUST ensure observers cease receiving position updates for that entity and purge the entity from spatial membership.

---

### Key Entities

- **SpatialRegionSlot**: Represents a single 512 x 1024 pixel partition of a map zone, maintaining dynamic membership collections of active players and NPCs located within its bounds.
- **SpatialRegionGrid**: The 2D grid structure managing all spatial region slots for a map zone, providing coordinate-to-region indexing, 3x3 neighborhood retrieval, and entering/leaving set-difference computations.
- **NpcZoneState**: An active non-player character or monster instance living inside a map zone, holding runtime identifier, template ID, level, camp, five-elements series, coordinates, posture, vitals, and facing direction.
- **NpcTemplate**: A static read-only archetype loaded from configuration, defining base attributes, entity kind (Dialog NPC, Monster), default camp, series, movement speeds, and visual model parameters.
- **EntityFullSyncSnapshot**: A unified, immutable value structure capturing the full appearance, name, position, vitals, camp, series, posture, and template metadata of an entity for wire transmission.

---

## 5. Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 50 concurrent entities moving and crossing region boundaries in a map zone maintain an 18-Hz simulation loop with 99th-percentile tick duration under 15 milliseconds.
- **SC-002**: 100% of static NPCs and monsters defined in loaded map region assets spawn at their exact configured coordinates upon map startup with authentic localized names and camps.
- **SC-003**: 100% of valid on-demand entity detail queries are resolved and responded to within 1 simulation frame (55.5ms) of receipt.
- **SC-004**: When an entity crosses a region boundary, 100% of observers in newly visible regions receive full entity synchronization without dropped packets or phantom duplicates.
- **SC-005**: Purging a departing or disconnecting entity completely clears its presence from the spatial region grid within the same simulation tick, leaving zero orphaned references.
