---
description: "Actionable, dependency-ordered task list for 007-core-portals-and-map-handoff: Boundary Portals & Map Handoff"
---

# Tasks: Boundary Portals & Map Handoff

**Input**: Design documents from `/specs/007-core-portals-and-map-handoff/` (`spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`)  
**Prerequisites**: `plan.md` (required), `spec.md` (required for user stories), `research.md`, `data-model.md`, `contracts/`  
**Status**: All tasks completed (`[X]`) and verified against actual codebase.

---

## Phase 1: Shared Enums, Persistence DTO & Trap Catalogs

- [X] T001 [P] [US3] Implement `PortalTrapActionKind` enum (`ToggleFightStateAndSetPos = 1`, `ChangeMapPortal = 2`) in `src/JXServer.Primitives/WorldZones/PortalTrapActionKind.cs`.
- [X] T002 [P] [US2] Extend `UpdateCharacterWorldSnapshotDto` with `MapId`, `MapMpsX`, `MapMpsY`, and `FightMode` in `src/JXServer.Contracts/Characters/UpdateCharacterWorldSnapshotDto.cs`.
- [X] T003 [P] [US1] Implement `PakFilePathHasher` (`g_FileName2Id` path hash), `RegionTrapGrid`, `PortalTrapBindingTemplate`, `ITrapScriptCatalog`, and `TrapScriptCatalog` in `src/JXServer.GameAssets/WorldMaps/`.

---

## Phase 2: In-Memory Map Transfer & Sharded Routing (`JXServer.ZoneEngine`)

- [X] T004 [P] [US2] Implement `AttachTransferredPlayerZoneCommand` transferring player state to destination shard in `src/JXServer.ZoneEngine/PlayerSessions/AttachTransferredPlayerZoneCommand.cs`.
- [X] T005 [US2, US4] Implement `IZoneClusterRouter` and `ZoneClusterRouter.TryTransferPlayerAcrossMaps` coordinating inter-shard state handoff in `src/JXServer.ZoneEngine/WorldZones/IZoneClusterRouter.cs` and `src/JXServer.ZoneEngine/WorldZones/ZoneClusterRouter.cs`.
- [X] T006 [US1, US3, US4] Implement cell trap checking, edge-triggered anti-loop tracking (`LastTriggeredTrapId`), town gate `FightMode` toggling, and level gate validation in `src/JXServer.ZoneEngine/WorldZones/MovementAndPostureZoneTickSystem.cs`.
- [X] T007 [P] [US1, US2, US3] Unit test trap script parsing, `LastTriggeredTrapId` anti-loop suppression, fight mode toggles, and same-node transfers in `tests/JXServer.ZoneEngine.Tests/TrapAndMapTransitionTests.cs`.

---

## Phase 3: Wire Protocol Packets & Outbound Sinks (`JXServer.Adapter.LegacyJX1`)

- [X] T008 [P] [US2, US4] Implement wire packet layouts for `SyncWorldS2CPacket` (`0x49`, 34B) and `NotifyPlayerExchangeS2CPacket` (`0x35`, 25B, plain little-endian port) in `src/JXServer.Adapter.LegacyJX1/Packets/SyncWorldS2CPacket.cs` and `src/JXServer.Adapter.LegacyJX1/Packets/NotifyPlayerExchangeS2CPacket.cs`.
- [X] T009 [US2, US4] Implement `SendWorldMapTransitionSync` and `SendNotifyServerExchange` in `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.WorldSink.cs`.

---

## Phase 4: Integration Verification

- [X] T010 [US1, US2, US3, US4] Verify seamless same-node map transitions over live TCP connection without cipher reset, level-gate bouncing, and cross-node redirects in `tests/JXServer.Integration.Tests/MapTransitionIntegrationTests.cs`.
