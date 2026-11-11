# Contract 01: JXData Schema Contracts & Binary Serialization Format

**Feature**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)  
**Standard**: [docs/data-architecture-standard.md](../../../docs/data-architecture-standard.md)  

---

## 1. JSON Schema (Draft 2020-12) Architecture

All canonical game data documents under `data/` MUST adhere to schema contracts defined in `schemas/`:

| Schema File | Target Domain | Core Validations |
| :--- | :--- | :--- |
| `schemas/map.schema.json` | `data/maps/*.map.json` | Validates Map ID (`1..999`), region bounds, base64 RLE obstacle matrix, portals, traps, spawn groups, and map objects. |
| `schemas/npc-catalog.schema.json` | `data/npcs/*.npcs.json` | Validates template ID (`0..2539`), entity kind, walk/run speeds, combat scaling formulas, multi-skill slots, and AI configurations. |
| `schemas/player-template.schema.json` | `data/players/*.players.json` | Validates 10 base archetypes (5 series x 2 genders), series growth tables, 5-element matrix, factions, and starter village spawn coordinates. |
| `schemas/skill-catalog.schema.json` | `data/skills/*.skills.json` | Validates skill styles, LR skill info, level scaling parameters (`param0..param2`), 64-dir missiles, multi-stage child skills, and event triggers. |
| `schemas/item-catalog.schema.json` | `data/items/*.items.json` | Validates row ID, quality (`0..4`), genre, detail/particular types, equip part weights, magic affix pools, and fixed set affixes. |
| `schemas/loot-table.schema.json` | `data/loot/*.loot.json` | Validates rand range (`1000000`), drop percentages, gold scales, and weighted loot item pools. |
| `schemas/shop-catalog.schema.json` | `data/shops/*.shops.json` | Validates 1-based shop ID, currency type, sell-back divisor (`BUY_SELL_SCALE = 4`), and good item lists. |

---

## 2. Obstacle Matrix RLE Binary Layout (`ObstacleRleCodec`)

Within `*.map.json`, each region's `obstacleRleBase64` field encodes `16 x 32 = 512` obstacle integers in `(cellX * 32 + cellY)` order.
- Each run is encoded as a **6-byte Little-Endian pair**:
  - `+0x00` (`2B` uint16): `runLength` (`1..512`)
  - `+0x02` (`4B` int32): `cellValue` (`ObstacleKind` in bits `0..3` + `4x4` `SubCellMask` in bits `4..19`)
- The sequence of 6-byte pairs is base64-encoded.
- Regions where all 512 cells equal `0` (`Obstacle_NULL`) are omitted from the document.

---

## 3. Pre-Evaluated Skill Curve Array Contract

To achieve deterministic $O(1)$ evaluation during live ticks, all skill parameter curves emit a pre-calculated integer array:
- `valuesByLevel` (`int[]`): Minimum length 64 (levels 1 through 64).
- Value at index `L-1` equals $\lfloor f(L) \rfloor$ computed using IEEE 754 double precision matching historical C++ Kingsoft formulas:
  - Linear: $V = \lfloor P_{min} + \frac{(L - 1) \cdot (P_{max} - P_{min})}{L_{max} - 1} \rfloor$
  - Conic: $V = \lfloor P_{start} + (P_{end} - P_{start}) \cdot \sqrt{\frac{L - 1}{L_{max} - 1}} \rfloor$
  - Extrac: Quadratic curve interpolation.
  - Link: Piecewise linear interpolation across control points.

---

## 4. Binary Snapshot Package (`jxdata.pack`) Contract

- **Header / Container**: Serialized using `MemoryPack` (`v1.21.4`) with format version `"1.0.0"`.
- **Integrity**: Accompanied by canonical snapshot loader verification and `artifacts/migration-parity-report.json`.
- **Deserialization**: Handled via `GameAssetsSnapshotLoader` directly into pinned memory buffers, achieving sub-100ms startup times.
