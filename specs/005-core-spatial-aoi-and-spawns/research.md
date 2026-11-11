# Research & Design Rationale: Spatial 3x3 Region AOI Visibility & Spawns

**Feature Branch**: `005-core-spatial-aoi-and-spawns` | **Date**: 2026-11-11

---

## 1. Spatial Coordinate Hierarchy

The legacy Kingsoft Swordsman Online engine uses a multi-tier coordinate hierarchy:

| Coordinate Tier | Units | Dimensions | Conversion Formula |
| :--- | :--- | :--- | :--- |
| **Mps (Pixels)** | 1 pixel | Sub-cell continuous | Authoritative world position `(MpsX, MpsY)` |
| **Cell** | 32 x 32 Mps | Discrete pathfinding & obstacle unit | `CellX = MpsX / 32`, `CellY = MpsY / 32` |
| **Region** | 16 x 32 Cells | 512 x 1024 Mps | `RegionX = (MpsX / 32) / 16`, `RegionY = (MpsY / 32) / 32` |

### Coordinate Decomposition within a Region
When broadcasting self-position sync (`0x4E` `s2c_syncnpcminplayer`), the packet carries sub-cell fractional offsets and intra-region cell coordinates:
```csharp
byte cellX = (byte)((mpsX / 32) % 16);
byte cellY = (byte)((mpsY / 32) % 32);
ushort offX = (ushort)((mpsX % 32) << 10);
ushort offY = (ushort)((mpsY % 32) << 10);
```

---

## 2. 3x3 Region Neighborhood & Set-Difference AOI

### Neighborhood Definition
For an entity at `(RegionX, RegionY)`, its Area of Interest (AOI) encompasses up to 9 regions:
$$\text{AOI}(R_x, R_y) = \{ (x, y) \mid x \in [R_x - 1, R_x + 1], y \in [R_y - 1, R_y + 1], \text{bounded by map limits} \}$$

### Set-Difference Boundary Crossing
When an entity moves from $R_{\text{old}}$ to $R_{\text{new}}$:
- **Newly Visible Regions**: $\Delta_{\text{enter}} = \text{AOI}(R_{\text{new}}) \setminus \text{AOI}(R_{\text{old}})$.
  - For all entities in $\Delta_{\text{enter}}$, send full sync `0x4C` (`s2c_syncnpc`) to the moving player.
  - Broadcast the moving player's `0x4C` to all observers currently in $\Delta_{\text{enter}}$.
- **Departed Regions**: $\Delta_{\text{leave}} = \text{AOI}(R_{\text{old}}) \setminus \text{AOI}(R_{\text{new}})$.
  - Observers residing in $\Delta_{\text{leave}}$ stop receiving subsequent periodic `0x4D`/`0x50`/`0x51` broadcasts for the moving player.
- **Persisting Regions**: $\text{AOI}(R_{\text{new}}) \cap \text{AOI}(R_{\text{old}})$.
  - Normal periodic compact position synchronization (`0x4D`) continues uninterrupted without re-sending expensive full appearance packets.

---

## 3. Kingsoft Ground-Truth Formulas

### Packed NPC Setting Index (`NpcSettingIdx`)
In `0x4C` `s2c_syncnpc`:
- For static NPCs and monsters:
  $$\text{NpcSettingIdx} = ((\text{uint})\text{Level} \ll 16) \mid (\text{uint})(\text{ushort})\text{TemplateId}$$
- For players:
  $$\text{NpcSettingIdx} = 0$$

### Runtime Entity Identifier Ranges
- `1..9999`: Reserved for static town NPCs and spawned field monsters.
- `>= 10000`: Dynamic player sessions (`NpcId = (uint)SessionIndex + 10000`).

---

## 4. Static NPC & Monster Loading Pipeline

The static world population originates from multiple complementary sources:
1. **`settings/npcs.txt`**: Canonical NPC template table containing base attributes, default camps, series, speeds, and localized TCVN3 display names.
2. **`script/global/autoexec.lua`**: Script-driven town dialog NPC placements (`adddialognpc(...)`) with explicit coordinates and script binding.
3. **`Region_S.dat` Section 2 (`NPC_SECTION`)**: Binary map region records containing 12-byte `KNpcFileHead` header followed by variable-length `60 + nScriptNameLen` byte `KSPNpc` records.

### Name Resolution Precedence
1. Explicit name argument in `autoexec.lua`.
2. Localized Vietnamese display name from `settings/npcs.txt` (decoded from `TCVN3` to `UTF-8`).
3. Raw GBK string in `KSPNpc.szName` as fallback.
