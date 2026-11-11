# Research & Ground-Truth Rationale: Core Game Data Pipeline (`JXData Standard v1`)

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  
**Standard**: [docs/data-architecture-standard.md](../../docs/data-architecture-standard.md)  

---

## 1. Architectural Decisions & Alternatives Considered

### Decision 1: Dual-Tier Architecture (JSON Authoring + MemoryPack Binary Snapshot)
- **Problem**: Legacy JXLinux game assets are stored across thousands of tab-delimited text tables (`.txt`), `.ini` files, binary `.dat` regions, and `.lua` scripts. Loading and parsing these files on server startup required seconds of disk I/O, regex string parsing, and complex character set transcoding, leading to slow startup and runtime memory fragmentation.
- **Alternatives Considered**:
  1. *Continue loading legacy text tables directly in C#*: Rejected. Preserving legacy parsing in the runtime violates Constitution Principle V, exposes runtime to text encoding glitches (TCVN3 vs GBK), and creates slow boot times.
  2. *SQLite/Relational database for static assets*: Rejected. High overhead for immutable static balance tables, foreign key queries add unnecessary latency to 18-Hz simulation ticks.
  3. *Single binary blob only (no human-readable files in Git)*: Rejected. Unusable for Git diffing, human review, branch merging, or web-based editing.
- **Selected Solution**: **Dual-Tier (`JXData Standard v1`)**:
  - **Tier 1 (Authoring)**: Canonical UTF-8 JSON files in `data/` validated against JSON Schema Draft 2020-12 in `schemas/`.
  - **Tier 2 (Delivery)**: Pre-compiled `MemoryPack` binary snapshot (`artifacts/jxdata.pack`) with cryptographic SHA-256 integrity verification. Sub-100ms startup with zero dynamic allocations.

### Decision 2: Elimination of Synthetic Identifiers & Symmetric Semantic Code Slugs
- **Problem**: Previous modernization attempts assigned synthetic sequential IDs (e.g., `100001`, `200001`) to items and skills, breaking compatibility with client packets, legacy table row indices, and historical formulas.
- **Ground-Truth Finding**:
  - Kingsoft's C++ engine identifies entities either by native integer ID (`SubWorld` Map ID `1..999`, NPC Template ID `0..2539`, Skill ID, Missile ID, Shop ID `1..179`) or by physical table row index (`rowId` in `goldequip.txt`, `platinaequip.txt`, `magicattrib_ge.txt`) combined with natural composite keys `(quality, genre, detailType, particularType, level)`.
- **Selected Solution**:
  - Natural composite keys and authentic `rowId`s are preserved 100%. No synthetic numbers are generated.
  - Symmetrical semantic `code` slugs (`ba-lang-huyen`, `dong-bac-ho`, `thanh-phong-kiem`) are generated deterministically. If collisions occur, `-<id>` or `-lv<level>` is appended symmetrically to all colliding entries.

### Decision 3: Physical `KTabFile` Line Indexing and Row-2 Default Inheritance
- **Ground-Truth Analysis (`SwordOnline/Sources/Core/Src/KTabFile.cpp`)**:
  - Kingsoft's `KTabFile` reader treats physical Line 1 (`lineIdx = 0`) as the column header.
  - Line 2 (`lineIdx = 1`) is BOTH the first data record AND the baseline default row: if a cell in Row 3+ is empty (`""`), it inherits the default value from Line 2 (except for optional list slots like `Skill1..4` or `MagicAttrib1..6`).
  - Blank and tab-only rows must NEVER decrement or shift subsequent line indices. For example, `npcs.txt` line 2535 has an empty `Name`, but subsequent rows (2536 *Bậc Thầy Dị Dung*, 2537 *Tiêu Dao Tử*) must retain their exact zero-based template IDs `2535` and `2536`.

### Decision 4: Unit-Explicit Property Suffixes
- **Problem**: In legacy Lua and C++ codebases, spatial units were frequently confused between World Pixels (`Mps`), Grid Cells (`32x32 Mps`), and Spatial Regions (`16x32 Cells = 512x1024 Mps`), and timing was confused between simulation frames (18 Hz) and milliseconds.
- **Selected Solution**: All schema properties enforce explicit unit suffixes:
  - Pixels: `*MpsX`, `*MpsY`, `*RadiusMps`, `*SpeedMpsPerFrame`
  - Cells: `*CellX`, `*CellY`, `*WidthCells`, `*HeightCells`
  - Regions: `*RegionX`, `*RegionY`, `*LeftRegionX`, `*TopRegionY`
  - Durations: `*Frames`, `*Seconds`, `*Ms`

### Decision 5: Pre-Evaluated Curve Lookup Tables for 18-Hz Performance
- **Problem**: Lua scaling formulas (`GetSkillLevelData`, `Line`, `Conic`, `Extrac`, `Link`) executed at runtime introduce CPU spikes, floating-point rounding discrepancies, and garbage collection pressure inside the single-threaded 18-Hz zone loop.
- **Selected Solution**:
  - During offline export/compilation, every skill parameter curve is pre-evaluated across levels 1 to 64 using exact IEEE 754 arithmetic with `Math.Floor`.
  - The resulting `valuesByLevel` array is stored in the catalog, enabling instant $O(1)$ indexed array lookups during live ticks with zero allocations.

---

## 2. 4-Layer Ground-Truth Parity Verification Methodology

The offline migration tool implements `MigrationParityVerifier.cs`, validating four strict layers:
1. **Layer 1: File Inventory Coverage**: Ensures every legacy file in `jx-linux-bin/server1/` is accounted for in the migration ledger (`Unaccounted == 0`).
2. **Layer 2: 1:1 Physical Record Parity**: Confirms exact physical row and record counts across all domains (maps, NPCs, skills, missiles, items, affixes, loot tables, shops).
3. **Layer 3: Bitwise & Cell-by-Cell Reconstruction**: Verifies that decoded obstacle matrices match `Region_S.dat` byte-for-byte, portal `triggerCells` match trap definitions, and skill curve tables produce identical outputs.
4. **Layer 4: Schema & Referential Integrity**: Executes JSON Schema Draft 2020-12 validation across all files in `data/`, ensuring 100% valid cross-catalog references.
