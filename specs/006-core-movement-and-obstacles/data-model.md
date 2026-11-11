# Data Model: Movement, Posture & Obstacle Validation

**Feature Branch**: `006-core-movement-and-obstacles` | **Date**: 2026-11-11

---

## 1. Domain Types & Enums

### 1.1 `EntityDoingPosture` (`JXServer.Primitives.WorldZones`)

```csharp
namespace JXServer.Primitives.WorldZones;

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
```

### 1.2 `MovementAntiCheatConsts` (`JXServer.Primitives.WorldZones`)

```csharp
namespace JXServer.Primitives.WorldZones;

public static class MovementAntiCheatConsts
{
    public const uint WalkCheckKey = 0x24A1u; // 9377
    public const uint RunCheckKey = 0x1D78u;  // 7544
    public const uint SkillCheckKey = 0x3AB9u; // 15033

    public static bool IsValidCheckValue(uint checkValue, uint token1, uint token2, uint key)
    {
        if (checkValue == 0) return false;
        uint intermediate = checkValue ^ token1 ^ token2;
        return (intermediate % key) == 0;
    }
}
```

---

## 2. In-Memory Commands & Structures

### 2.1 Zone Movement Commands (`JXServer.ZoneEngine.PlayerSessions`)

```csharp
public sealed record MovePlayerZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    int TargetMpsX,
    int TargetMpsY,
    bool IsRunning,
    uint CheckValue) : IZoneCommand;

public sealed record JumpPlayerZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    int TargetMpsX,
    int TargetMpsY) : IZoneCommand;

public sealed record TogglePlayerSitZoneCommand(
    Guid CharacterId,
    long LeaseEpoch,
    bool IsSitting) : IZoneCommand;
```

### 2.2 `RegionObstacleGrid` (`JXServer.GameAssets.WorldMaps`)
Encapsulates terrain passability for a 16x32 cell region.

| Property | Type | Description |
| :--- | :--- | :--- |
| `RegionX` / `RegionY` | `int` | Region grid coordinates |
| `GetObstacle(int cellX, int cellY)` | `int` | Returns cell obstacle value (`0` = Passable, `1` = Blocked, `2` = Water) |
| `CellObstacles` | `ReadOnlyMemory<int>` | Read-only memory of 512 cell obstacle values (`cellX * 32 + cellY`) |

---

## 3. Wire Packet Structures (`JXServer.Adapter.LegacyJX1.Packets`)

| Struct | Opcode | Size | Description |
| :--- | :--- | :--- | :--- |
| `NpcWalkC2SPacket` | `0x4B` | 13B | `<BiiI`: TargetMpsX, TargetMpsY, CheckValue |
| `NpcRunC2SPacket` | `0x4C` | 13B | `<BiiI`: TargetMpsX, TargetMpsY, CheckValue |
| `NpcWalkS2CPacket` | `0x50` | 13B | `<BIii`: NpcId, DestMpsY, DestMpsX (Asymmetric) |
| `NpcRunS2CPacket` | `0x51` | 13B | `<BiIi`: DestMpsY, NpcId, DestMpsX (Asymmetric) |
| `NpcJumpC2SPacket` | `0x4E` | 13B | `<BiiI`: TargetMpsX, TargetMpsY, Reserved |
| `NpcJumpS2CPacket` | `0x54` | 13B | `<BiiI`: DestMpsY, DestMpsX, NpcId |
| `NpcSitC2SPacket` | `0x71` | 2B | `<BB`: SitFlag (`0` = Stand, `1` = Sit) |
| `NpcSitS2CPacket` | `0x53` | 5B | `<BI`: NpcId |
