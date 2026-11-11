# Quickstart: Movement, Posture & Obstacle Validation

**Feature Branch**: `006-core-movement-and-obstacles` | **Date**: 2026-11-11

---

## 1. Build Verification

Build the entire solution with zero warnings:

```powershell
dotnet build "d:\Projects\jx-server\JXServer.slnx" --nologo -warnaserror
```

**Expected Result**: Build succeeds with `0 Warning(s)`, `0 Error(s)`.

---

## 2. Unit & Simulation Verification

### Wire Packet Serialization & Checksum Verification
```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.Protocol.Tests\JXServer.Protocol.Tests.csproj" --filter "FullyQualifiedName~MovementAndPosturePacketTests" --nologo
```

**What this validates**:
1. Little-Endian binary packing for `0x4B`, `0x4C`, `0x50`, `0x51`, `0x4E`, `0x54`, `0x71`, `0x53`.
2. Validates asymmetric field order between `0x50` (`NpcId, DestMpsY, DestMpsX`) and `0x51` (`DestMpsY, NpcId, DestMpsX`).
3. Validates `MovementAntiCheatConsts.IsValidCheckValue` with valid and tampered checksums.

### Deterministic 18-Hz Movement Simulation & Obstacle Clamping
```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.ZoneEngine.Tests\JXServer.ZoneEngine.Tests.csproj" --filter "FullyQualifiedName~MovementSimulationTests" --nologo
```

**What this validates**:
1. Accurate step progression at 10 Mps/tick (walk) and 20 Mps/tick (run).
2. Obstacle collision clamping at `Obstacle_Full` cell boundaries without wall clipping.
3. Sitting vital regeneration rate acceleration and instant cancellation upon movement.

---

## 3. End-to-End Headless Multi-Client Verification

```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.Integration.Tests\JXServer.Integration.Tests.csproj" --filter "FullyQualifiedName~MovementIntegrationTests" --nologo
```

**What this validates**:
1. Client sends walk/run commands over live TCP connection.
2. Server validates anti-cheat tokens, broadcasts trajectories to observers, and authoritatively synchronizes final coordinates.
