# Quickstart: Boundary Portals & Map Handoff

**Feature Branch**: `007-core-portals-and-map-handoff` | **Date**: 2026-11-11

---

## 1. Build Verification

Build the entire solution with zero warnings:

```powershell
dotnet build "d:\Projects\jx-server\JXServer.slnx" --nologo -warnaserror
```

**Expected Result**: Build succeeds with `0 Warning(s)`, `0 Error(s)`.

---

## 2. Unit & Transition Verification

### Trap Catalog, Anti-Loop & Same-Node Map Handoff Tests
```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.ZoneEngine.Tests\JXServer.ZoneEngine.Tests.csproj" --filter "FullyQualifiedName~TrapAndMapTransitionTests" --nologo
```

**What this validates**:
1. Region cell trap parsing and `g_FileName2Id` path hash mapping in `TrapScriptCatalog`.
2. Edge-triggered `LastTriggeredTrapId` suppression while remaining stationary on a trap cell.
3. Town-gate `FightMode` toggle (`0 <-> 1`) and position nudging.
4. In-memory `IZoneClusterRouter.TryTransferPlayerAcrossMaps` state transfer between shards.
5. Level-gate rejection and bounce-back coordinate safety.

---

## 3. End-to-End Headless Multi-Map Verification

```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.Integration.Tests\JXServer.Integration.Tests.csproj" --filter "FullyQualifiedName~MapTransitionIntegrationTests" --nologo
```

**What this validates**:
1. Player walks onto a portal trap leading from Ba Lăng Huyện (Map 53) to an adjacent map.
2. Server transitions player over existing TCP socket without cipher reset, emitting `0x49` and `0x4E`.
3. Player continues moving and chatting on the destination map seamlessly.
