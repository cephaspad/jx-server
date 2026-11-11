---
description: "Actionable, dependency-ordered task list for 005-core-spatial-aoi-and-spawns: Spatial 3x3 Region AOI Visibility, Entity Synchronization & Static NPC/Monster Spawns"
---

# Tasks: Spatial 3x3 Region AOI Visibility & Spawns

**Input**: Design documents from `/specs/005-core-spatial-aoi-and-spawns/` (`spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`)  
**Prerequisites**: `plan.md` (required), `spec.md` (required for user stories), `research.md`, `data-model.md`, `contracts/`  
**Status**: All tasks completed (`[X]`) and verified against actual codebase.

---

## Phase 1: Static NPC & Monster Catalogs (`JXServer.GameAssets`)

- [X] T001 [P] [US3] Define `NpcTemplate` archetype, `StaticNpcSpawnTemplate`, and `INpcTemplateCatalog` query interface in `src/JXServer.GameAssets/NpcTemplates/NpcTemplate.cs`, `src/JXServer.GameAssets/NpcTemplates/StaticNpcSpawnTemplate.cs`, and `src/JXServer.GameAssets/NpcTemplates/INpcTemplateCatalog.cs`.
- [X] T002 [US3] Implement `NpcTemplateCatalog` loading `settings/npcs.txt` (decoding TCVN3 names to UTF-8) and `script/global/autoexec.lua` dialog NPC placements, and wire into `MapCatalog` / `MapDescriptor.RegionNpcSpawns` in `src/JXServer.GameAssets/NpcTemplates/NpcTemplateCatalog.cs`, `src/JXServer.GameAssets/WorldMaps/MapCatalog.cs`, and `src/JXServer.GameAssets/WorldMaps/MapDescriptor.cs`.
- [X] T003 [P] [US3] Unit test static NPC template and Map 53 `Region_S.dat` placement parsing in `tests/JXServer.ZoneEngine.Tests/NpcAssetLoadingTests.cs`.

---

## Phase 2: Spatial 2D Grid Partitioning & AOI (`JXServer.ZoneEngine`)

- [X] T004 [P] [US1] Implement `SpatialRegionSlot` holding active player and NPC membership lists in `src/JXServer.ZoneEngine/SpatialGrid/SpatialRegionSlot.cs`.
- [X] T005 [US1] Implement `SpatialRegionGrid` managing the 2D bounding grid, coordinate indexing (`RegionX = MpsX / 512`, `RegionY = MpsY / 1024`), `GetSurroundingRegions` 3x3 AOI collection, and entering/leaving set-difference computations (`New3x3 \ Old3x3`) in `src/JXServer.ZoneEngine/SpatialGrid/SpatialRegionGrid.cs`.
- [X] T006 [P] [US1] Implement in-memory `NpcZoneState` instance state with packed `NpcSettingIdx` in `src/JXServer.ZoneEngine/NpcEntities/NpcZoneState.cs`.
- [X] T007 [P] [US1] Unit test `SpatialRegionGrid` 3x3 AOI neighbor calculation and boundary crossing set differences in `tests/JXServer.ZoneEngine.Tests/SpatialRegionGridTests.cs`.
- [X] T008 [US1] Benchmark high-density boundary crossing across 50+ concurrent entities under 15ms in `tests/JXServer.ZoneEngine.Tests/HighDensityAoiBenchmarkTests.cs`.

---

## Phase 3: Wire Protocol Packets & On-Demand Queries (`JXServer.Adapter.LegacyJX1`)

- [X] T009 [P] [US2] Implement wire packet layouts for `c2s_requestplayer` (`0x48`, 5B), `c2s_requestnpc` (`0x49`, 5B), `s2c_syncnpc` (`0x4C`, 61B), `s2c_syncnpcmin` (`0x4D`, 18B), and `s2c_syncnpcminplayer` (`0x4E`, 13B) in `src/JXServer.Adapter.LegacyJX1/Packets/RequestPlayerC2SPacket.cs`, `src/JXServer.Adapter.LegacyJX1/Packets/RequestNpcC2SPacket.cs`, `src/JXServer.Adapter.LegacyJX1/Packets/SyncNpcFullS2CPacket.cs`, `src/JXServer.Adapter.LegacyJX1/Packets/SyncNpcMinS2CPacket.cs`, and `src/JXServer.Adapter.LegacyJX1/Packets/SyncNpcMinPlayerS2CPacket.cs`.
- [X] T010 [P] [US2] Unit test wire packet serialization and Little-Endian round-trips in `tests/JXServer.Protocol.Tests/AoiEntitySyncPacketTests.cs`.
- [X] T011 [US2] Implement `RequestPlayerPacketHandler` (`0x48`) and `RequestNpcPacketHandler` (`0x49`) dispatching `RequestEntitySyncZoneCommand` to the zone worker in `src/JXServer.Adapter.LegacyJX1/GameServer/RequestPlayerPacketHandler.cs` and `src/JXServer.Adapter.LegacyJX1/GameServer/RequestNpcPacketHandler.cs`.
- [X] T012 [US1] Implement outbound notification dispatchers (`SendNpcFullSync`, `SendNpcCompactSync`, `SendSelfPositionSync`) in `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.WorldSink.cs`.
- [X] T013 [US2] Route `RequestEntitySyncZoneCommand` in `WorldSessionCommandHandler` to look up entities on the active map and reply with `SendNpcFullSync` in `src/JXServer.ZoneEngine/WorldZones/WorldSessionCommandHandler.cs`.

---

## Phase 4: Integration Verification

- [X] T014 [US1, US2, US3] Verify end-to-end multi-client 3x3 AOI entity visibility, static town NPC discovery, and on-demand detail resolution over live TCP connections in `tests/JXServer.Integration.Tests/SpatialAoiIntegrationTests.cs`.
