# Quickstart: Spatial Inventory & Equipment Grid

**Feature Branch**: `008-core-inventory-and-equipment` | **Date**: 2026-11-11

---

## 1. Build Verification

Build the entire solution with zero warnings:

```powershell
dotnet build "d:\Projects\jx-server\JXServer.slnx" --nologo -warnaserror
```

**Expected Result**: Build succeeds with `0 Warning(s)`, `0 Error(s)`.

---

## 2. Unit & Container Simulation Verification

### Wire Packet Serialization Round-Trip
```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.Protocol.Tests\JXServer.Protocol.Tests.csproj" --filter "FullyQualifiedName~InventoryAndEquipmentPacketTests" --nologo
```

**What this validates**:
1. Little-Endian binary packing for `0x5B`, `0x61`, `0x8E`, `0x85`, `0x59`, `0x5F`, `0x60`, `0x68`.
2. 64-byte `SViewItemInfo` layout inside `0x5F` (`SyncItemS2CPacket`).

### Spatial 2D Collision, Five-Elements Affix Ring & Stack Splitting
```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.ZoneEngine.Tests\JXServer.ZoneEngine.Tests.csproj" --filter "FullyQualifiedName~PlayerInventoryAndEquipmentTests" --nologo
```

**What this validates**:
1. 2D rectangular footprint collision in 6x10 bag matrix (`pos_equiproom`).
2. Atomic move, swap, and stack splitting into cursor hand (`pos_hand`).
3. Five-Elements generation cycle (Metal $\rightarrow$ Water $\rightarrow$ Wood $\rightarrow$ Fire $\rightarrow$ Earth $\rightarrow$ Metal) activating dark affixes.
4. Cursor-hand disconnect safety: automatic return of held item to bag upon detach.

### Domain Entity `CharacterItem` Durability & Enchasing
```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.ZoneEngine.Tests\JXServer.ZoneEngine.Tests.csproj" --filter "FullyQualifiedName~CharacterItemUnitTests" --nologo
```

---

## 3. End-to-End Headless Multi-Client Verification

```powershell
dotnet test "d:\Projects\jx-server\tests\JXServer.Integration.Tests\JXServer.Integration.Tests.csproj" --filter "FullyQualifiedName~InventoryAndLootIntegrationTests" --nologo
```

**What this validates**:
1. Connecting client receives complete inventory synchronization (`0x5F`) and gold sync (`0x68`) on world entry.
2. Client moves items, splits stacks, consumes medicine (`0x59`), and receives instant server confirmations.
