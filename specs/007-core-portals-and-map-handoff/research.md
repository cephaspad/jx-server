# Research & Design Rationale: Boundary Portals & Map Handoff

**Feature Branch**: `007-core-portals-and-map-handoff` | **Date**: 2026-11-11

---

## 1. Binary Trap Data & `g_FileName2Id` Hashing

### 1.1 `TRAP_SECTION` in `Region_S.dat`
Section 1 of each region's binary data defines cell trap triggers:
- Header: 12-byte `KTrapFileHead` (`uint dwVersion`, `uint dwTrapCount`, `uint dwReserved`).
- Records: 8-byte `KSPTrap` structures representing horizontal runs of cells:
  - `+0x00` (`ushort`): `uTrapId` (mapped to trap script or template)
  - `+0x02` (`byte`): `btCellY` (cell row `0..31`)
  - `+0x03` (`byte`): `btCellXStart` (starting cell column `0..15`)
  - `+0x04` (`byte`): `btCellXEnd` (ending cell column `0..15`, inclusive)
  - `+0x05` (`byte[3]`): `reserved`

### 1.2 `g_FileName2Id` Script Path Hashing
Trap scripts under `script/**/trap/*.lua` are referenced by their 32-bit path hash computed via Kingsoft's canonical `g_FileName2Id` algorithm (`PakFilePathHasher.ComputeHash`). The path is normalized to lowercase with backslashes (`\`) before computing the CRC/polynomial hash.

---

## 2. Edge-Triggered Anti-Loop Trap Protection

If a trap triggered continuously every tick a player stood on a cell, a character stepping onto a portal would trigger dozens of transitions per second, corrupting state.

### Anti-Loop Algorithm
Each player session tracks `LastTriggeredTrapId` (initialized to `0`):
```csharp
uint currentTrapId = region.GetTrapIdAt(cellX, cellY);
if (currentTrapId == 0)
{
    // Stepped onto a non-trap cell; re-arm trigger for future traps
    player.LastTriggeredTrapId = 0;
    return;
}

if (currentTrapId == player.LastTriggeredTrapId)
{
    // Still standing on the same trap cell run; suppress duplicate trigger
    return;
}

// First time stepping onto this trap ID: record and trigger action!
player.LastTriggeredTrapId = currentTrapId;
ExecuteTrapAction(currentTrapId, player);
```

---

## 3. Town-Gate FightMode Toggles

Town gates delineate safe zones from combat zones:
- In safe towns: `FightMode = 0` (skills cannot be cast, PK is prohibited).
- In fields: `FightMode = 1` (skills and combat enabled).

### Gate Action (`PortalTrapActionKind.ToggleFightStateAndSetPos`)
When crossing a gate threshold:
1. `PlayerZoneState.FightMode` is toggled: `player.FightMode = (byte)(player.FightMode == 0 ? 1 : 0)`.
2. Coordinates are nudged forward across the threshold to prevent getting stuck in the doorway:
   `player.MpsX = targetCellX * 32; player.MpsY = targetCellY * 32;`
3. Authoritative coordinates and posture are synchronized to the player via `0x4E` (`s2c_syncnpcminplayer`).

---

## 4. In-Memory Same-Node Transitions vs. Cross-Node Redirect

### 4.1 Same-Node Map Handoff (`NewWorld`)
When the target map is active on the same world server:
- `IZoneClusterRouter.TryTransferPlayerAcrossMaps` detaches the player from the source worker's spatial grid and posts `AttachTransferredPlayerZoneCommand` to the destination worker.
- The destination worker transmits:
  1. `s2c_syncworld` (`0x49`, 34B): `SubWorldID = targetMapId`, `Region = (TargetRegionY << 16) | TargetRegionX`.
  2. `s2c_syncnpcminplayer` (`0x4E`, 13B): arrival sub-cell and cell offsets, `Doing = Stand (1)`.
  3. `s2c_syncnpc` (`0x4C`) for all entities in the target 3x3 region neighborhood.
- **CRITICAL PROTOCOL INVARIANT**:
  - The TCP connection remains active.
  - The rolling stream cipher (`KSG_DecodeEncode`) is **NOT** reset.
  - The server does **NOT** send `0x43` (`s2c_syncend`).

### 4.2 Cross-Node Redirect (`0x35` `s2c_notifyplayerexchange`)
When the target map is hosted on an external server node:
- The server generates a 16-byte single-use `Guid` handoff ticket.
- Outbound sink dispatches `s2c_notifyplayerexchange` (`0x35`, 25B).
- **Endianness Requirement**: The destination port (`6666 = 0x1A0A`) MUST be serialized as plain little-endian integer bytes, NOT converted via `htons`. The legacy client reads this port directly into a standard 16-bit integer.
