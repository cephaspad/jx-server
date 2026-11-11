# Feature Specification: Core Loot, Item Generation & Ground Objects

**Feature Branch**: `011-core-loot-and-item-generation`  
**Created**: 2026-11-11  
**Status**: Specification Complete  
**Input**: Single-responsibility vertical slice covering weighted monster drop tables, procedural 6-affix Blue equipment generation, ground object spatial lifecycle, killer ownership protection, and zero-duplication atomic pickup mechanics.

---

## 1. Overview & Context

Rewarding monster kills with currency, consumables, and randomized equipment is a foundational pillar of Võ Lâm Truyền Kỳ. Upon monster defeat, the server evaluates drop-rate rules, generates gold piles and procedural equipment, and instantiates ground objects on walkable terrain. Surrounding observers see the items appear in real time. To maintain fair gameplay, dropped items enforce an initial ownership protection window for the killer, expire after a duration if unclaimed, and support single-threaded atomic pickup that eliminates item duplication.

This specification isolates the **Core Loot, Item Generation and Ground Objects** vertical slice:
1. **Weighted Monster Loot Evaluation**: Determining drop counts, currency ranges, and item candidates from declarative drop tables.
2. **Procedural 6-Affix Blue Equipment Rolling**: Generating White and Blue gear with up to 3 explicit affixes and 3 Five-Elements hidden magic attributes based on item level, series, and player luck.
3. **Ground Object Spatial Lifecycle**: Spawning items and money piles on walkable map coordinates, broadcasting additions and removals, and tracking lifetime expiration on Phase 400.
4. **Killer Ownership Protection Window**: Enforcing exclusive pickup rights for the killer for a configurable initial period before opening to the public.
5. **Atomic Pickup & Race Condition Protection**: Processing pickup requests on the single-threaded zone loop to ensure that concurrent claims resolve to exactly one claimant with zero item duplication.

---

## 2. Clarifications & Ground-Truth Context

- **Equipment Generation Algorithm**: Item generation strictly follows the version 4 algorithm (`m_wVersion = 4`), rolling durability, base defense/damage ranges, and up to 6 magic attributes (3 explicit prefix/suffix slots and 3 hidden Five-Elements activation slots) based on equipment part weight, series, and item level.
- **Dual Identifiers**: Every non-stackable equipment item receives a persistent globally unique identifier in the domain layer, paired with an ephemeral runtime integer identifier for client wire synchronization.
- **Ground Object Placement**: Dropped objects are scattered around the defeated monster across valid walkable terrain cells, avoiding solid obstacles.
- **Ownership Timer**: Loot is protected for the killer (or killing party) for 180 simulation frames (~10 seconds) before becoming accessible to all players. Uncollected objects expire and despawn after 1,080 frames (~60 seconds).
- **Atomic Pickup Concurrency**: In high-density looting scenarios where multiple combatants attempt to pick up the same object on the same simulation frame, single-threaded command serialization guarantees exactly one success; the second attempt is safely rejected as a non-existent object.

---

## 3. User Scenarios & Acceptance Criteria

### User Story 1 - Monster Drop Evaluation & Ground Spawning (Priority: P1)
When a monster is defeated, the server evaluates the monster's assigned drop table. If the drop roll succeeds, the server rolls currency amounts and procedural items, instantiates corresponding ground objects on walkable cells surrounding the death position, and broadcasts the ground object appearance to all nearby players in the 3x3 region Area of Interest.

**Acceptance Scenarios**:
1. **Given** a defeated monster associated with a drop table, **When** loot evaluation resolves, **Then** the server rolls against drop chances and weights, generates the corresponding money amounts or equipment/consumable items, places them on nearby walkable cells, and broadcasts object addition notifications to the 3x3 region neighborhood.
2. **Given** a monster configured with a drop table having no successful rolls, **When** it dies, **Then** no ground objects are spawned.
3. **Given** a player entering a new region containing active ground objects, **When** region synchronization occurs, **Then** all active ground objects in the newly visible regions are synchronized to the player.

---

### User Story 2 - Procedural 6-Affix Blue Equipment Generation (Priority: P1)
When an equipment drop is generated, the item generator creates a piece of equipment matching the chosen genre, detail, and particular types. For Blue equipment, the generator rolls base durability, random seed, and up to 6 magic attributes (3 explicit affixes and 3 hidden Five-Elements affixes) selected according to part weights, series, item level, and player luck.

**Acceptance Scenarios**:
1. **Given** an equipment drop roll targeting Blue quality, **When** the item generator executes, **Then** it assigns a globally unique item identifier, rolls base defense or damage stats, and populates up to 3 explicit and 3 hidden Five-Elements magic attributes according to drop weights and series.
2. **Given** a character with high luck attribute defeating a creature, **When** Blue equipment rolls affixes, **Then** higher-tier magic affix candidates receive increased selection probability.
3. **Given** stackable items such as medicines or scrolls, **When** generated, **Then** they instantiate with configured stack counts without magic affixes.

---

### User Story 3 - Killer Ownership Protection & Despawn Expiration (Priority: P2)
Ground objects dropped from monster kills remain reserved exclusively for the killer during an initial protection window. After the protection timer elapses, any player may pick them up. If an object remains on the ground until its total lifetime expires, the server removes it and notifies nearby observers.

**Acceptance Scenarios**:
1. **Given** a ground object dropped by a player kill, **When** another player attempts pickup while the ownership protection timer is active, **Then** the server denies the pickup attempt and leaves the item on the ground.
2. **Given** a protected ground object whose protection window has elapsed, **When** any player attempts pickup, **Then** the server permits the pickup.
3. **Given** an uncollected ground object whose lifetime timer expires, **When** the ground object tick executes, **Then** the server removes the object from the region and broadcasts an object removal notification with expiration reason to nearby observers.

---

### User Story 4 - Atomic Item Pickup & Race Condition Resolution (Priority: P1)
A player within pickup range submits a pickup command for a ground object. For money, the server credits the player's gold balance, sends balance sync, and removes the ground object. For items, the server validates free inventory space, transfers the item into the player's bag, sends item sync, and removes the ground object. If two players attempt to pick up the same object concurrently, exactly one succeeds and zero duplication occurs.

**Acceptance Scenarios**:
1. **Given** a player standing within pickup range of a ground gold pile, **When** a pickup command is received, **Then** the server adds the gold to the player's inventory, sends money synchronization, and broadcasts ground object removal with pickup reason to the area of interest.
2. **Given** a player standing within pickup range of a ground item with sufficient free bag rectangle space, **When** a pickup command is received, **Then** the server transfers the item into the player's bag, emits the full item synchronization packet, and broadcasts ground object removal.
3. **Given** a player with an entirely full bag attempting to pick up an item, **When** the pickup command is received, **Then** the server rejects the pickup and leaves the ground object intact.
4. **Given** two players concurrently attempting to pick up the exact same ground object on the same simulation tick, **When** commands are processed, **Then** the first processed command claims the object, and the second command safely rejects without item duplication.

---

### User Story 5 - Throwing Away Inventory Items onto the Ground (Priority: P2)
A player discards unwanted items or medicines from their inventory. The server removes the items from inventory and creates corresponding ground objects at the player's feet, allowing nearby players to see and claim them.

**Acceptance Scenarios**:
1. **Given** a player holding an item on their cursor hand, **When** the player sends a throw-away command, **Then** the server removes the item from the player's inventory, creates a ground object at the player's coordinates, and broadcasts the ground object addition.
2. **Given** a player carrying medicines in their bag, **When** the player sends a discard-all-medicine command, **Then** all medicines are removed from the bag and spawned as ground objects around the player.

---

## 4. Edge Cases

- **Concurrent Pickup Race on Single Item**: Multiple players or automated farming bots send pickup requests for the same item pile on the same tick; single-threaded zone command dispatch guarantees only the first receives the item, while subsequent requests find the object already consumed.
- **Dropping Items on Impassable Coordinates**: If a player attempts to drop an item while standing on or adjacent to an impassable barrier, the server locates the nearest valid walkable cell to host the ground object.
- **Ground Object Pool Saturation**: High-density farming generates hundreds of ground objects in a single map area; ground objects use bounded region lists with automatic recycling of expired objects to prevent unbounded memory growth.
- **Player Disconnects Mid-Pickup**: If a player disconnects as a pickup command executes, the item is safely stored in the character's bag state and persisted to the control plane, ensuring the item is neither duplicated nor deleted.

---

## 5. Functional Requirements

- **FR-001**: The system MUST evaluate monster drop tables on defeat, generating weighted currency and item drop candidates.
- **FR-002**: The item generator MUST implement the authentic version 4 equipment generation algorithm, creating White and Blue equipment with base durability, base defense/damage stats, and up to 6 magic attributes (3 explicit prefix/suffix, 3 hidden Five-Elements).
- **FR-003**: Magic attribute selection MUST adhere to equipment part weights, element series compatibility, item level restrictions, and player luck factors.
- **FR-004**: Each generated non-stackable item MUST possess a globally unique identifier for persistence alongside an ephemeral runtime identifier for client wire communication.
- **FR-005**: Ground objects MUST be instantiated on walkable coordinates within the slain entity's spatial region.
- **FR-006**: Ground object additions and removals MUST be broadcast to all players observing the 3x3 region neighborhood.
- **FR-007**: Ground objects MUST enforce an initial killer ownership protection duration (default 180 frames) before allowing non-owner pickup.
- **FR-008**: Ground objects MUST automatically expire and despawn after their configured lifetime (default 1,080 frames).
- **FR-009**: The system MUST process gold pickups, crediting inventory currency, dispatching money sync, and removing the ground object.
- **FR-010**: The system MUST process item pickups, validating 2D footprint grid availability, moving the item to the bag, dispatching item sync, and removing the ground object.
- **FR-011**: Pickup command handling MUST be atomic and race-condition free, preventing item and currency duplication.
- **FR-012**: The system MUST support dropping items from the cursor hand and discarding all medicines onto the ground.

---

## 6. Measurable Success Criteria

- **SC-001**: 100% of generated 6-affix Blue equipment carry valid prefix/suffix and hidden Five-Elements magic attributes matching drop rules and series alignments.
- **SC-002**: In 10,000 simulated concurrent pickup races, exactly 1 claimant succeeds per ground object, achieving 0% duplicate items or currency.
- **SC-003**: 100% of ground objects enforce killer ownership protection and uncollected objects despawn cleanly upon expiration.
- **SC-004**: Ground object synchronization to players crossing region boundaries completes within 1 simulation tick.
- **SC-005**: 100% of items dropped by players appear accurately as ground objects on valid walkable cells.
