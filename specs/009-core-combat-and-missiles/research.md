# Research & Technical Rationale: Core Combat and 64-Direction Missiles

**Feature Branch**: `009-core-combat-and-missiles`  
**Date**: 2026-11-11  

---

## 1. Ground-Truth Source Analysis

The combat and missile simulation models are derived from the canonical C++ reference implementations in `jx-win-src`:
- `Sources/Core/Src/KSkills.cpp`: Skill execution rules, cooldown checks, vital consumption, and effect scaling.
- `Sources/Core/Src/KMissle.cpp`: 64-direction discrete projectile motion, fixed-point step accumulation, collision detection, and missile expiration.
- `Sources/Core/Src/KNpc.cpp` & `KNpcAttribModify.cpp`: Attack rating vs defense rating calculations, Five-Elements damage resolution, and resistance mitigations.
- `Sources/Core/Src/KProtocol.h`: Wire packet formats `c2s_npcskill` (`0x4D`), `c2s_autoattacknpc` (`0x87`), `c2s_changeauraskill` (`0x6F`), `s2c_skillcast` (`0x5A`), `s2c_castskilldirectly` (`0x85`), and `s2c_broadcasenetcommand` (`0x56` with `do_hurt`).

---

## 2. 64-Direction Fixed-Point Kinematics

In the Kingsoft game engine, angles are divided into 64 discrete angular intervals (`0..63`). This discrete discretization ensures 100% deterministic trajectory stepping on both client and server:
- Angle index `0` points directly East (`+X`), incrementing clockwise:
  - `0`: East (`(1024, 0)`)
  - `16`: South (`(0, 1024)`)
  - `32`: West (`(-1024, 0)`)
  - `48`: North (`(0, -1024)`)
- Trigonometric values are stored as integers scaled by `1024`:
  $$\text{SinBuffer}[d] = \lfloor 1024 \times \sin\left(\frac{2\pi \times d}{64}\right) \rfloor$$
  $$\text{CosBuffer}[d] = \lfloor 1024 \times \cos\left(\frac{2\pi \times d}{64}\right) \rfloor$$
- Position advancement per 18-Hz simulation tick uses fixed-point coordinates shifted by 10 bits (`FixedX = MpsX << 10`, `FixedY = MpsY << 10`):
  $$\text{StepFixedX} = \text{SpeedMpsPerFrame} \times \text{CosBuffer}[d]$$
  $$\text{StepFixedY} = \text{SpeedMpsPerFrame} \times \text{SinBuffer}[d]$$
  $$\text{FixedX}_{t+1} = \text{FixedX}_t + \text{StepFixedX}$$
  $$\text{FixedY}_{t+1} = \text{FixedY}_t + \text{StepFixedY}$$
  $$\text{CurrentMpsX} = \text{FixedX} \gg 10$$
  $$\text{CurrentMpsY} = \text{FixedY} \gg 10$$

---

## 3. Five-Elements Combat Mathematics

Combat damage is split across 6 distinct channels: Physical, Cold, Fire, Light, Poison, and Magic.

### 3.1 Five-Elements Matrix Interplay
- **Generation (*Tương Sinh*)**: Kim $\rightarrow$ Thủy $\rightarrow$ Mộc $\rightarrow$ Hỏa $\rightarrow$ Thổ $\rightarrow$ Kim.
- **Conquest (*Tương Khắc*)**: Kim $\rightarrow$ Mộc $\rightarrow$ Thổ $\rightarrow$ Thủy $\rightarrow$ Hỏa $\rightarrow$ Kim.
- When an attacker's element conquers the defender's element, elemental damage gains a conquest bonus multiplier:
  $$\text{ConquestMultiplier} = 1.25 \quad (+25\% \text{ elemental damage})$$
- When an attacker's element is conquered by the defender's element, elemental damage suffers attenuation:
  $$\text{ConqueredMultiplier} = 0.80 \quad (-20\% \text{ elemental damage})$$

### 3.2 Hit Probability & Defense
Hit determination compares Attacker Attack Rating ($AR$) with Defender Defense Rating ($DR$):
$$\text{HitChance} = \text{Clamp}\left(\frac{AR}{AR + DR} \times 100\%, \; 5\%, \; 95\%\right)$$
Even with overwhelming defense, a minimum 5% hit rate is preserved; conversely, maximum hit rate is capped at 95%.

### 3.3 Resistance Mitigation Caps
Each elemental damage channel is reduced by the defender's corresponding resistance:
$$\text{Damage}_{\text{element}} = \text{RawDamage}_{\text{element}} \times \left(1 - \frac{\text{Clamp}(\text{Resist}_{\text{element}}, -100, 75)}{100}\right)$$
Standard maximum resistance is capped at `75%` (expandable up to `95%` hard cap with special enchantments).

---

## 4. Status Ailments & Hurt Recoil Mechanics

- **Cold Status Effect**:
  Reduces movement speed and attack speed proportional to cold intensity for `ColdFreezeFrames`.
- **Poison Status Effect**:
  Applies non-fatal or lethal damage ticks every `PoisonIntervalFrames` (typically every 9 or 18 frames) until `PoisonRemainingFrames` expires. Multiple poison strikes do not compound infinitely; the more potent poison rate takes precedence.
- **Lightning Stun & Hurt Recoil**:
  When damage exceeds an entity's stagger threshold, `HurtRecoveryRemainingFrames` lock out input and movement actions, accompanied by `0x56` (`CmdKind = 9`, `do_hurt`) broadcast to all observers within the 3x3 region AOI.

---

## 5. Architectural Decoupling: Phase 100 & Phase 300 Tick Systems

In the 18-Hz Zone Modular Spine:
- **Phase 100 (`StatusEffectZoneTickSystem`)**: Evaluates Poison periodic damage ticks, decrements Freeze/Stun/Hurt recovery timers, and updates entity vitals before movement or combat resolution.
- **Phase 300 (`MissileZoneTickSystem`)**: Advances projectile fixed-point coordinates, detects entity and barrier collisions, evaluates `CombatDamageCalculator`, and reclaims expired missile slots.
- Both systems operate on pre-allocated arrays, ensuring zero heap allocations on hot tick loops.
