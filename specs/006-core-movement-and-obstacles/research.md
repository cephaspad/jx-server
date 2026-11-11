# Research & Design Rationale: Movement, Posture & Obstacle Validation

**Feature Branch**: `006-core-movement-and-obstacles` | **Date**: 2026-11-11

---

## 1. Anti-Cheat Security Algorithm (`dwCheckValue`)

In the legacy client, movement commands include a 32-bit security checksum (`dwCheckValue`) to prevent packet forgery and speed modification.

### Key Table
- Walk Key: `0x24A1` (Decimal `9377`)
- Run Key: `0x1D78` (Decimal `7544`)
- Skill Key: `0x3AB9` (Decimal `15033`)

### Verification Formula
During the world entry handshake, `c2s_syncend` (`0x43`) supplies two 32-bit pseudo-random tokens (`token1`, `token2`). When a movement command arrives:
```csharp
public static bool IsValidCheckValue(uint checkValue, uint token1, uint token2, uint key)
{
    if (checkValue == 0) return false;
    uint intermediate = checkValue ^ token1 ^ token2;
    return (intermediate % key) == 0;
}
```
If the check fails, the movement command is strictly dropped, and a security warning is logged.

---

## 2. Asymmetric S2C Wire Packet Layouts

A crucial discovery from decompiling the original Linux server binary (`server1`) and client code (`game.exe`):
The field order between walking and running is **asymmetric**!

- `s2c_npcwalk` (`0x50` / `80`):
  Structure: `<BIii` (Little-Endian)
  Order: `[0x00] Opcode 0x50`, `[0x01] uint NpcId`, `[0x05] int DestMpsY`, `[0x09] int DestMpsX`.
- `s2c_npcrun` (`0x51` / `81`):
  Structure: `<BiIi` (Little-Endian)
  Order: `[0x00] Opcode 0x51`, `[0x01] int DestMpsY`, `[0x05] uint NpcId`, `[0x09] int DestMpsX`.

Preserving this exact asymmetric ordering is mandatory for legacy client compatibility; swapping them causes `game.exe` to desynchronize or walk in inverted directions.

---

## 3. Frame Progression & Obstacle Clamping Math

### Frame-Accurate Speed Constants
- 18 simulation ticks per second (~55.5ms per tick).
- Walk speed: `10` Mps/tick (~180 Mps / second).
- Run speed: `20` Mps/tick (~360 Mps / second).

### Step Progression Algorithm
Given current coordinates $(X_0, Y_0)$ and target $(X_t, Y_t)$:
1. $\Delta X = X_t - X_0$, $\Delta Y = Y_t - Y_0$.
2. Distance $D = \sqrt{\Delta X^2 + \Delta Y^2}$.
3. If $D \le \text{Speed}$, set position to $(X_t, Y_t)$, transition posture to `Stand (1)`.
4. Otherwise, calculate candidate step:
   $$X_{\text{cand}} = X_0 + \operatorname{round}\left(\frac{\Delta X \cdot \text{Speed}}{D}\right)$$
   $$Y_{\text{cand}} = Y_0 + \operatorname{round}\left(\frac{\Delta Y \cdot \text{Speed}}{D}\right)$$
5. Check candidate cell $(X_{\text{cand}} / 32, Y_{\text{cand}} / 32)$ in `RegionObstacleGrid`:
   - If `Obstacle_Full == 1`: stop at $(X_0, Y_0)$, set `Doing = Stand (1)`, emit `s2c_syncnpcminplayer` (`0x4E`).
   - Otherwise: advance to $(X_{\text{cand}}, Y_{\text{cand}})$.
6. If the new position crosses a region boundary, trigger `SpatialRegionGrid.UpdateEntityRegion`.

---

## 4. Posture & Sitting Vital Regeneration

- `c2s_npcsit` (`0x71`) carries a 1-byte `SitFlag` (`0` = Stand, `1` = Sit).
- When `SitFlag != 0`, character enters `EntityDoingPosture.Sit (4)` and broadcasts `0x53` (`s2c_npcsit`) to the 3x3 AOI.
- While seated, every 18 frames (1 second), the character regenerates:
  $$\Delta \text{Life} = \left\lfloor \frac{\text{MaxLife}}{20} \right\rfloor + 5$$
  $$\Delta \text{Mana} = \left\lfloor \frac{\text{MaxMana}}{20} \right\rfloor + 5$$
- Any incoming `MovePlayerZoneCommand` or `JumpPlayerZoneCommand` immediately cancels sit posture before processing movement.
