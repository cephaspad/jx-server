# Data Model: Boundary Portals & Map Handoff

**Feature Branch**: `007-core-portals-and-map-handoff` | **Date**: 2026-11-11

---

## 1. Domain Types & Enums

### 1.1 `PortalTrapActionKind` (`JXServer.Primitives.WorldZones`)

```csharp
namespace JXServer.Primitives.WorldZones;

public enum PortalTrapActionKind : byte
{
    ToggleFightStateAndSetPos = 1,
    ChangeMapPortal = 2,
}
```

### 1.2 `PortalTrapBindingTemplate` (`JXServer.GameAssets.WorldMaps`)
Immutable representation of a configured portal destination and action ruleset.

| Property | Type | Description |
| :--- | :--- | :--- |
| `TrapId` | `uint` | Numeric trap identifier or `g_FileName2Id` path hash |
| `Action` | `PortalTrapActionKind` | Action kind (`ToggleFightStateAndSetPos` or `ChangeMapPortal`) |
| `TargetMapId` | `ushort` | Target destination map ID |
| `TargetCellX` / `TargetCellY` | `int` | Destination arrival cell coordinates |
| `TargetFightMode` | `byte` | Target combat state (`0` = Safe, `1` = Combat) |
| `MinLevel` | `short` | Minimum level required to enter destination |
| `BounceBackCellX` / `Y` | `int` | Safe return coordinates if under-leveled |
| `DisplayName` | `string` | Display name of the portal destination |

### 1.3 `RegionTrapGrid` (`JXServer.GameAssets.WorldMaps`)
Represents an immutable 16x32 cell trap script hash matrix for a map region.

| Property | Type | Description |
| :--- | :--- | :--- |
| `RegionX` / `RegionY` | `int` | Region grid coordinates |
| `CellTrapIds` | `ReadOnlyMemory<uint>` | 512 cell trap hashes (`cx * 32 + cy`) |
| `GetTrapId(int cellX, int cellY)` | `uint` | Returns 32-bit `uTrapId` (`g_FileName2Id` hash) or 0 |

---

## 2. In-Memory Commands & Persistence

### 2.1 `AttachTransferredPlayerZoneCommand` (`JXServer.ZoneEngine.PlayerSessions`)

```csharp
public sealed record AttachTransferredPlayerZoneCommand(
    PlayerZoneState PlayerState,
    int ArrivalMpsX,
    int ArrivalMpsY,
    byte ArrivalFightMode) : IZoneCommand;
```

### 2.2 `UpdateCharacterWorldSnapshotDto` (`JXServer.Contracts.Characters`)

```csharp
public sealed record UpdateCharacterWorldSnapshotDto(
    Guid CharacterId,
    long ExpectedLeaseEpoch,
    ushort MapId,
    int MapMpsX,
    int MapMpsY,
    int CurrentLife,
    int CurrentMana,
    ushort CurrentStamina,
    byte FightMode = 0);
```

---

## 3. Wire Packet Structures (`JXServer.Adapter.LegacyJX1.Packets`)

| Struct | Opcode | Size | Description |
| :--- | :--- | :--- | :--- |
| `SyncWorldS2CPacket` | `0x49` | 34B | Synchronizes destination subworld ID, packed region, and map parameters |
| `NotifyPlayerExchangeS2CPacket` | `0x35` | 25B | Cross-node redirect: single-use handoff ticket GUID, IP, plain little-endian port |
| `SyncNpcMinPlayerS2CPacket` | `0x4E` | 13B | Self-position sync upon arrival: sub-cell offsets, region cell coordinates, posture |
