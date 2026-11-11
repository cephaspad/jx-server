# Quickstart: Spatial 3x3 Region AOI Visibility & Spawns

**Feature Branch**: `005-core-spatial-aoi-and-spawns` | **Date**: 2026-11-11

---

## 1. Build Verification

Build the entire solution with zero warnings:

```powershell
dotnet build "d:\Projects\jx-server\JXServer.slnx" --nologo -warnaserror
```

**Expected Result**: Build succeeds with `0 Warning(s)`, `0 Error(s)`.

---

## 2. Unit & Benchmark Verification

### Wire Packet Serialization Round-Trip (`0x48`, `0x49`, `0x4C`, `0x4D`, `0x4E`)
```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.Protocol.Tests\JXServer.Protocol.Tests.csproj" --filter "FullyQualifiedName~AoiEntitySyncPacketTests" --nologo
```

### Spatial Grid 3x3 AOI & Set-Difference Logic
```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.ZoneEngine.Tests\JXServer.ZoneEngine.Tests.csproj" --filter "FullyQualifiedName~SpatialRegionGridTests" --nologo
```

### High-Density AOI Boundary Crossing Benchmark
```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.ZoneEngine.Tests\JXServer.ZoneEngine.Tests.csproj" --filter "FullyQualifiedName~HighDensityAoiBenchmarkTests" --nologo
```

### Static NPC Asset Loading & Name Resolution
```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.ZoneEngine.Tests\JXServer.ZoneEngine.Tests.csproj" --filter "FullyQualifiedName~NpcAssetLoadingTests" --nologo
```

---

## 3. End-to-End Headless Multi-Client Verification

```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.Integration.Tests\JXServer.Integration.Tests.csproj" --filter "FullyQualifiedName~SpatialAoiIntegrationTests" --nologo
```

**What this validates**:
1. Two simulated players complete world entry in Ba Lăng Huyện (Map 53).
2. Both receive full appearance synchronization (`0x4C`) for each other and all surrounding static town NPCs.
3. On-demand entity queries (`0x48`, `0x49`) return valid `0x4C` snapshots within 1 tick.
