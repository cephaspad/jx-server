# Implementation Plan: Core Loot, Item Generation & Ground Objects

**Branch**: `011-core-loot-and-item-generation` | **Date**: 2026-11-11 | **Spec**: [spec.md](spec.md)

## Summary

Deliver the **011-core-loot-and-item-generation** vertical slice of `JXServer` in strict compliance with **Constitution v1.2.0** and **JXData Standard v1**:
1. **Weighted Monster Loot Table Evaluation**: Evaluate declarative drop tables via `ILootTableCatalog` / `LootTableCatalog` (`LootTableTemplate`, `WeightedLootPoolEntryTemplate`), computing currency amounts and item drops.
2. **Procedural 6-Affix Blue Equipment Rolling (`ItemGenerator`)**: Port the authentic Kingsoft `KItemGenerator.cpp` algorithm (`m_wVersion = 4`), rolling durability, base defense/damage stats, and up to 6 magic attributes (3 explicit prefix/suffix slots and 3 hidden Five-Elements slots) filtered by equipment part weights, element series, item level, and player luck.
3. **Ground Object Lifecycle (`GroundObjectZoneTickSystem`)**: Manage `GroundObjectSlot` instances in `SpatialRegionSlot.GroundObjects`, stepped on Phase 400 (`ZoneTickPhase.GroundObjects = 400`) to enforce killer ownership protection (`DefaultLootOwnerProtectionFrames = 180`), broadcast additions (`0x64` `s2c_objadd`), and expire uncollected items (`0x67` `s2c_objremove` with reason `0`).
4. **Zero-Duplication Atomic Pickup**: Process `PickupGroundObjectZoneCommand` (`0x5A` `c2s_playerpickupitem`) in `InventoryLootCommandHandler`, atomically transferring currency or items to character bags and emitting `s2c_objremove` (`0x67` with reason `1`) and `s2c_syncitem` (`0x5F`) / `s2c_syncmoney` (`0x68`).
5. **Item Dropping**: Process `DropInventoryItemZoneCommand` (`0x5E` `c2s_playerthrowawayitem` and `0x83` `c2s_throwawayallmedicine`), creating ground objects on walkable terrain.

---

## Technical Context

- **Platform & Language**: .NET 10 (`net10.0`), C# 14 with `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `AllowUnsafeBlocks=true`.
- **Primary Projects**:
  - `src/JXServer.Primitives`: `ItemGenreKind`, `ItemQualityKind`, `EquipmentPartKind`, `ItemContainerPlace`, `GroundObjectKind`, `ItemConsts`.
  - `src/JXServer.GameAssets`: `ItemGenerator`, `IItemTemplateCatalog`, `ItemTemplateCatalog`, `ItemTemplate`, `MagicAffixTemplate`, `GroundObjectTemplate`, `ILootTableCatalog`, `LootTableCatalog`, `LootTableTemplate`.
  - `src/JXServer.ZoneEngine`: `GroundObjectSlot`, `GroundObjectZoneTickSystem`, `GroundObjectTickSystem`, `PickupGroundObjectZoneCommand`, `RequestGroundObjectSyncZoneCommand`, `DropInventoryItemZoneCommand`, `InventoryLootCommandHandler`, `IInventoryOutboundSink`.
  - `src/JXServer.Adapter.LegacyJX1`: Wire packets `ObjAddS2CPacket` (`0x64`), `ObjRemoveS2CPacket` (`0x67`), `RequestObjC2SPacket` (`0x4A`), `ObjMouseClickC2SPacket` (`0x72`), `PlayerPickupItemC2SPacket` (`0x5A`), `PlayerThrowAwayItemC2SPacket` (`0x5E`), `ThrowAwayAllMedicineC2SPacket` (`0x83`), and packet handlers.
- **Verification Suites**:
  - `tests/JXServer.Protocol.Tests/GroundObjectPacketTests.cs`
  - `tests/JXServer.ZoneEngine.Tests/BlueItemGeneratorAndLootTests.cs`
  - `tests/JXServer.Integration.Tests/InventoryAndLootIntegrationTests.cs`
  - `tests/JXServer.Integration.Tests/HighDensityCombatAndLootStressTests.cs`

---

## Constitution Check (v1.2.0)

| Principle | Status | Evaluation |
| :--- | :---: | :--- |
| **I. Hybrid Distributed Control Plane & Sharded 18-Hz Zone Loop** | **PASS** | Ground objects reside strictly in spatial region slots of single-threaded map shards. Pickup operations execute without cross-shard locks. |
| **II. Multi-Protocol Transport Abstraction** | **PASS** | Ground object interactions use typed commands (`PickupGroundObjectZoneCommand`) and domain `IInventoryOutboundSink`. Wire framing stays in `Adapter.LegacyJX1`. |
| **III. Zero-Hallucination & Ground-Truth Fidelity** | **PASS** | `ItemGenerator` faithfully ports Kingsoft `KItemGenerator.cpp` / `KItemSet.cpp` version 4 with exact affix rolling and drop table parsing. |
| **IV. Zero-Dupe Economy & Lease-Fenced Integrity** | **PASS** | Single-threaded zone serialization eliminates race conditions; non-stackable items maintain unique `Guid` values across the persistence lifecycle. |
| **V. UTF-8 Domain Core & Boundary-Only Transcoding** | **PASS** | Ground object names and catalogs are stored in standard UTF-8; wire packets transcode to null-terminated `TCVN3` at the wire adapter edge. |
| **VI. Clean Modular Boundaries, ABP DDD & C# 14** | **PASS** | 1 type per `.cs` file, sealed concrete classes, pluralized folders (`Items/`, `LootTables/`), unit suffixes (`*MpsX`, `*MpsY`, `*Frames`). |

---

## Project Structure

```text
specs/011-core-loot-and-item-generation/
├── spec.md
├── checklists/
│   └── requirements.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── loot-and-ground-object-protocol.md
└── tasks.md
```

### Actual Codebase Mapping

```text
src/
├── JXServer.Primitives/
│   └── Items/
│       ├── ItemGenreKind.cs
│       ├── ItemQualityKind.cs
│       ├── EquipmentPartKind.cs
│       ├── ItemContainerPlace.cs
│       ├── GroundObjectKind.cs
│       └── ItemConsts.cs
├── JXServer.GameAssets/
│   ├── Items/
│   │   ├── ItemGenerator.cs
│   │   ├── IItemTemplateCatalog.cs
│   │   ├── ItemTemplateCatalog.cs
│   │   ├── ItemTemplate.cs
│   │   ├── MagicAffixTemplate.cs
│   │   ├── GroundObjectTemplate.cs
│   │   ├── ItemBaseAttributeTemplate.cs
│   │   ├── ItemRequirementTemplate.cs
│   │   ├── EquipPartWeightsTemplate.cs
│   │   ├── SeriesWeightsTemplate.cs
│   │   └── GeneratedItemData.cs
│   └── LootTables/
│       ├── ILootTableCatalog.cs
│       ├── LootTableCatalog.cs
│       ├── LootTableTemplate.cs
│       └── WeightedLootPoolEntryTemplate.cs
├── JXServer.ZoneEngine/
│   ├── Items/
│   │   ├── GroundObjectSlot.cs
│   │   ├── GroundObjectZoneTickSystem.cs
│   │   ├── GroundObjectTickSystem.cs
│   │   ├── PickupGroundObjectZoneCommand.cs
│   │   ├── RequestGroundObjectSyncZoneCommand.cs
│   │   ├── DropInventoryItemZoneCommand.cs
│   │   └── InventoryLootCommandHandler.cs
│   └── PlayerSessions/
│       ├── GroundObjectSyncSnapshot.cs
│       ├── IInventoryOutboundSink.cs
│       └── NoOpInventoryOutboundSink.cs
└── JXServer.Adapter.LegacyJX1/
    ├── Packets/
    │   ├── ObjAddS2CPacket.cs
    │   ├── ObjRemoveS2CPacket.cs
    │   ├── RequestObjC2SPacket.cs
    │   ├── ObjMouseClickC2SPacket.cs
    │   ├── PlayerPickupItemC2SPacket.cs
    │   ├── PlayerThrowAwayItemC2SPacket.cs
    │   └── ThrowAwayAllMedicineC2SPacket.cs
    └── GameServer/
        ├── GameServerClientSession.InventorySink.cs
        ├── RequestObjPacketHandler.cs
        ├── ObjMouseClickPacketHandler.cs
        ├── PlayerPickupItemPacketHandler.cs
        ├── PlayerThrowAwayItemPacketHandler.cs
        └── ThrowAwayAllMedicinePacketHandler.cs

tests/
├── JXServer.Protocol.Tests/
│   └── GroundObjectPacketTests.cs
├── JXServer.ZoneEngine.Tests/
│   └── BlueItemGeneratorAndLootTests.cs
└── JXServer.Integration.Tests/
    ├── InventoryAndLootIntegrationTests.cs
    └── HighDensityCombatAndLootStressTests.cs
```
