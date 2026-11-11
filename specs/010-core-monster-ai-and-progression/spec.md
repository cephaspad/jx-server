# Feature Specification: Core Monster AI, Death, Experience Progression & Revival

**Feature Branch**: `010-core-monster-ai-and-progression`  
**Created**: 2026-11-11  
**Status**: Specification Complete  
**Input**: Single-responsibility vertical slice covering the deterministic 18-Hz monster artificial intelligence state machine, monster death and killer attribution, experience progression, level-up milestones, attribute/skill point distribution, and player death/revival workflows.

---

## 1. Overview & Context

A living virtual world requires autonomous creatures that react to their environment and challenge players. In Võ Lâm Truyền Kỳ, field creatures autonomously patrol designated territories, engage threats, pursue retreating foes, and return to their origin when pulled too far. Defeating these monsters yields experience points that propel character progression, granting unallocated attribute and skill points to shape personal combat builds. Conversely, when a player falls in battle, they must safely revive at designated sanctuary revival points.

This specification isolates the **Core Monster AI and Progression** vertical slice:
1. **18-Hz Monster AI State Machine**: Deterministic transitions across Idle/Patrol, Aggro/Pursuit, Attack, Leash/Return, and Dead/RespawnWait.
2. **Entity Death & Killer Attribution**: Broadcast of death state upon reaching zero health, attribution of kill credit to the responsible player, and clearance from active spatial views.
3. **Experience Accrual & Level-Up Progression**: Tracking cumulative experience, crossing level-up thresholds, awarding attribute and skill points, and updating base vitals.
4. **Attribute & Skill Point Allocation**: Interactive distribution of unallocated attribute points into base stats (Strength, Dexterity, Vitality, Energy) and skill points into learned martial techniques.
5. **Player Death & Town Sanctuary Revival**: Managing player defeat, clearing hostile monster aggro, switching to peaceful mode, and teleporting to saved town revival coordinates.

---

## 2. Clarifications & Ground-Truth Context

- **AI State Frequency**: Monster decisions (target evaluation, path stepping, attack triggering, leash checks) execute synchronously on Phase 200 of the 18-Hz zone loop (~55.5ms per tick), ensuring completely deterministic behavior.
- **Aggro Evaluation**: Monsters evaluate hostility based on camp relations and proximity (within their configured perception radius). Taking damage from a player immediately places that player at the top of the monster's aggro focus.
- **Leash Distance Boundary**: If a target pulls a monster beyond its maximum pursuit distance from its spawn anchor, the monster disengages, becomes temporarily invulnerable, and returns directly to its spawn point.
- **Level-Up Allocation Rates**: Each level-up grants exactly 5 unallocated base attribute points and 1 unallocated skill point, while base maximum health, mana, and stamina expand according to class-specific series growth rates.
- **Revival Destination**: Player revival defaults to the character's bound town revival point (e.g., Ba Lăng Huyện town center), resetting combat posture to peaceful (`FightMode = 0`) to prevent spawn-camping.

---

## 3. User Scenarios & Acceptance Criteria

### User Story 1 - Monster AI Lifecycle & Pursuit/Leash Behavior (Priority: P1)
A monster spawned in the wild periodically patrols within a wandering radius of its spawn origin. When an enemy character enters its aggro detection radius or attacks it, the monster switches to pursuit mode, closing the distance to execute attacks. If the target runs beyond the monster's maximum leash radius or enters a peaceful sanctuary, the monster abandons pursuit and walks back to its home coordinates.

**Acceptance Scenarios**:
1. **Given** a living monster idling near its spawn coordinates, **When** simulation ticks elapse without nearby hostiles, **Then** the monster periodically picks random patrol points within its patrol radius and walks toward them.
2. **Given** an idling or patrolling monster, **When** a player in combat mode enters its aggro radius or damages it, **Then** the monster transitions to pursuit state, sets that player as its target, and moves toward the player until within attack range.
3. **Given** a pursuing monster within attack range of its target, **When** its attack cooldown has elapsed, **Then** the monster triggers an attack action targeting the player.
4. **Given** a pursuing monster whose target exceeds the maximum leash distance from the spawn anchor, or whose target dies or enters a safe zone, **When** the leash check evaluates, **Then** the monster clears aggro, enters leash return state, and walks directly back to its home position.

---

### User Story 2 - Monster Death, Killer Attribution & Scheduled Respawn (Priority: P1)
When a monster's health reaches zero, the server broadcasts its death animation to all surrounding observers, clears its spatial presence from active combat targeting, awards experience to the killer, and starts a respawn countdown timer. Once the respawn delay elapses, the monster reappears at its original spawn coordinates at full health.

**Acceptance Scenarios**:
1. **Given** a monster whose health drops to zero from a player attack, **When** the fatal blow is processed, **Then** the monster transitions to dead state, broadcasts the death notification to the 3x3 region neighborhood, and awards experience to the responsible player.
2. **Given** a dead monster whose respawn countdown timer has elapsed, **When** the zone tick executes, **Then** the monster respawns at its home spawn coordinates with full health, resetting its AI state to idle patrol.

---

### User Story 3 - Character Experience & Level-Up Progression (Priority: P1)
When a character earns experience from defeating creatures, the server credits the experience. If cumulative experience reaches or exceeds the threshold for the next level, the character advances in level, gains 5 unallocated attribute points and 1 unallocated skill point, expands maximum vitals, and synchronizes updated status to the client.

**Acceptance Scenarios**:
1. **Given** a character gaining experience that does not cross the next level threshold, **When** experience is awarded, **Then** the server updates current experience and synchronizes it to the client.
2. **Given** a character gaining sufficient experience to cross one or more level thresholds, **When** experience is applied, **Then** the server increments the character level, grants 5 attribute points and 1 skill point per level advanced, expands maximum health, mana, and stamina, and sends a level-up notification to the client.

---

### User Story 4 - Attribute & Skill Point Distribution (Priority: P2)
A player holding unallocated attribute or skill points distributes them into chosen base attributes (Strength, Dexterity, Vitality, Energy) or learned skills. The server validates that sufficient points remain, increments the requested attribute or skill, recalculates derived stats (such as physical damage from strength, hit rate/evasion from dexterity, max health from vitality, and max mana from energy), and synchronizes the changes.

**Acceptance Scenarios**:
1. **Given** a character with unallocated attribute points, **When** the client submits an attribute allocation request for a valid attribute, **Then** the server deducts the allocated points from unallocated reserves, adds them to the attribute, recalculates derived statistics, and synchronizes the new values.
2. **Given** a character with unallocated skill points, **When** the client submits a skill point allocation for a learned skill that has not reached its maximum level cap, **Then** the server deducts 1 unallocated skill point, raises the skill level, and synchronizes the updated skill level.
3. **Given** an allocation request specifying more points than available or targeting an unlearned skill, **When** received, **Then** the server rejects the request without deducting points or modifying attributes.

---

### User Story 5 - Player Defeat & Town Revival (Priority: P2)
When a player's health reaches zero, the server places the player in dead posture, broadcasts the death event to surrounding observers, and clears all monster aggro against the deceased. When the player requests revival, the server restores vitals, sets combat posture to peaceful mode, teleports the character to their saved town revival point, and broadcasts the revival notification.

**Acceptance Scenarios**:
1. **Given** a player whose health reaches zero in combat, **When** the fatal hit is registered, **Then** the server marks the player dead, broadcasts the death notification to the 3x3 region neighborhood, and clears active hostile monster aggro toward the player.
2. **Given** a dead player submitting a revival request, **When** processed, **Then** the server restores full vitals, resets `FightMode` to 0 (peaceful), moves the player to their bound town sanctuary coordinates, broadcasts the revival notification, and synchronizes their updated position.

---

## 4. Edge Cases

- **Multiple Simultaneous Level-Ups**: A low-level character receives a massive experience reward; the progression calculation iteratively resolves multiple level thresholds, awarding 5 attribute points and 1 skill point per tier crossed without losing surplus experience.
- **Player Disconnects While Dead**: If a player disconnects in dead state, the server persists their dead status; upon subsequent reconnection or login, the player re-enters in town sanctuary at their revival coordinates with restored vitals.
- **Target Dies While Monster Is Mid-Swing**: If a monster initiates an attack animation but the target dies before damage connects, the attack concludes harmlessly without null-reference errors.
- **Leashing Through Obstacles**: If a monster's direct return path is obstructed, the leash controller uses obstacle-aware stepping to return home safely rather than getting permanently stuck in a wall.

---

## 5. Functional Requirements

- **FR-001**: The system MUST implement a deterministic 18-Hz monster AI state machine supporting `IdlePatrol`, `Pursuit`, `Attack`, `LeashReturn`, and `DeadRespawnWait` states.
- **FR-002**: Monsters MUST detect hostile players within their configured aggro radius and acquire aggro immediately upon sustaining damage from an attacker.
- **FR-003**: Monsters MUST disengage and return to their spawn coordinates if pulled beyond their configured maximum pursuit leash radius.
- **FR-004**: When a monster dies, the system MUST broadcast a death notification to the 3x3 region area of interest, award experience to the killer, and initiate a respawn timer.
- **FR-005**: Upon respawn timer expiration, the monster MUST reappear at its original spawn anchor with full vitals and reset AI state.
- **FR-006**: The system MUST evaluate experience thresholds and trigger character level-ups, awarding exactly 5 unallocated attribute points and 1 unallocated skill point per level advanced.
- **FR-007**: The system MUST expand maximum health, mana, and stamina upon level-up according to class Five-Elements series growth tables.
- **FR-008**: The system MUST support client allocation of base attribute points across Strength, Dexterity, Vitality, and Energy, validating point availability and recalculating derived stats.
- **FR-009**: The system MUST support client allocation of skill points into learned skills, validating level caps and point availability.
- **FR-010**: When a player dies, the system MUST broadcast player death, clear monster aggro, and lock combat actions until revival.
- **FR-011**: The system MUST support player town revival, restoring vitals, resetting combat mode to peaceful (`FightMode = 0`), and moving the character to bound town revival coordinates.
- **FR-012**: Character progression state (level, experience, base attributes, unallocated points, learned skill levels) MUST be durably persisted.

---

## 6. Measurable Success Criteria

- **SC-001**: 100% of monster AI states execute deterministically on Phase 200 of the 18-Hz loop without timing jitter or skipped ticks.
- **SC-002**: 100% of monsters pulled beyond leash distance successfully return to their spawn origin without pathing deadlocks.
- **SC-003**: 100% of level-up events award exactly 5 base attribute points and 1 skill point per level, strictly conserving point totals across multiple level increments.
- **SC-004**: 100% of player revivals cleanly reposition the character in town sanctuary with peaceful posture restored.
- **SC-005**: 100% of invalid attribute or skill allocations (exceeding available points, allocating into unlearned skills) are rejected with zero corrupted state.
