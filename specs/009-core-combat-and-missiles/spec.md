# Feature Specification: Core Combat and 64-Direction Missiles

**Feature Branch**: `009-core-combat-and-missiles`  
**Created**: 2026-11-11  
**Status**: Specification Complete  
**Input**: Single-responsibility decomposition of Five-Elements combat mechanics, martial arts skill casting, aura switching, 64-direction fixed-point projectile trajectory physics, multi-channel damage mitigation, and status ailments.

---

## 1. Overview & Context

In the martial arts world of Võ Lâm Truyền Kỳ, combat resolution is governed by the ancient Five-Elements philosophy (*Ngũ Hành*: Kim $\rightarrow$ Thủy $\rightarrow$ Mộc $\rightarrow$ Hỏa $\rightarrow$ Thổ $\rightarrow$ Kim generation and conquest interactions), 64-direction discrete trigonometric projectile kinematics, and real-time status ailments.

This specification isolates the **Core Combat and Missiles** vertical slice:
1. **Martial Skill Execution & Cooldown Management**: Verification of combat posture, mana/stamina costs, security validation, weapon constraints, and cast cooldown timers.
2. **64-Direction Deterministic Projectile Simulation**: Spawning, stepping, and collision testing of ranged missiles across discrete 64-direction trigonometric angles on the fixed-frequency simulation loop.
3. **Multi-Channel Five-Elements Damage & Resistance Resolution**: Physical, Cold, Fire, Light, Poison, and Magic damage calculations with elemental counter-multiplier amplification and mitigation caps.
4. **Elemental Status Ailments & Hurt Recoil**: Cold freezing/slowdown, Poison damage-over-time ticks, Lightning stun lockouts, and hurt recovery animation synchronization.
5. **Right-Hand Aura Toggling & Passive Buffs**: Activation, switching, and continuous application of aura effects.

---

## 2. Clarifications & Ground-Truth Context

- **Skill Targeting Modes**: Skills support both locked-entity targeting (homing or direct application to a specific character or creature) and ground-coordinate targeting (directional projectile fan, area bombardment, or directional melee thrust).
- **Trigonometric Representation**: Missiles compute trajectories using 64 discrete angular headings indexed `0..63`, corresponding to pre-computed sine and cosine lookup tables scaled by 1,024 to preserve integer determinism without floating-point drift.
- **Five-Elements Counter Multipliers**: An elemental advantage (e.g., Water attacking Fire) scales elemental damage up, while an elemental disadvantage (e.g., Water attacking Earth) suffers damage attenuation, in addition to percentage-based elemental resistance mitigations.
- **Relation Matrix**: Skill hits only affect valid targets determined by combat flags: offensive skills target entities in enemy relation (monsters or players with active combat/slaughter flags enabled outside peaceful safe zones).

---

## 3. User Scenarios & Acceptance Criteria

### User Story 1 - Martial Skill Execution & Area Broadcast (Priority: P1)
A combatant outside a peaceful safe zone executes an active martial arts skill or basic weapon attack against a target entity or target coordinate. The server verifies security checks, combat readiness, vital costs (mana and stamina), weapon requirements, and cooldown limits, deducting vitals and broadcasting the skill casting animation to all nearby observers within the local Area of Interest (AOI).

**Acceptance Scenarios**:
1. **Given** an in-world character in active combat mode having sufficient mana and stamina and knowing the requested skill, **When** the combatant submits a skill execution command targeting an in-range entity or coordinate, **Then** the server verifies the security validation token, deducts mana and stamina, initiates the skill cooldown timer, and broadcasts the casting event to all observers in the 3x3 region neighborhood.
2. **Given** a character standing inside a peaceful safe sanctuary, on skill cooldown, lacking required mana or stamina, or submitting an invalid security token, **When** a skill cast command is submitted, **Then** the server cleanly rejects the action without deducting vitals, invoking cooldowns, or broadcasting animations.
3. **Given** a combatant with an unequipped weapon or an incompatible weapon type for a weapon-restricted skill, **When** a skill execution is requested, **Then** the action is rejected without state mutation.

---

### User Story 2 - Deterministic 64-Direction Projectile Simulation (Priority: P1)
When a ranged or projectile-based skill is cast, the server instantiates projectile objects that travel along discrete 64-direction paths on each simulation tick. The projectiles advance based on discrete trigonometric vector components, test for obstacle collisions and target intersections within the area of interest, and expire upon hitting a target, striking a solid barrier, or exceeding maximum lifetime.

**Acceptance Scenarios**:
1. **Given** an active projectile spawned in a map zone, **When** the simulation tick advances, **Then** the projectile position increments along its assigned 64-direction heading using fixed-point velocity components without floating-point accumulation errors.
2. **Given** a flying non-piercing projectile encountering a valid enemy hitbox or impassable wall barrier, **When** a collision is detected, **Then** the projectile detonates or terminates, delivering its damage payload to the struck entity or vanishing upon barrier contact.
3. **Given** a projectile with piercing capability, **When** it strikes an enemy, **Then** it delivers damage to that target, decrements its remaining pierce count, and continues flying along its trajectory until its pierce count or lifetime reaches zero.

---

### User Story 3 - Five-Elements Damage & Resistance Resolution (Priority: P1)
When a melee attack or projectile collides with a valid target, the system calculates multi-channel damage across Physical, Cold, Fire, Lightning, Poison, and Magic channels. Damage is scaled by attacker attack rating and defender defense rating, adjusted by Five-Elements generation/conquest relationships, and mitigated by the defender's corresponding elemental resistances.

**Acceptance Scenarios**:
1. **Given** an attacker of Water series casting a Water elemental skill against a Fire series defender, **When** the damage is evaluated, **Then** the elemental conquest advantage scales the elemental damage upward before applying the defender's Fire resistance mitigation.
2. **Given** a defender with high elemental resistance, **When** damage is computed, **Then** resistance reduces the incoming elemental damage up to the configured standard resistance cap, while ensuring true damage minimums are respected.
3. **Given** an attack where attack rating fails to beat the defender's defense rating threshold, **When** the hit check resolves, **Then** the attack registers as a miss or glancing blow within minimum and maximum hit probability clamps (5% to 95%).

---

### User Story 4 - Status Effects & Hurt Recoil (Priority: P2)
Successful hits that deliver cold, poison, or lightning damage apply corresponding status ailments to the victim. Cold damage applies movement and attack speed reduction for a duration; Poison damage applies periodic health loss ticks over time; Lightning damage applies brief stun immobilization; and qualifying high-impact hits trigger hurt recoil animation frames.

**Acceptance Scenarios**:
1. **Given** a target taking Cold damage, **When** the cold effect is applied, **Then** the victim's movement and action animations are slowed for the duration determined by cold power and cold resistance.
2. **Given** a target afflicted by Poison, **When** simulation ticks elapse, **Then** the victim loses health at fixed periodic intervals until the poison duration expires.
3. **Given** a target suffering Lightning stun or a qualifying high-impact hit, **When** the hit resolves, **Then** the victim enters hurt recoil or stun state, temporarily interrupting active movement or actions, and an observer broadcast is dispatched.

---

### User Story 5 - Right-Hand Aura Skill Switching (Priority: P2)
A character who has learned passive aura skills toggles their active right-hand aura. The server verifies ownership of the skill, updates the combatant's active aura slot, and synchronizes the aura state.

**Acceptance Scenarios**:
1. **Given** a character who knows a learned aura skill, **When** the player requests switching to that aura, **Then** the server validates ownership and activates the aura, applying its ongoing passive enhancements.
2. **Given** an attempt to switch to an unlearned aura or an invalid skill identifier, **When** requested, **Then** the server rejects the request without altering current aura state.

---

## 4. Edge Cases

- **Target Disappears Mid-Flight**: A projectile travels toward an entity that teleports, changes maps, or dies before impact; on subsequent ticks the projectile safely continues to its maximum range or terminates cleanly without null-pointer errors or orphaned state.
- **High-Density Projectile Bursts**: Dozens of players and creatures fire multi-missile attacks simultaneously in the same region; projectiles use pre-allocated slot pools to guarantee consistent 18-Hz tick timing without garbage collection spikes.
- **Overlapping Status Ailments**: A target afflicted by poison is struck by a secondary poison attack; the system refreshes duration or retains the higher damage-per-tick according to poison stacking rules without infinite cumulative decay.
- **Friendly Fire & Safe Sanctuaries**: Attacks fired from or directed into peaceful town regions are neutralized; non-hostile entities sharing friendly camp relations take no damage from allied area-of-effect projectiles.

---

## 5. Functional Requirements

- **FR-001**: The system MUST authenticate skill cast requests against combat posture (`FightMode == 1`), caster life status, cooldown frames, mana/stamina costs, attack range, and anti-cheat validation tokens.
- **FR-002**: The system MUST support directional targeting toward world coordinates as well as locked homing targeting toward entity identifiers.
- **FR-003**: The system MUST broadcast skill execution animations to all observing clients within the 3x3 region area of interest upon successful cast acceptance.
- **FR-004**: Ranged projectiles MUST be simulated deterministically on the fixed 18-Hz tick loop using 64-direction discrete trigonometric heading tables.
- **FR-005**: Projectile positions MUST advance using fixed-point coordinate mathematics to eliminate floating-point drift across platforms.
- **FR-006**: Projectile lifecycle MUST support maximum lifetime frame expiration, obstacle collision termination, single-target detonation, and multi-target penetration up to a configured pierce count.
- **FR-007**: Damage evaluation MUST resolve across 6 distinct channels: Physical, Cold, Fire, Lightning, Poison, and Magic.
- **FR-008**: Elemental damage MUST apply Five-Elements generation and conquest multipliers based on attacker and defender elemental series.
- **FR-009**: Damage reduction MUST apply defender defense and elemental resistances, clamped between standard minimum and maximum resistance caps.
- **FR-010**: Cold damage MUST apply temporary action and movement speed reduction scaled by the victim's cold resistance.
- **FR-011**: Poison damage MUST apply periodic damage-over-time health reductions at fixed frame intervals across its active duration.
- **FR-012**: Lightning damage and heavy blows MUST apply stun or hurt recovery lockout frames and broadcast hurt recoil notifications to surrounding observers.
- **FR-013**: The system MUST allow characters to switch active right-hand aura skills among learned aura abilities and persist the active aura selection.
- **FR-014**: The system MUST maintain pre-allocated projectile slot pools per map zone to ensure predictable memory usage and bounded tick execution times.

---

## 6. Measurable Success Criteria

- **SC-001**: 100% of valid martial skill cast requests result in accurate vital deductions, cooldown initiation, and area-of-interest animation broadcasts.
- **SC-002**: 100% of projectile trajectories match exact 64-direction trigonometric lookup coordinates without floating-point drift over maximum flight range.
- **SC-003**: Five-Elements damage calculations demonstrate exact mathematical parity with reference formulas across all 25 series pairings (`5 x 5` attacker vs defender).
- **SC-004**: Under concurrent loads of 50 combatants continuously executing multi-projectile skills in a single map zone, 99th-percentile zone simulation tick duration remains under 15 milliseconds.
- **SC-005**: 100% of invalid combat requests (casting in safe zones, forged check values, insufficient mana, unlearned skills) are rejected with zero corrupted state and zero server crashes.
