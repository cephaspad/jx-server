# Feature Specification: Core Game Data Pipeline (`JXData Standard v1`)

**Feature Directory**: `specs/001-core-game-data-pipeline`  
**Status**: Approved  
**Applies To**: Authoring Game Data (`data/`), Schema Contracts (`schemas/`), Pre-Compiled Delivery Artifacts (`artifacts/`), Runtime Asset Registries, and Migration Tooling  

---

## 1. Executive Summary & Value Proposition

The legacy server distribution fragments static game data across thousands of disparate, undocumented files: tab-delimited text tables with unverified column schemas, untyped initialization scripts, binary regional obstacle maps, and procedural scripts that mingle balance constants with execution logic. This dispersion creates significant operational friction: balance adjustments require editing raw tables across multiple legacy encodings, spatial maps cannot be validated prior to launch, and game servers risk crashing at runtime due to malformed data or missing references.

The **Core Game Data Pipeline** establishes a modernized, dual-tier game data architecture (**`JXData Standard v1`**):
1. **Tier 1 (Canonical Source of Truth)**: Validated, human-readable structured documents organized by business domain (`maps/`, `npcs/`, `players/`, `skills/`, `items/`, `loot/`, `shops/`) governed by strict structural schemas with complete foreign-key referential integrity and unit-explicit spatial/timing dimensions.
2. **Tier 2 (High-Speed Binary Delivery)**: Zero-copy, pre-compiled binary snapshots equipped with cryptographic integrity verification, enabling instant, allocation-free server startup.
3. **Immutable Runtime Registries**: Read-only in-memory registries serving the simulation loop with zero file system or parsing overhead during execution.
4. **Offline Migration Tooling**: A decoupled extraction and compilation suite capable of inspecting legacy server files, evaluating scaling formulas, and validating full data parity.

---

## 2. User Stories & Acceptance Scenarios

### User Story 1 - Canonical Structured Data Authoring & Schema Enforcement (Priority: P1)

As a game designer or content engineer, I want to define, inspect, and modify game balance attributes, map boundaries, monster templates, skill scaling curves, and equipment drop rates in structured, human-readable documents validated against explicit schemas, so that structural errors, missing references, and out-of-range balance values are caught instantly during authoring before reaching runtime.

**Why this priority**: Correct, schema-validated game assets are the foundational prerequisite for all simulation mechanics, zone instances, combat encounters, and item economies.

**Independent Test**: Can be validated by executing schema validation across all canonical documents, asserting that 100% of documents comply with their respective schema contracts and that invalid attributes or missing cross-references are rejected with descriptive diagnostic paths.

**Acceptance Scenarios**:
1. **Given** a collection of canonical game data documents organized by domain, **When** structural schema validation is executed, **Then** all documents pass validation without warnings or errors.
2. **Given** a modified game data document containing a misspelled field, out-of-range numeric value, or broken foreign reference to another catalog, **When** validation runs, **Then** the validation tool immediately halts, cites the file path and line location, and prevents artifact publication.
3. **Given** spatial coordinates or duration fields in data documents, **When** inspected, **Then** all properties feature explicit unit designations (such as world pixels, obstacle grid cells, spatial regions, or simulation frames), preventing unit-conversion ambiguities.

---

### User Story 2 - Instant Zero-Copy Runtime Catalog Loading (Priority: P1)

As a server operator or automated test harness, I want the game server to load complete world maps, monster definitions, skills, items, and loot tables in milliseconds without performing text parsing, file system exploration, or dynamic allocations on startup, so that simulation instances and test harnesses launch instantly and operate with predictable low-latency memory footprints.

**Why this priority**: Sub-second startup and predictable memory allocation are required for zero-downtime rolling reloads, autonomous testing harnesses, and high-density simulation clusters.

**Independent Test**: Can be tested by starting the runtime catalog loader from pre-compiled binary artifacts, measuring elapsed startup duration, asserting sub-second loading time, and verifying that in-memory entity lookups return identical data to canonical sources.

**Acceptance Scenarios**:
1. **Given** pre-compiled binary asset snapshot artifacts verified by cryptographic checksums, **When** the server initializes, **Then** all catalogs load into immutable in-memory registries in under 100 milliseconds without executing legacy file parsers.
2. **Given** a corrupted or mismatched binary artifact whose cryptographic checksum fails verification, **When** initialization starts, **Then** the runtime halts startup before opening listener ports and reports the integrity discrepancy.
3. **Given** loaded in-memory registries, **When** the simulation loop performs high-frequency lookups for templates by identity or semantic code slug, **Then** lookups complete in constant $O(1)$ time with zero heap allocations.

---

### User Story 3 - Standalone Offline Extraction & Parity Verification (Priority: P2)

As a tools engineer or legacy systems archivist, I want a standalone command-line extraction tool to parse raw legacy asset archives, decompress binary map regions, evaluate legacy balance formulas, and produce both canonical documents and binary snapshots alongside an automated parity report, so that mechanical behavior matches historical reference servers without requiring legacy runtime dependencies.

**Why this priority**: Preserves historical game accuracy while cleanly divorcing the modernized runtime server from brittle legacy file formats and script interpreters.

**Independent Test**: Can be validated by running the offline extraction tool against legacy server data and asserting that the resulting parity report confirms zero missing rows, bitwise matching obstacle grids, and zero unmapped entities.

**Acceptance Scenarios**:
1. **Given** a legacy server asset directory containing tab-delimited files, map data archives, and balance script curves, **When** the offline exporter executes, **Then** it produces complete, canonical structured documents matching 100% of historical entity records.
2. **Given** procedural balance curves and skill scaling formulas in legacy scripts, **When** processed by the extraction tool, **Then** formulas are pre-evaluated across their full level ranges into static lookup tables, preserving exact numerical outcomes without requiring script evaluation at runtime.
3. **Given** legacy text records containing mixed character encodings, **When** extracted, **Then** all display names and descriptions are normalized into standard Unicode representations without corruption or mojibake.

---

## 3. Edge Cases

- **Missing or Corrupted Static Assets**: When a required data document or binary snapshot is missing or fails integrity checks, the system must fail fast during startup, reporting the exact identifier or path, rather than degrading into an inconsistent runtime state.
- **Duplicate Identifier Collision**: When two entities in the same catalog share identical display names or codes, the pipeline must reject the collision or symmetrically disambiguate identifiers so no ambiguity exists at runtime.
- **Over-Cap Progression Lookups**: When skills or player stats exceed standard baseline levels (e.g., through equipment bonuses), the pre-evaluated curve tables must support over-cap ranges up to the engine maximum (level 64) without out-of-bounds indexing.
- **Sparse Map Grid Regions**: Map regions that contain zero obstacles or entities must be represented sparsely, omitting empty blocks to prevent unnecessary storage and memory consumption.
- **Cross-Catalog Foreign Reference Drift**: If an item references a non-existent skill or a monster references a deleted loot table, referential integrity checks must flag the discrepancy during compilation.

---

## 4. Functional Requirements

- **FR-001**: The game data pipeline MUST maintain canonical game assets in human-readable, schema-validated structured documents organized by business domain (`maps/`, `npcs/`, `players/`, `skills/`, `items/`, `loot/`, `shops/`).
- **FR-002**: Every canonical data document MUST validate against an explicit schema specification enforcing property types, value boundaries, required fields, and structural constraints.
- **FR-003**: All cross-references between catalogs (such as monsters referencing skills or loot tables referencing item codes) MUST enforce 100% foreign-key referential integrity.
- **FR-004**: All spatial coordinates, boundaries, speeds, and timing attributes in data documents MUST include explicit unit suffixes (`*MpsX`, `*MpsY`, `*CellX`, `*CellY`, `*RegionX`, `*RegionY`, `*Frames`, `*Seconds`) to eliminate conversion ambiguities.
- **FR-005**: Procedural balance curves and scaling formulas MUST be pre-evaluated during compilation into fixed lookup tables covering the full supported level scale (levels 1 through 64) to eliminate runtime formula calculation.
- **FR-006**: The pipeline MUST support compiling canonical documents into zero-copy, pre-compiled binary snapshot artifacts paired with cryptographic checksums (`SHA-256`) for rapid runtime distribution.
- **FR-007**: The game server runtime MUST load static assets into immutable in-memory catalog registries without invoking legacy file readers, table parsers, or archive decompressors during runtime execution.
- **FR-008**: All catalog registries MUST support constant-time $O(1)$ lookups by numeric identity and semantic code slug with zero runtime memory allocations.
- **FR-009**: The pipeline MUST provide a standalone offline extraction tool capable of reading legacy archive formats, decoding legacy character encodings, evaluating balance formulas, and emitting canonical documents and binary snapshots.
- **FR-010**: The offline extraction tool MUST generate an automated parity verification report certifying 100% record coverage, zero unaccounted files, and bit-level obstacle grid reconstruction.

---

## 5. Key Entities

- **Map Document**: Consolidates map dimensions, environmental rules, walkable and barrier cell matrices, static non-player character placements, interactive map objects, and portal teleport destinations.
- **NPC Template**: Defines monster, companion, and non-player character archetypes, including base attributes, combat scaling curves, movement speeds, active skill configurations, and assigned loot tables.
- **Player Progression Template**: Configures base attributes across character series, vital statistic growth formulas, elemental affinity matrices, faction definitions, and starter village coordinates.
- **Skill & Projectile Catalog**: Defines combat martial arts, buffs, auras, projectile trajectories, damage scaling curves, multi-stage child skills, and conditional proc triggers.
- **Item & Affix Catalog**: Defines equipment archetypes, consumable items, quest items, randomized magic attribute pools, fixed golden set bonuses, and crafting/enchasing recipes.
- **Loot Table**: Configures weighted item drop trees, currency drop probabilities, level-scaled equipment pools, and boss reward distributions.
- **Shop Catalog**: Defines multi-currency merchant goods inventories, pricing rules, repurchase terms, and item expiration rules.
- **Binary Snapshot Artifact**: A single consolidated binary bundle containing pre-indexed catalog registries for instant runtime ingestion.

---

## 6. Success Criteria

- **SC-001**: 100% of canonical game data documents pass automated schema validation and referential integrity checks with zero errors.
- **SC-002**: Complete static asset initialization from binary snapshot artifacts finishes in under 100 milliseconds on standard server hardware with zero runtime memory allocations.
- **SC-003**: The runtime server assembly contains zero legacy file parsers, archive decompressors, or raw text format decoders.
- **SC-004**: In-memory catalog lookups for entities across all 7 domains achieve constant $O(1)$ time complexity.
- **SC-005**: The offline migration parity verifier confirms 1:1 physical record counts, 100% file inventory coverage, and identical cell-by-cell obstacle grid reconstruction against historical reference distributions.
