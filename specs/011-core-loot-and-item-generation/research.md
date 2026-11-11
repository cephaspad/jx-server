# Research & Technical Rationale: Core Loot, Item Generation & Ground Objects

**Feature Branch**: `011-core-loot-and-item-generation`  
**Date**: 2026-11-11  

---

## 1. Ground-Truth Source Analysis

The item generation rules, drop table parsing, and ground object mechanics are ported from Kingsoft C++ sources:
- `Sources/Core/Src/KItemGenerator.cpp` & `KItemSet.cpp`: Equipment generation version 4 (`m_wVersion = 4`), rolling durability, base attributes, explicit prefix/suffix affixes, and hidden Five-Elements affixes.
- `Sources/Core/Src/KObj.cpp` & `KObjSet.cpp`: Ground object lifecycle, walkable cell scattering, ownership protection timer, and spatial region management.
- `settings/droprate/**/*.ini`: Monster drop-rate definitions specifying drop chances, money ranges, and item pools.
- `settings/item/004/*.txt`: Item templates, base stats, and magic attribute candidate pools.
- `Sources/Core/Src/KProtocol.h`: Wire packet formats `s2c_objadd` (`0x64`), `s2c_objremove` (`0x67`), `c2s_requestobj` (`0x4A`), `c2s_objmouseclick` (`0x72`), `c2s_playerpickupitem` (`0x5A`), `c2s_playerthrowawayitem` (`0x5E`), and `c2s_throwawayallmedicine` (`0x83`).

---

## 2. Procedural Equipment Generation (`KItemGenerator` Version 4)

In version 4 of the item generation pipeline:
- **Base Attributes**:
  Rolled within template bounds:
  $$\text{BaseDurability} = \text{Random}(\text{MinDurability}, \; \text{MaxDurability})$$
  $$\text{CurrentDurability} = \text{MaxDurability}$$
- **Magic Attribute Slots (6 Max)**:
  Blue equipment (`ItemQualityKind.Normal = 0` with rolled affixes) can carry up to 6 magic attributes:
  - **Explicit Slots (`0, 2, 4`)**: Selected from candidate affixes marked `IsSuffix = false` (prefixes), active immediately upon equipping.
  - **Hidden Five-Elements Slots (`1, 3, 5`)**: Selected from candidate affixes marked `IsSuffix = true` (suffixes). These affixes remain inactive until activated by the Five-Elements equipment activation ring.
- **Candidate Filtering & Selection**:
  Candidates are filtered by:
  1. `Level <= ItemLevel`
  2. `WeightsByEquipPart[EquipDetailType] > 0`
  3. `ElementSeries == ItemSeries` (for elemental affixes) or series-independent
  4. Luck bonus: increases candidate weight for higher-level affixes:
     $$\text{EffectiveWeight} = \text{BaseWeight} \times \left(1 + \frac{\text{Luck}}{100} \times \text{AffixLevel}\right)$$

---

## 3. Ground Object Lifecycle & Spatial Partitioning

Ground objects (`KObj`) are managed within spatial region slots on Phase 400 (`ZoneTickPhase.GroundObjects = 400`):
- **Cell Placement**:
  When a creature dies at `(DeathMpsX, DeathMpsY)`, drops are scattered on surrounding walkable cells by sampling nearby coordinates and testing against the map's obstacle grid (`Obstacle_Full`).
- **Timing Invariants**:
  - `OwnerProtectionExpiryTick = CurrentTick + DefaultLootOwnerProtectionFrames` (180 frames / 10s). Non-killers attempting pickup before this tick are rejected.
  - `ExpireTick = CurrentTick + DefaultGroundObjectLifeFrames` (1080 frames / 60s). Ground objects are pruned when `CurrentTick >= ExpireTick`, triggering `s2c_objremove` (`0x67`, `Reason = 0`).

---

## 4. Zero-Duplication Atomic Pickup Architecture

Item duplication is a notorious vulnerability in MMORPG servers with multi-threaded or asynchronous pickup loops. In JXServer:
1. **Single-Threaded Shard Execution**:
   All zone commands (`PickupGroundObjectZoneCommand`) execute sequentially on the dedicated 18-Hz shard loop of `MapZoneWorker`.
2. **Atomic Verification & Removal**:
   When player $A$ and player $B$ attempt to pick up the same `ObjId` in the same frame:
   - Command $A$ executes first: verifies player distance, verifies ownership protection, verifies inventory space in $A$'s bag, removes the object from `SpatialRegionSlot.GroundObjects`, adds the item to $A$'s inventory, and sends `s2c_objremove` (`0x67`, `Reason = 1`) and `s2c_syncitem` (`0x5F`).
   - Command $B$ executes second: queries the region for `ObjId`, finds that it no longer exists, and safely aborts with zero side effects.
3. **No Lock Contention**:
   Because all spatial mutations occur on the zone thread, zero mutex locks or database round-trips occur on the hot path, achieving high throughput and mathematical zero duplication.
