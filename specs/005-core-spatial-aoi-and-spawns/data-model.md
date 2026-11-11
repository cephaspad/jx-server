# Data Model: Spatial 3x3 Region AOI Visibility & Spawns

**Feature Branch**: `005-core-spatial-aoi-and-spawns` | **Date**: 2026-11-11

---

## 1. Domain Entities & Spatial Structures

### 1.1 `SpatialRegionSlot` (`JXServer.ZoneEngine.SpatialGrid`)
Represents an individual 512 x 1024 pixel partition of an active map zone.

| Property | Type | Description |
| :--- | :--- | :--- |
| `RegionX` | `int` | Horizontal region index across the map grid |
| `RegionY` | `int` | Vertical region index across the map grid |
| `Players` | `List<PlayerZoneState>` | Active players currently located within this region |
| `Npcs` | `List<NpcZoneState>` | Static NPCs and monsters currently located within this region |

### 1.2 `SpatialRegionGrid` (`JXServer.ZoneEngine.SpatialGrid`)
Encapsulates the 2D array of `SpatialRegionSlot` instances bounded by the map's `.wor` rectangle (`left, top, right, bottom`).

| Member | Signature | Purpose |
| :--- | :--- | :--- |
| `Left` / `Top` / `Right` / `Bottom` | `int` | Map region bounding coordinates |
| `GetSlot(int rx, int ry)` | `SpatialRegionSlot?` | Retrieves slot by region coordinate |
| `GetSurroundingRegions(int rx, int ry)` | `IReadOnlyList<SpatialRegionSlot>` | Returns the up to 9 regions composing the 3x3 AOI |
| `ComputeEnteringRegions(int oldRx, int oldRy, int newRx, int newRy)` | `IReadOnlyList<SpatialRegionSlot>` | Computes `New3x3 \ Old3x3` set difference |
| `ComputeLeavingRegions(int oldRx, int oldRy, int newRx, int newRy)` | `IReadOnlyList<SpatialRegionSlot>` | Computes `Old3x3 \ New3x3` set difference |
| `RegisterPlayer` / `UnregisterPlayer` | `void` | Adds/removes player to/from slot |
| `RegisterNpc` / `UnregisterNpc` | `void` | Adds/removes NPC to/from slot |

### 1.3 `NpcZoneState` (`JXServer.ZoneEngine.NpcEntities`)
In-memory runtime state for a live non-player character or monster.

| Field | Type | Description |
| :--- | :--- | :--- |
| `NpcId` | `uint` | Runtime entity identifier (`1..9999`) |
| `TemplateId` | `int` | Template identifier from `npcs.txt` |
| `Name` | `string` | UTF-8 localized display name |
| `Level` | `short` | Combat level (`1..200`) |
| `Kind` | `EntityKind` | Dialog NPC (`3`), Monster (`0`), Partner (`2`) |
| `Camp` | `EntityCamp` | Current camp (`0..6`) |
| `Series` | `ElementSeries` | Five-elements series (`0..4`) |
| `IsSpecialNpc` | `bool` | True if golden/named boss |
| `MapId` | `ushort` | Host map identifier |
| `MpsX` / `MpsY` | `int` | World pixel coordinates |
| `RegionX` / `RegionY` | `int` | Current region coordinates |
| `Doing` | `EntityDoingPosture` | Current posture (`Stand = 1`) |
| `Dir` | `byte` | 64-direction facing angle (`0..63`) |
| `WalkSpeed` / `RunSpeed` | `int` | Movement speeds (Mps/tick) |
| `CurLife` / `MaxLife` | `uint` | Health vitals |
| `ScriptPath` | `string` | Dialog script path binding |
| `PackedNpcSettingIdx` | `uint` | `((uint)(ushort)Level << 16) \| (uint)(ushort)TemplateId` |

### 1.4 `EntityFullSyncSnapshot` (`JXServer.ZoneEngine.PlayerSessions`)
Immutable readonly record struct encapsulating all data needed for `0x4C` `s2c_syncnpc`.

| Property | Type | Description |
| :--- | :--- | :--- |
| `NpcId` | `uint` | Runtime entity identifier |
| `MpsX` / `MpsY` | `int` | World pixel coordinates |
| `Doing` | `EntityDoingPosture` | Posture (`Stand = 1`, etc.) |
| `LifePer` | `byte` | Health percentage `0..100` |
| `IsGold` | `bool` | Boss flag |
| `Series` | `ElementSeries` | Five elements series |
| `Camp` | `EntityCamp` | Camp |
| `NpcSettingIdx` | `uint` | Packed level & template ID |
| `Dir` | `byte` | 64-direction angle |
| `Kind` | `EntityKind` | Player or NPC kind |
| `Name` | `string` | UTF-8 name (encoded to TCVN3 at wire) |

---

## 2. Shared Enums (`JXServer.Primitives.WorldZones`)

```csharp
public enum EntityDoingPosture : byte
{
    Stand = 1,
    Walk = 2,
    Run = 3,
    Sit = 4,
    Jump = 5,
    Attack = 6,
    Skill = 7,
    Death = 11,
}

public enum EntityCamp : byte
{
    Begin = 0,
    Justice = 1,
    Evil = 2,
    Balance = 3,
    Free = 4,
    Animal = 5,
    Event = 6,
}
```
