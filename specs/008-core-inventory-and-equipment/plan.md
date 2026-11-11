# Implementation Plan: Spatial Inventory & Equipment Grid

**Branch**: `008-core-inventory-and-equipment` | **Date**: 2026-11-11 | **Spec**: [spec.md](spec.md)

---

## 1. Summary

Deliver the authoritative spatial container grids, 2D footprint collision matrix, 15-part equipment paper doll, Five-Elements hidden affix activation chain, stackable item splitting, medicine consumption, and cursor disconnect safety for JXServer (.NET 10, C# 14) in strict compliance with **Constitution v1.2.0** and **`JXData Standard v1`**:

1. **6x10 Spatial Bag Grid & Collision Matrix (`JXServer.ZoneEngine`)**: Maintain 60-cell 2D grid in `PlayerInventoryGrid`, validating item `width x height` rectangular footprints across `pos_hand` (`1`), `pos_equip` (`2`), `pos_equiproom` (`3`), and `pos_immediacy` (`7`). Process item moves via `InventoryLootCommandHandler`, emitting move confirmation (`0x61`) and auto-move unlock (`0x8E`).
2. **15-Part Equipment & Five-Elements Affix Chain**: Validate item equip requirements across 15 slots (Head, Body, Belt, Weapon, Foot, Cuff, Amulet, Ring1, Ring2, Pendant, Horse, Mask, Mantle, Signet, Badge), recalculating character combat attributes and evaluating the Five-Elements generation cycle (Metal $\rightarrow$ Water $\rightarrow$ Wood $\rightarrow$ Fire $\rightarrow$ Earth $\rightarrow$ Metal) to activate hidden dark affixes (1, 3, 5).
3. **Stack Splitting & Currency Sync**: Process `c2s_playerdivideitem` (`0x85`), splitting stacks into `pos_hand` and synchronizing gold balances via `0x68` (`s2c_syncmoney`, 13B).
4. **Medicine Consumption & Disconnect Safety**: Process `c2s_playereatitem` (`0x59`), applying gradual vital recovery and emitting `0x60` (`s2c_removeitem`). Guarantee zero item loss by returning items in `pos_hand` to `pos_equiproom` upon session disconnect before committing to persistence.

---

## 2. Technical Context

- **Language/Version**: C# 14 on .NET 10 (`net10.0`), centralized `Directory.Build.props` (`TreatWarningsAsErrors=true`, `AnalysisLevel=latest`).
- **Primary Dependencies**:
  - `Microsoft.Extensions.Hosting` / `DependencyInjection` / `Logging.Abstractions` (`10.0.0`)
  - `System.IO.Pipelines` (`10.0.0`)
- **Storage**:
  - `CharacterItem` persistence via EF Core 10, preserving `Id`, `IsBroken`, `EnchaseLevel`, durability, and 6-slot magic attributes under lease-epoch fencing.
  - Immutable item catalogs loaded from `data/items/*.json` into `IItemTemplateCatalog`.
- **Testing**:
  - `xunit.v3` across `JXServer.Protocol.Tests` (`InventoryAndEquipmentPacketTests`), `JXServer.ZoneEngine.Tests` (`PlayerInventoryAndEquipmentTests`, `CharacterItemUnitTests`), and `JXServer.Integration.Tests` (`InventoryAndLootIntegrationTests`).
- **Performance Goals**:
  - 2D footprint collision check executes in under 2 microseconds.
  - Zero heap allocations during item move/swap evaluation.

---

## 3. Constitution Check (v1.2.0)

### Core Principles (I–VI)

| Principle | Status | Verification Evidence |
| :--- | :---: | :--- |
| **I. Hybrid Distributed Control Plane & Sharded 18-Hz Zone Architecture** | **PASS** | Inventory modifications execute strictly within single-threaded zone worker; persistence is write-behind via `ICharacterAppService`. |
| **II. Multi-Protocol Transport Abstraction & Client Compatibility** | **PASS** | Legacy packet layouts (`0x5F`, `0x61`, `0x8E`, `0x60`, `0x68`) remain isolated in `JXServer.Adapter.LegacyJX1`. |
| **III. Zero-Hallucination Evidence & Ground-Truth Fidelity** | **PASS** | Exact 64-byte `SViewItemInfo` layout, 15 equipment slots, and Five-Elements generation ring match Kingsoft C++ sources. |
| **IV. Zero-Dupe Economy & Lease-Fenced Session Integrity** | **PASS** | Atomic container transactions prevent duplication; held cursor items return safely to bag upon disconnect. |
| **V. UTF-8 Domain Core, Dual-Tier Game Data Standard & Boundary-Only Transcoding** | **PASS** | Internal item names are UTF-8; boundary transcoding to TCVN3 occurs only during wire serialization. |
| **VI. Clean Modular Boundaries, ABP DDD Role Separation & C# 14 Scripting** | **PASS** | One type per file, sealed concrete classes, pluralized folders (`Items/`, `Characters/`). |

### Verification Gates (1–5)

| Gate | Status | Enforcement |
| :--- | :---: | :--- |
| **1. Ground-Truth Specification Gate** | **PASS** | Documented in `contracts/inventory-and-equipment-protocol.md`. |
| **2. Symmetric Protocol & Formula Unit Gate** | **PASS** | Verified in `InventoryAndEquipmentPacketTests` and `PlayerInventoryAndEquipmentTests`. |
| **3. Autonomous Swarm & Headless Integration Gate** | **PASS** | Verified in `InventoryAndLootIntegrationTests`. |
| **4. High-Density Stress & Fault Isolation Gate** | **PASS** | Stress tests rapid concurrent item moves and splits with zero duplication. |
| **5. Structural & Zero-Warning Build Gate** | **PASS** | 0 compiler warnings under `TreatWarningsAsErrors=true`. |

---

## 4. Project Structure

### Documentation

```text
specs/008-core-inventory-and-equipment/
├── spec.md                                          # WHAT & WHY, 16/16 requirements verified
├── plan.md                                          # Technical context & architecture
├── research.md                                      # Ground-truth analysis & formulas
├── data-model.md                                    # Container schemas and item slots
├── quickstart.md                                    # Build & verification instructions
├── checklists/
│   └── requirements.md                              # 16/16 verification checklist
└── contracts/
    ├── inventory-and-equipment-protocol.md          # Wire protocol contracts (0x5B, 0x61, 0x8E, 0x85, 0x59, 0x5F, 0x60, 0x68)
    └── inventory-ports-and-containers.md            # Internal container & catalog ports
```

### Source Code

```text
src/
├── JXServer.Primitives/
│   └── Items/
│       ├── ItemConsts.cs                            # EquipRoomWidth = 6, EquipRoomHeight = 10, MaxEquipParts = 15
│       ├── ItemContainerPlace.cs                    # Hand = 1, Equip = 2, EquipRoom = 3, Immediacy = 7
│       ├── EquipmentPartKind.cs                     # 15 equipment slots
│       └── ItemGenreKind.cs                         # Equipment, Medicine, TownPortal, Quest
│
├── JXServer.Domain/
│   └── Characters/
│       └── CharacterItem.cs                         # Durable aggregate with IsBroken & EnchaseLevel
│
├── JXServer.Contracts/
│   └── Characters/
│       └── CharacterItemDto.cs                      # Application DTO contract
│
├── JXServer.GameAssets/
│   └── Items/
│       ├── IItemTemplateCatalog.cs                  # Item catalog interface
│       ├── ItemTemplate.cs                          # Base item archetype
│       └── ItemTemplateCatalog.cs                   # JXData v1 item catalog implementation
│
├── JXServer.ZoneEngine/
│   ├── Items/
│   │   ├── DivideInventoryItemZoneCommand.cs        # Zone command for item split
│   │   ├── DropInventoryItemZoneCommand.cs          # Zone command for item drop
│   │   ├── InventoryLootCommandHandler.cs           # Handler for move, split, drop, use commands
│   │   ├── MoveInventoryItemZoneCommand.cs          # Zone command for item move/swap
│   │   ├── PlayerInventoryGrid.cs                   # 6x10 2D spatial collision grid & 15 equip slots
│   │   ├── PlayerItemSlot.cs                        # In-memory item instance state
│   │   └── UseInventoryItemZoneCommand.cs           # Zone command for consumable use
│   └── PlayerSessions/
│       ├── IInventoryOutboundSink.cs                # Outbound sink for item & money sync
│       └── PlayerItemSyncSnapshot.cs                # Unified item synchronization snapshot
│
└── JXServer.Adapter.LegacyJX1/
    ├── GameServer/
    │   ├── GameServerClientSession.InventorySink.cs # Emits 0x5F, 0x61, 0x8E, 0x60, 0x68
    │   ├── PlayerDivideItemPacketHandler.cs         # Handles 0x85 c2s_playerdivideitem
    │   ├── PlayerEatItemPacketHandler.cs            # Handles 0x59 c2s_playereatitem
    │   ├── PlayerMoveItemPacketHandler.cs           # Handles 0x5B c2s_playermoveitem
    │   ├── PlayerThrowAwayItemPacketHandler.cs      # Handles 0x5E c2s_playerthrowawayitem
    │   └── ThrowAwayAllMedicinePacketHandler.cs     # Handles 0x83 c2s_throwawayallmedicine
    └── Packets/
        ├── ItemAutoMoveS2CPacket.cs                 # 0x8E wire layout (1B)
        ├── PlayerDivideItemC2SPacket.cs             # 0x85 wire layout (10B)
        ├── PlayerEatItemC2SPacket.cs                # 0x59 wire layout (16B)
        ├── PlayerMoveItemC2SPacket.cs               # 0x5B wire layout (7B)
        ├── PlayerMoveItemS2CPacket.cs               # 0x61 wire layout (7B)
        ├── PlayerThrowAwayItemC2SPacket.cs          # 0x5E wire layout (1B)
        ├── RemoveItemS2CPacket.cs                   # 0x60 wire layout (5B)
        ├── SyncItemS2CPacket.cs                     # 0x5F wire layout (80B)
        ├── SyncMoneyS2CPacket.cs                    # 0x68 wire layout (13B)
        └── ThrowAwayAllMedicineC2SPacket.cs         # 0x83 wire layout (4B)

tests/
├── JXServer.Protocol.Tests/
│   └── InventoryAndEquipmentPacketTests.cs          # Packet serialization & framing tests
├── JXServer.ZoneEngine.Tests/
│   ├── Items/
│   │   └── CharacterItemUnitTests.cs                # Domain entity unit tests
│   └── PlayerInventoryAndEquipmentTests.cs          # 2D collision, 5-element affix & split tests
└── JXServer.Integration.Tests/
    └── InventoryAndLootIntegrationTests.cs          # Live TCP inventory move, eat & sync tests
```
