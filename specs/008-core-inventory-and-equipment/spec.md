# Feature Specification: Spatial Inventory & Equipment Grid

**Feature Branch**: `008-core-inventory-and-equipment`  
**Created**: 2026-11-11  
**Status**: Draft  
**Input**: Master Roadmap `specs-roadmap.md` (`008`), re-baselined from `specs/_archive_m1_m4/003-combat-missiles-loot-inventory/` and actual codebase.

---

## 1. Executive Summary & Value Proposition

An immersive martial arts RPG economy depends on tangible, physical item management. Weapons, armors, talismans, and elixirs possess varying physical dimensions, requiring players to organize their storage spatial bags strategically. Furthermore, the authentic Five-Elements philosophy governs equipment synergies, unlocking hidden magical powers when complementary elemental gear is worn together.

This specification defines the authoritative spatial container grids, 2D footprint collision matrix, 15-part equipment paper doll, Five-Elements hidden affix activation chain, stackable item splitting, medicine consumption, ground dropping, and cursor disconnect safety for JXServer. Player items occupy defined width-by-height cells inside a 6x10 main bag (`pos_equiproom`), a 3-slot quick-belt (`pos_immediacy`), cursor hand (`pos_hand`), or 15 dedicated equipment slots (`pos_equip`). The engine enforces strict zero-duplication guarantees, atomic move/swap confirmations, and automatic cursor return on disconnect.

---

## 2. User Scenarios & Acceptance Criteria *(mandatory)*

### User Story 1 — 6x10 Spatial Bag Grid Footprint Collision & Move/Swap (Priority: P1)

A player opens their inventory bag and reorganizes items of varying rectangular dimensions (such as 1x1 potions, 2x1 daggers, 2x3 robes, or 2x4 polearms). The server checks the 2D footprint collision against existing items, allows placement into empty cells or swapping with a matching-sized item, confirms the movement, and unlocks the client's inventory UI.

**Why this priority**: Spatial inventory grid management is the fundamental interaction for item storage, looting, and trade. Preventing grid collisions and overlaps guarantees inventory integrity.

**Independent Test**: Place a 2x3 armor into a 6x10 bag. Attempt to place a 2x4 spear overlapping the same cells; verify that the server rejects the move. Move the spear to an empty 2x4 area; verify success and receipt of move confirmation (`0x61`) and unlock (`0x8E`).

**Acceptance Scenarios**:
1. **Given** a 6x10 inventory bag grid, **When** a player moves an item into unoccupied cells within bounds, **Then** the server updates the item's `(GridX, GridY)` coordinates, emits move confirmation (`0x61`), and emits auto-move unlock (`0x8E`).
2. **Given** a player dragging an item onto cells already occupied by another item, **When** the target item can be legally swapped or picked up into `pos_hand`, **Then** the server swaps their container positions atomically.
3. **Given** an invalid move targeting cells outside the 6x10 boundary or colliding with an immovable item, **When** the server processes the command, **Then** the move is rejected, preserving original item positions and emitting unlock (`0x8E`).

---

### User Story 2 — 15-Part Equipment Management & Five-Elements Hidden Affix Chain (Priority: P1)

Players equip weapons, helmets, armor, rings, amulets, boots, belts, and accessories across 15 dedicated equipment slots. Equipping gear validates level, series, and stat requirements, recalculates character combat attributes, and evaluates the Five-Elements generation cycle (Kim sinh Thủy, Thủy sinh Mộc, Mộc sinh Hỏa, Hỏa sinh Thổ, Thổ sinh Kim) to activate hidden (dark) magical affixes on complementary gear.

**Why this priority**: The Five-Elements equipment activation ring is the signature build-crafting mechanic of JX1. Players combine series gear to unlock latent bonuses.

**Independent Test**: Equip a Metal series weapon with locked hidden affixes. Equip an Earth series helmet or ring that generates Metal; verify that the weapon's hidden affixes become active and apply their stat bonuses immediately.

**Acceptance Scenarios**:
1. **Given** an unequipped item in the bag meeting character requirements, **When** the player equips it into its designated equipment slot (`pos_equip`), **Then** the item moves to the paper doll slot, and the character's base stats, resistances, and damage are recalculated.
2. **Given** equipped items across adjacent slots, **When** an equipped item's series generates the series of another equipped item according to the Five-Elements cycle, **Then** the generated item's dark/hidden magical affixes (affixes 1, 3, 5) activate and apply to the character.
3. **Given** an equipped item currently activating a hidden affix on another item, **When** that activating item is unequipped, **Then** the dependent hidden affix is immediately deactivated and its bonuses are deducted.

---

### User Story 3 — Stackable Item Splitting & Currency Sync (Priority: P2)

Players can split stacks of consumable items (such as town portal scrolls, arrows, or potions) by specifying a split quantity, picking up the split portion into their cursor hand (`pos_hand`) to place elsewhere in their bag or drop on the ground. Player gold balances carried in the bag are synchronized whenever changed.

**Why this priority**: Splitting stacks enables trading partial quantities, stocking quick-belts, and sharing supplies among team members.

**Independent Test**: Select a stack of 20 town portal scrolls. Send split command for 5 scrolls; verify that the original stack decrements to 15, and a new stack of 5 scrolls appears in `pos_hand`.

**Acceptance Scenarios**:
1. **Given** a stackable item with quantity $N > 1$ in the bag, **When** the player divides quantity $k < N$, **Then** the source stack reduces to $N - k$, a new item instance with quantity $k$ is placed in `pos_hand`, and both are synchronized to the client.
2. **Given** an attempt to split a quantity greater than or equal to the stack size or less than 1, **When** processed, **Then** the server safely rejects the request.
3. **Given** an action that adds or spends gold, **When** the balance changes, **Then** the server dispatches `0x68` (`s2c_syncmoney`) with updated inventory and storage gold values.

---

### User Story 4 — Item Consumption: Medicines & Town Portal Teleport (Priority: P2)

Players consume health and mana potions from their bag or 3-slot quick-belt (`pos_immediacy`) to recover vitals over time, or use Town Portal scrolls (*Thổ Địa Phù*) to escape danger and teleport back to their bound revival village.

**Why this priority**: Consumables provide essential in-combat sustain and emergency evacuation back to safe town zones.

**Independent Test**: Consume a health potion from the belt. Verify that the potion stack decrements or is removed (`0x60`), and health regenerates tick-by-tick over the potion's duration. Use a Town Portal scroll; verify immediate teleportation to town.

**Acceptance Scenarios**:
1. **Given** a consumable medicine in the bag or belt, **When** the player sends an eat command (`0x59`), **Then** the item quantity decrements (or is removed if count is 1), and a periodic vital regeneration effect is scheduled on the character.
2. **Given** a player in a dangerous wilderness map using a Town Portal scroll, **When** the scroll is consumed, **Then** the item is removed and the server transitions the character to their bound town coordinates.

---

### User Story 5 — Ground Item Dropping & Cursor-Hand Disconnect Safety (Priority: P3)

Players can discard items from their cursor hand (`pos_hand`) onto the ground, spawning a public ground loot object. If a player abruptly disconnects or experiences a crash while holding an item on their cursor, the server guarantees zero item loss by returning the held item into the first available space in `pos_equiproom` before saving character state.

**Why this priority**: Disconnecting while dragging an item is a notorious vector for accidental item loss or duplication in legacy games. Preserving held items protects player wealth.

**Independent Test**: Pick up a valuable weapon into `pos_hand`. Terminate the client socket abruptly. Log back into the character; verify that the weapon is safely present inside the 6x10 bag without loss or corruption.

**Acceptance Scenarios**:
1. **Given** an item held in `pos_hand`, **When** the player sends a throw-away command (`0x5E`), **Then** the item is removed from the inventory and instantiated as a ground object on the map.
2. **Given** a player with an item in `pos_hand`, **When** the session disconnects unexpectedly, **Then** the server automatically returns the item into the first available space in `pos_equiproom` (or storage) prior to persisting character state.

---

## 3. Edge Cases & Boundary Conditions

1. **Bag Full on Cursor-Hand Return**: If `pos_equiproom` has no free cells when a disconnected player's cursor item is returned, the item is placed into temporary overflow storage to prevent data loss.
2. **Rapid Potion Quaffing**: A player spams potion consumption commands faster than the animation rate; the server queues or stacks regeneration duration without multiplying instant healing illegally.
3. **Concurrent Move and Drop**: A player attempts to drop an item while simultaneously moving it; the single-threaded shard serializes commands, preventing duplicate item creation.
4. **Item Swap with Unequal Dimensions**: A player attempts to swap a 1x1 ring with a 2x3 armor located in the bag; the swap requires sufficient surrounding free cells to accommodate the differing footprints.

---

## 4. Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The inventory system MUST maintain a 6x10 2D spatial grid for `pos_equiproom` (60 cells), verifying `width x height` cell availability and boundaries for all item placements.
- **FR-002**: The engine MUST process item move commands (`c2s_playermoveitem` `0x5B`), validating source and destination placements across `pos_hand` (`1`), `pos_equip` (`2`), `pos_equiproom` (`3`), and `pos_immediacy` (`7`), confirming via `0x61` and unlocking via `0x8E`.
- **FR-003**: The engine MUST maintain 15 dedicated equipment slots (`pos_equip`), validating item equip eligibility (character level, gender, series, and faction requirements).
- **FR-004**: The engine MUST evaluate the Five-Elements generation cycle (Metal $\rightarrow$ Water $\rightarrow$ Wood $\rightarrow$ Fire $\rightarrow$ Earth $\rightarrow$ Metal) across equipped gear, activating dark/hidden magical affixes (affixes 1, 3, 5) when activated by a generating series item.
- **FR-005**: The engine MUST process stack splitting (`c2s_playerdivideitem` `0x85`), creating a new item instance with the split count in `pos_hand` and decrementing the original stack.
- **FR-006**: The engine MUST process medicine consumption (`c2s_playereatitem` `0x59`), applying gradual health/mana regeneration ticks and removing or decrementing the item.
- **FR-007**: The engine MUST process item discarding (`c2s_playerthrowawayitem` `0x5E` and `c2s_throwawayallmedicine` `0x83`), removing items from inventory and spawning ground loot objects.
- **FR-008**: The engine MUST enforce cursor-hand disconnect safety: whenever a player session terminates with an item held in `pos_hand`, the engine MUST return the item to `pos_equiproom` prior to saving.
- **FR-009**: The server MUST synchronize item instances via `0x5F` (`s2c_syncitem`, 80B) and gold balances via `0x68` (`s2c_syncmoney`, 13B).
- **FR-010**: Durable item persistence MUST record `IsBroken` (zero-durability indicator) and `EnchaseLevel` (`0..10`) on `CharacterItem` entities.

---

### Key Entities

- **PlayerInventoryGrid**: 2D spatial container matrix managing 6x10 bag cells, 15 equipment slots, 3 belt slots, and cursor hand state.
- **PlayerItemSlot**: In-memory representation of an owned item in the zone engine, holding runtime ID, template reference, durability, container place, grid coordinates, and rolled magical affixes.
- **InventoryLootCommandHandler**: Modular command handler processing item move, divide, drop, eat, and pickup zone commands.
- **CharacterItem**: Domain persistence entity storing owned items with durability, broken status, enchase level, and 6-slot affix parameters.

---

## 5. Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of invalid spatial grid placements (out-of-bounds or overlapping cells) are rejected with zero item corruption or ghost items.
- **SC-002**: Equipping complementary series gear activates Five-Elements hidden affixes with 100% mathematical accuracy matching legacy formulas.
- **SC-003**: 100% of items held on the cursor during unexpected client disconnect are returned safely to the player's bag with zero item loss.
- **SC-004**: Item move confirmations (`0x61`) and UI unlocks (`0x8E`) are delivered to the client in under 50 milliseconds.
