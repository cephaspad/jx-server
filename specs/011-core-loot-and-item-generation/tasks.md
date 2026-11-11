# Tasks: Core Loot, Item Generation & Ground Objects

**Feature Branch**: `011-core-loot-and-item-generation`  
**Status**: Completed & Verified  

---

## Task List

- [X] **T01 — Primitive & Data Catalog Foundations**
  - [X] Define item primitives: `src/JXServer.Primitives/Items/ItemGenreKind.cs`, `src/JXServer.Primitives/Items/ItemQualityKind.cs`, `src/JXServer.Primitives/Items/EquipmentPartKind.cs`, `src/JXServer.Primitives/Items/ItemContainerPlace.cs`, `src/JXServer.Primitives/Items/GroundObjectKind.cs`, `src/JXServer.Primitives/Items/ItemConsts.cs`
  - [X] Implement static loot catalogs: `src/JXServer.GameAssets/LootTables/LootTableCatalog.cs`, `src/JXServer.GameAssets/LootTables/LootTableTemplate.cs`, `src/JXServer.GameAssets/LootTables/WeightedLootPoolEntryTemplate.cs`
  - [X] Implement item template catalogs: `src/JXServer.GameAssets/Items/ItemTemplateCatalog.cs`, `src/JXServer.GameAssets/Items/ItemTemplate.cs`, `src/JXServer.GameAssets/Items/MagicAffixTemplate.cs`, `src/JXServer.GameAssets/Items/GroundObjectTemplate.cs`

- [X] **T02 — Procedural 6-Affix Blue Item Generator** `src/JXServer.GameAssets/Items/`
  - [X] Port version 4 generator logic (`m_wVersion = 4`): `ItemGenerator.cs`
  - [X] Implement base durability, defense, and damage rolling: `GeneratedItemData.cs`
  - [X] Implement 6-slot magic attribute generation (3 explicit prefix/suffix, 3 hidden Five-Elements slots): `EquipPartWeightsTemplate.cs`, `SeriesWeightsTemplate.cs`
  - [X] Item generator unit test verification: `tests/JXServer.ZoneEngine.Tests/BlueItemGeneratorAndLootTests.cs`

- [X] **T03 — Wire Packet Serialization & Deserialization** `src/JXServer.Adapter.LegacyJX1/Packets/`
  - [X] Define ground object add packet struct (`0x64`): `ObjAddS2CPacket.cs`
  - [X] Define ground object remove packet struct (`0x67`): `ObjRemoveS2CPacket.cs`
  - [X] Define ground object query packet struct (`0x4A`): `RequestObjC2SPacket.cs`
  - [X] Define object mouse click packet struct (`0x72`): `ObjMouseClickC2SPacket.cs`
  - [X] Define player pickup packet struct (`0x5A`): `PlayerPickupItemC2SPacket.cs`
  - [X] Define throw-away item packet structs (`0x5E`, `0x83`): `PlayerThrowAwayItemC2SPacket.cs`, `ThrowAwayAllMedicineC2SPacket.cs`
  - [X] Wire packet test verification: `tests/JXServer.Protocol.Tests/GroundObjectPacketTests.cs`

- [X] **T04 — Phase 400 Ground Object Tick System** `src/JXServer.ZoneEngine/Items/`
  - [X] Implement in-memory ground object slot: `GroundObjectSlot.cs`
  - [X] Implement Phase 400 18-Hz ground object tick system: `GroundObjectZoneTickSystem.cs`, `GroundObjectTickSystem.cs`
  - [X] Implement killer ownership protection timer (180 frames) and despawn expiration (1,080 frames)

- [X] **T05 — Zone Loot Commands & Atomic Pickup Handling** `src/JXServer.ZoneEngine/Items/`
  - [X] Define pickup and query commands: `PickupGroundObjectZoneCommand.cs`, `RequestGroundObjectSyncZoneCommand.cs`, `DropInventoryItemZoneCommand.cs`
  - [X] Implement atomic pickup handling with zero duplication: `InventoryLootCommandHandler.cs`
  - [X] Implement packet handlers: `PlayerPickupItemPacketHandler.cs`, `RequestObjPacketHandler.cs`, `ObjMouseClickPacketHandler.cs`, `PlayerThrowAwayItemPacketHandler.cs`, `ThrowAwayAllMedicinePacketHandler.cs`
  - [X] Implement legacy inventory outbound sink: `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.InventorySink.cs`

- [X] **T06 — Integration & Concurrency Verification**
  - [X] End-to-end loot drop and pickup test suite: `tests/JXServer.Integration.Tests/InventoryAndLootIntegrationTests.cs`
  - [X] High-density concurrent pickup race condition stress tests: `tests/JXServer.Integration.Tests/HighDensityCombatAndLootStressTests.cs`
