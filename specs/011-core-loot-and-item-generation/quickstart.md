# Quickstart: Core Loot, Item Generation & Ground Objects

**Feature Branch**: `011-core-loot-and-item-generation`  
**Date**: 2026-11-11  

---

## 1. Prerequisites

- .NET 10 SDK (`net10.0`)
- Working directory: `d:\Projects\jx-server`

---

## 2. Build Verification

Build the entire solution with zero warnings:

```powershell
dotnet build JXServer.slnx -warnaserror
```

---

## 3. Running Unit and Simulation Tests

### 3.1 Ground Object Wire Packet Serialization Tests
Verifies byte layouts of `0x64`, `0x67`, `0x4A`, `0x72`, `0x5A`, `0x5E`, and `0x83`:

```powershell
dotnet test tests/JXServer.Protocol.Tests --filter "FullyQualifiedName~GroundObjectPacketTests" --no-restore
```

### 3.2 6-Affix Blue Item Generator & Loot Table Tests
Verifies version 4 procedural equipment generation, magic affix rolling, luck weighting, and drop table parsing:

```powershell
dotnet test tests/JXServer.ZoneEngine.Tests --filter "FullyQualifiedName~BlueItemGeneratorAndLootTests" --no-restore
```

### 3.3 End-to-End Inventory & Loot Integration Tests
Verifies monster kill drop spawning, killer protection timer, ground object AOI broadcast, and item pickup:

```powershell
dotnet test tests/JXServer.Integration.Tests --filter "FullyQualifiedName~InventoryAndLootIntegrationTests" --no-restore
```

### 3.4 High-Density Combat & Loot Race Stress Tests
Verifies atomic pickup race conditions (zero duplication under concurrent bot farming):

```powershell
dotnet test tests/JXServer.Integration.Tests --filter "FullyQualifiedName~HighDensityCombatAndLootStressTests" --no-restore
```

---

## 4. Key Verification Checklist

- [x] ItemGenerator produces authentic 6-affix Blue equipment matching version 4 rules.
- [x] Ground objects scatter on walkable terrain cells without colliding with impassable barriers.
- [x] GroundObjectZoneTickSystem enforces killer protection window before public access.
- [x] Unclaimed ground objects despawn upon lifetime expiration and emit `0x67` (reason 0).
- [x] 100% of concurrent pickup races resolve to exactly one claimant with zero item duplication.
