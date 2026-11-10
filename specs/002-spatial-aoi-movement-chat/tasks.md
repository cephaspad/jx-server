---
description: "Actionable, dependency-ordered task list for Milestone 2: Spatial 3x3 Region AOI, Movement, Map Transitions & Multi-Channel Chat"
---

# Tasks: Spatial 3x3 Region AOI, Movement, Map Transitions & Multi-Channel Chat

**Input**: Design documents from `/specs/002-spatial-aoi-movement-chat/` (`spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`)

**Prerequisites**: `plan.md` (required), `spec.md` (required for user stories), `research.md`, `data-model.md`, `contracts/`

**Tests**: Included per Constitution Verification Gates 2, 3, and 4 (`JXServer.Protocol.Tests`, `JXServer.ZoneEngine.Tests`, and `JXServer.Integration.Tests`).

**Organization**: Tasks are grouped by phase and user story (`US3` P3 Asset Prerequisite, `US1` P1 MVP, `US2` P2, `US4` P4, `US5` P5) so each story can be implemented, tested, and validated as an independent increment while strictly enforcing **One-Type-Per-File**, pluralized feature directories, **sealed concrete classes**, **ABP Framework DDD** naming, and the **5 Real-Time 18-Hz Game Server** naming conventions.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (`[US1]`, `[US2]`, `[US3]`, `[US4]`, `[US5]`)
- Every task specifies the exact target file path(s) in `d:\Projects\jx-server`

---

## Phase 1: Setup (Feature Directories & Multi-Map Host Configuration)

**Purpose**: Create the pluralized feature folders for Milestone 2 across `Primitives`, `GameAssets`, `ZoneEngine`, and `Adapter.LegacyJX1`, and configure `JXServer.Host` to load Ba Lăng Huyện (`53`) and its connected portal destination maps (`54`, `70`, `74`, `199`).

- [X] T001 Create pluralized feature directories (`src/JXServer.Primitives/Chats`, `src/JXServer.GameAssets/NpcTemplates`, `src/JXServer.ZoneEngine/SpatialGrid`, `src/JXServer.ZoneEngine/NpcEntities`, `src/JXServer.ZoneEngine/Chats`) and update `ConfiguredMapIds` to `[53, 54, 70, 74, 199]` in `src/JXServer.Host/appsettings.json`

---

## Phase 2: Foundational (Shared Primitives, Protocol Framing & Durable Position Persistence)

**Purpose**: Core shared enums, anti-cheat checksum constants, variable-length protocol framing rules (`0x52`, `0x56`), durable coordinate persistence (`FR-012`), and structured log messages that MUST be complete before user story implementation.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 [P] Create `EntityDoingPosture` enum (`Stand = 1`, `Walk = 2`, `Run = 3`, `Sit = 4`, `Jump = 5`, `Attack = 6`, `Skill = 7`, `Death = 11`) in `src/JXServer.Primitives/WorldZones/EntityDoingPosture.cs`, `EntityCamp` enum (`Begin = 0`, `Justice = 1`, `Evil = 2`, `Balance = 3`, `Free = 4`, `Animal = 5`, `Event = 6`) in `src/JXServer.Primitives/WorldZones/EntityCamp.cs`, `PortalTrapActionKind` enum (`ToggleFightStateAndSetPos = 1`, `ChangeMapPortal = 2`) in `src/JXServer.Primitives/WorldZones/PortalTrapActionKind.cs`, and `MovementAntiCheatConsts` (`WalkCheckKey = 0x24A1u`, `RunCheckKey = 0x1D78u`, `SkillCheckKey = 0x3AB9u`, `TradeStartCheckKey = 0x451Fu`, `DialogNpcCheckKey = 0x506Cu`, `TradeReplyCheckKey = 0x67CAu`, and `IsValidCheckValue(uint checkValue, uint token1, uint token2, uint key)`) in `src/JXServer.Primitives/WorldZones/MovementAntiCheatConsts.cs`
- [X] T003 [P] Create `ChatChannelKind` enum (`All = 0`, `World = 1`, `Nearby = 2`, `PrivateWhisper = 3`, `Team = 4`, `Faction = 5`, `Tong = 6`, `Screen = 7`, `System = 8`) in `src/JXServer.Primitives/Chats/ChatChannelKind.cs` and `ChatConsts` (`MaxTalkMessageBytes = 256`, `MaxChatSentenceBytes = 256`, `SenderNameBufferLength = 32`, `SentencePrefixBufferLength = 32`, `DefaultSubscribedChannelMask = 0xFFFFFFFFu`, `SystemExtendHeader = 0x00010032u`) in `src/JXServer.Primitives/Chats/ChatConsts.cs`
- [X] T004 [P] Extend `UpdateCharacterWorldSnapshotDto` with `long ExpectedLeaseEpoch` and `byte FightMode = 0` in `src/JXServer.Contracts/Characters/UpdateCharacterWorldSnapshotDto.cs`, add `UpdateCharacterWorldSnapshotAsync` to `src/JXServer.Contracts/Characters/ICharacterAppService.cs`, and implement lease-fenced `UpdateCharacterWorldSnapshotAsync` (calling `Character.UpdateWorldSnapshot` per `FR-012`) in `src/JXServer.Application/Characters/CharacterAppService.cs`
- [X] T005 [P] Update `ProtocolOpcodes` (adding `s2c_npcsit = 0x53` `5B` and `s2c_chatgetsomeone = 0x7E` variable) in `src/JXServer.Adapter.LegacyJX1/Packets/ProtocolOpcodes.cs` and extend `ProtocolSizeTable.TryValidateC2SPacketSize` to validate `c2s_requestplayer` (`0x48`, `5B`), `c2s_requestnpc` (`0x49`, `5B`), `c2s_npcwalk` (`0x4B`, `13B`), `c2s_npcrun` (`0x4C`, `13B`), `c2s_npcjump` (`0x4E`, `13B`), `c2s_chatsetchannel` (`0x60`, `5B`), `c2s_npcsit` (`0x71`, `2B`), and variable-length `c2s_playertalk` (`0x52`, min `8B`, max `264B`, `wLengthMinusOne + 1 == payload.Length`) and `c2s_playersendchat` (`0x56`, min `48B`, max `360B`, `wLengthMinusOne + 1 == payload.Length`) in `src/JXServer.Adapter.LegacyJX1/Framing/ProtocolSizeTable.cs`
- [X] T006 [P] Add Milestone 2 source-generated `[LoggerMessage]` methods for NPC/Trap asset loading, AOI sync, movement anti-cheat warnings, portal transitions, and chat routing in `src/JXServer.GameAssets/Logging/GameAssetsLogMessages.cs`, `src/JXServer.ZoneEngine/Logging/ZoneEngineLogMessages.cs`, and `src/JXServer.Adapter.LegacyJX1/Logging/LegacyAdapterLogMessages.cs`

**Checkpoint**: Foundation ready — shared enums, anti-cheat verifier, variable-length framing, and durable coordinate persistence are in place.

---

## Phase 3: User Story 3 - Static NPC & Monster Spawning from Map Assets (Priority: P3 — Asset Prerequisite for 3x3 AOI)

**Goal**: Parse `settings/npcs.txt` (`INpcTemplateCatalog`), `script/global/autoexec.lua` (`adddialognpc`), and `Region_S.dat` Section `2` (`NPC_SECTION`: 12-byte `KNpcFileHead` + variable-length `60 + nScriptNameLen`-byte `KSPNpc` records) on startup, resolving authentic Vietnamese display names (`TCVN3` $\rightarrow$ `UTF-8`) so all static town NPCs and field monsters spawn at their exact coordinates (`FR-004`, `SC-002`).

**Note on Execution Order**: Placed immediately before `US1` because `US1` (`Spatial 3x3 Region Visibility & Neighbor Synchronization`) requires static NPCs to be loaded and spawned inside map regions for AOI synchronization.

**Independent Test**: Run `dotnet test tests/JXServer.ZoneEngine.Tests --filter NpcAssetLoadingTests` against `d:\Projects\jx-linux-bin\server1` to verify `settings/npcs.txt` templates, `autoexec.lua` dialog NPCs, and Map `53` (`Ba Lăng Huyện`) `Region_S.dat` `NPC_SECTION` placements load with localized Vietnamese names.

### Implementation & Tests for User Story 3

- [X] T007 [P] [US3] Create `NpcTemplate` (`int TemplateId`, `string Name`, `EntityKind Kind`, `EntityCamp Camp`, `ElementSeries Series`, `int WalkSpeed` default `10`, `int RunSpeed` default `20`, `int BaseLifeMax`, `int DialogRadius`) in `src/JXServer.GameAssets/NpcTemplates/NpcTemplate.cs`, `StaticNpcSpawnTemplate` (`int TemplateId`, `ushort MapId`, `int SpawnMpsX`, `int SpawnMpsY`, `short Level`, `EntityKind Kind`, `EntityCamp Camp`, `ElementSeries Series`, `bool IsSpecialNpc`, `string DisplayName`, `string ScriptPath`) in `src/JXServer.GameAssets/NpcTemplates/StaticNpcSpawnTemplate.cs`, and `INpcTemplateCatalog` in `src/JXServer.GameAssets/NpcTemplates/INpcTemplateCatalog.cs`
- [X] T008 [US3] Implement `NpcTemplateCatalog` (parsing `settings/npcs.txt` where 0-based `TemplateId` maps to data rows after header row 1 and baseline row 2, decoding `TCVN3` names to `UTF-8`, and parsing `script/global/autoexec.lua` `adddialognpc` entries with `SpawnMpsX = cellX * 32`, `SpawnMpsY = cellY * 32`) in `src/JXServer.GameAssets/NpcTemplates/NpcTemplateCatalog.cs`
- [X] T009 [US3] Extend `MapDescriptor` with `IReadOnlyList<StaticNpcSpawnTemplate> RegionNpcSpawns` in `src/JXServer.GameAssets/WorldMaps/MapDescriptor.cs`, extend `MapCatalog` to parse `Region_S.dat` Section `2` (`NPC_SECTION`: 12B `KNpcFileHead` + `60 + nScriptNameLen` byte `KSPNpc` records, preferring localized Vietnamese `NpcTemplate.Name` from `INpcTemplateCatalog` over raw GBK `KSPNpc.szName`) and merge `GetAutoExecSpawnsForMap(mapId)` in `src/JXServer.GameAssets/WorldMaps/MapCatalog.cs`, and register `INpcTemplateCatalog` in `src/JXServer.GameAssets/GameAssetsServiceCollectionExtensions.cs`
- [X] T010 [P] [US3] Create `NpcZoneState` (`uint NpcId` in `1..9999`, `int TemplateId`, `string Name`, `short Level`, `EntityKind Kind`, `EntityCamp Camp`, `ElementSeries Series`, `bool IsSpecialNpc`, `ushort MapId`, `int MpsX`, `int MpsY`, `int RegionX`, `int RegionY`, `EntityDoingPosture Doing`, `byte Dir`, `int WalkSpeed`, `int RunSpeed`, `uint CurLife`, `uint MaxLife`, `string ScriptPath`, and `uint PackedNpcSettingIdx => ((uint)(ushort)Level << 16) | (uint)(ushort)TemplateId`) in `src/JXServer.ZoneEngine/NpcEntities/NpcZoneState.cs`
- [X] T011 [P] [US3] Write unit tests verifying `NpcTemplateCatalog` (`settings/npcs.txt` & `autoexec.lua`), Map `53` `Region_S.dat` `NPC_SECTION` binary parsing (`347` regions with NPCs), and Vietnamese name resolution in `tests/JXServer.ZoneEngine.Tests/NpcAssetLoadingTests.cs`

**Checkpoint**: User Story 3 complete — all static NPCs and monsters in Map `53` and connected maps are parsed with authentic Vietnamese names and ready for spatial region spawning.

---

## Phase 4: User Story 1 - Spatial 3x3 Region Visibility & Neighbor Synchronization (Priority: P1) 🎯 MVP

**Goal**: Partition each active map into a 2D grid of Regions (`512 x 1024` Mps / `16 x 32` cells), spawn static `NpcZoneState` instances into their regions on startup, synchronize all players and NPCs within the surrounding `3x3` region AOI (`0x4C` `s2c_syncnpc`, `0x4D` `s2c_syncnpcmin`, `0x4E` `s2c_syncnpcminplayer`) on world entry and region crossings, and respond to client entity detail queries (`0x48` `c2s_requestplayer`, `0x49` `c2s_requestnpc` $\rightarrow$ `0x4C`) (`FR-001`, `FR-002`, `FR-003`).

**Independent Test**: Connect two or more clients/bots to Map `53` at the same spawn region and verify that each client immediately receives `0x4C` (`s2c_syncnpc`) for nearby players and static town NPCs, receives periodic `0x4D` updates, and can query any entity via `0x48`/`0x49`.

### Implementation & Tests for User Story 1

- [X] T012 [P] [US1] Create `SpatialRegionSlot` (`int RegionX`, `int RegionY`, `List<PlayerZoneState> Players`, `List<NpcZoneState> Npcs`) in `src/JXServer.ZoneEngine/SpatialGrid/SpatialRegionSlot.cs` and `SpatialRegionGrid` (maintaining per-region player/NPC membership, `GetSurroundingRegions` for the `3x3` AOI, and `ComputeEnteringRegions` set difference `New3x3 \ Old3x3`) in `src/JXServer.ZoneEngine/SpatialGrid/SpatialRegionGrid.cs`
- [X] T013 [P] [US1] Create `EntityFullSyncSnapshot` readonly record struct in `src/JXServer.ZoneEngine/PlayerSessions/EntityFullSyncSnapshot.cs` and `RequestEntitySyncZoneCommand` (`Guid RequesterCharacterId`, `long LeaseEpoch`, `uint TargetNpcId`, `bool IsPlayerRequest`) in `src/JXServer.ZoneEngine/PlayerSessions/RequestEntitySyncZoneCommand.cs`
- [X] T014 [P] [US1] Create wire packet structs for US1: `RequestPlayerC2SPacket` (`0x48`, `5B` `<BI`) in `src/JXServer.Adapter.LegacyJX1/Packets/RequestPlayerC2SPacket.cs`, `RequestNpcC2SPacket` (`0x49`, `5B` `<BI`) in `src/JXServer.Adapter.LegacyJX1/Packets/RequestNpcC2SPacket.cs`, and `SyncNpcMinS2CPacket` (`0x4D`, `18B` `<BIiiBBBBB`) in `src/JXServer.Adapter.LegacyJX1/Packets/SyncNpcMinS2CPacket.cs`
- [X] T015 [US1] Extend `IPlayerOutboundSink` in `src/JXServer.ZoneEngine/PlayerSessions/IPlayerOutboundSink.cs` and `GameServerClientSession` in `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.cs` with `SendNpcFullSync(in EntityFullSyncSnapshot snapshot)` (`0x4C`), `SendNpcCompactSync` (`0x4D`), and `SendSelfPositionSync` (`0x4E`), and update `MapZoneWorker` to spawn `MapDescriptor.RegionNpcSpawns` into `SpatialRegionGrid`, synchronize the `3x3` region AOI (`0x4C`) when a player completes `0x43` sync-end or changes region, broadcast periodic `0x4D`/`0x4E` updates, remove departing players from their `SpatialRegionSlot` on detach, and handle `RequestEntitySyncZoneCommand` in `src/JXServer.ZoneEngine/WorldZones/MapZoneWorker.cs`
- [X] T016 [P] [US1] Implement `RequestPlayerPacketHandler` (`0x48`) in `src/JXServer.Adapter.LegacyJX1/GameServer/RequestPlayerPacketHandler.cs` and `RequestNpcPacketHandler` (`0x49`) in `src/JXServer.Adapter.LegacyJX1/GameServer/RequestNpcPacketHandler.cs`, and register both handlers in `src/JXServer.Adapter.LegacyJX1/LegacyJX1AdapterServiceCollectionExtensions.cs`
- [X] T017 [P] [US1] Write packet round-trip tests for `0x48`, `0x49`, `0x4C`, `0x4D`, and `0x4E` in `tests/JXServer.Protocol.Tests/AoiEntitySyncPacketTests.cs`, deterministic `SpatialRegionGrid` `3x3` AOI diff & NPC spawn tests in `tests/JXServer.ZoneEngine.Tests/SpatialRegionGridTests.cs`, and multi-client TCP AOI neighbor visibility tests in `tests/JXServer.Integration.Tests/SpatialAoiIntegrationTests.cs`

**Checkpoint**: User Story 1 (MVP) complete — players and bots entering Map `53` immediately see all static NPCs and neighboring players within their `3x3` region AOI.

---

## Phase 5: User Story 2 - Obstacle-Validated Movement, Posture & Step Progression (Priority: P2)

**Goal**: Process player walk (`0x4B` $\rightarrow$ `0x50`), run (`0x4C` $\rightarrow$ `0x51`), jump (`0x4E` $\rightarrow$ `0x54`), and sit/stand (`0x71` $\rightarrow$ `0x53`) commands, enforcing `KPlayer::IsCheckValueValid` (`0x24A1` / `0x1D78`), advancing coordinates across 18-Hz simulation ticks at `WalkSpeed`/`RunSpeed`, halting at `Obstacle_Full (1)` cells, updating `3x3` region membership on boundary crossings, and regenerating HP/MP while seated (`FR-005`, `FR-006`, `FR-007`, `SC-003`).

**Independent Test**: Connect clients/bots to Map `53`, issue walk, run, jump, and sit commands (including tampered `dwCheckValue` and blocked obstacle destinations), and verify valid movements advance and broadcast to the `3x3` AOI while invalid/blocked moves are rejected or stopped at the obstacle edge.

### Implementation & Tests for User Story 2

- [X] T018 [P] [US2] Create `MovePlayerZoneCommand` (`Guid CharacterId`, `long LeaseEpoch`, `int TargetMpsX`, `int TargetMpsY`, `bool IsRun`) in `src/JXServer.ZoneEngine/PlayerSessions/MovePlayerZoneCommand.cs`, `JumpPlayerZoneCommand` (`Guid CharacterId`, `long LeaseEpoch`, `int TargetMpsX`, `int TargetMpsY`) in `src/JXServer.ZoneEngine/PlayerSessions/JumpPlayerZoneCommand.cs`, and `TogglePlayerSitZoneCommand` (`Guid CharacterId`, `long LeaseEpoch`, `bool IsSitting`) in `src/JXServer.ZoneEngine/PlayerSessions/TogglePlayerSitZoneCommand.cs`, and extend `PlayerZoneState` with `TargetMpsX`, `TargetMpsY`, `EntityDoingPosture Doing`, `WalkSpeed = 10`, `RunSpeed = 20`, and `LastSitRegenTick` in `src/JXServer.ZoneEngine/PlayerSessions/PlayerZoneState.cs`
- [X] T019 [P] [US2] Create C2S movement wire packet structs: `NpcWalkC2SPacket` (`0x4B`, `13B` `<BiiI`) in `src/JXServer.Adapter.LegacyJX1/Packets/NpcWalkC2SPacket.cs`, `NpcRunC2SPacket` (`0x4C`, `13B` `<BiiI`) in `src/JXServer.Adapter.LegacyJX1/Packets/NpcRunC2SPacket.cs`, `NpcJumpC2SPacket` (`0x4E`, `13B` `<BiiI`) in `src/JXServer.Adapter.LegacyJX1/Packets/NpcJumpC2SPacket.cs`, and `NpcSitC2SPacket` (`0x71`, `2B` `<BB`) in `src/JXServer.Adapter.LegacyJX1/Packets/NpcSitC2SPacket.cs`
- [X] T020 [P] [US2] Create S2C movement wire packet structs: `NpcWalkS2CPacket` (`0x50`, `13B` `<BIii`: `NpcId` at `+1`, `DestMpsY` at `+5`, `DestMpsX` at `+9`) in `src/JXServer.Adapter.LegacyJX1/Packets/NpcWalkS2CPacket.cs`, `NpcRunS2CPacket` (`0x51`, `13B` `<BiIi`: `DestMpsY` at `+1`, `NpcId` at `+5`, `DestMpsX` at `+9`) in `src/JXServer.Adapter.LegacyJX1/Packets/NpcRunS2CPacket.cs`, `NpcJumpS2CPacket` (`0x54`, `13B` `<BiiI`: `DestMpsY` at `+1`, `DestMpsX` at `+5`, `NpcId` at `+9`) in `src/JXServer.Adapter.LegacyJX1/Packets/NpcJumpS2CPacket.cs`, and `NpcSitS2CPacket` (`0x53`, `5B` `<BI`: `NpcId` at `+1`) in `src/JXServer.Adapter.LegacyJX1/Packets/NpcSitS2CPacket.cs`
- [X] T021 [US2] Extend `IPlayerOutboundSink` in `src/JXServer.ZoneEngine/PlayerSessions/IPlayerOutboundSink.cs` and `GameServerClientSession` in `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.cs` with `SendWalkBroadcast` (`0x50`), `SendRunBroadcast` (`0x51`), `SendJumpBroadcast` (`0x54`), `SendSitBroadcast` (`0x53`), and `SendNormalVitalSync` (`0x46`), and implement 18-Hz step movement progression, `Obstacle_Full (1)` collision halting, `3x3` region boundary crossing AOI diff sync, jump validation, and sit HP/MP regeneration every 18 ticks in `src/JXServer.ZoneEngine/WorldZones/MapZoneWorker.cs`
- [X] T022 [P] [US2] Implement `NpcWalkPacketHandler` (`0x4B`, verifying `MovementAntiCheatConsts.WalkCheckKey` `0x24A1`) in `src/JXServer.Adapter.LegacyJX1/GameServer/NpcWalkPacketHandler.cs`, `NpcRunPacketHandler` (`0x4C`, verifying `MovementAntiCheatConsts.RunCheckKey` `0x1D78`) in `src/JXServer.Adapter.LegacyJX1/GameServer/NpcRunPacketHandler.cs`, `NpcJumpPacketHandler` (`0x4E`) in `src/JXServer.Adapter.LegacyJX1/GameServer/NpcJumpPacketHandler.cs`, and `NpcSitPacketHandler` (`0x71`) in `src/JXServer.Adapter.LegacyJX1/GameServer/NpcSitPacketHandler.cs`, and register them in `src/JXServer.Adapter.LegacyJX1/LegacyJX1AdapterServiceCollectionExtensions.cs`
- [X] T023 [P] [US2] Write unit tests for `MovementAntiCheatConsts` and `0x4B`/`0x4C`/`0x4E`/`0x71`/`0x50`/`0x51`/`0x53`/`0x54` packet symmetry in `tests/JXServer.Protocol.Tests/MovementAndPosturePacketTests.cs`, 18-Hz step progression & obstacle blocking tests in `tests/JXServer.ZoneEngine.Tests/MovementSimulationTests.cs`, and TCP walk/run/sit/anti-cheat integration tests in `tests/JXServer.Integration.Tests/MovementIntegrationTests.cs`

**Checkpoint**: User Story 2 complete — players and bots can walk, run, jump, and meditate with obstacle collision, anti-cheat verification, and dynamic `3x3` region boundary crossing.

---

## Phase 6: User Story 4 - Map Boundary Trap Detection & Seamless Map Transitions (Priority: P4)

**Goal**: Parse `Region_S.dat` Section `1` (`TRAP_SECTION`: 12B `KTrapFileHead` + 8B `KSPTrap` horizontal cell runs) and `script/**/trap/*.lua` (`ITrapScriptCatalog` indexed by `g_FileName2Id` hash), detect trap triggers on 18-Hz movement ticks with `LastTriggeredTrapId` edge-trigger anti-loop tracking, execute both Town Gate Fight-State traps (`FightMode` toggle + `SetPos` `0x4E`) and Cross-Map Portal traps (`NewWorld` with level-gate check, same-server `0x49` + `0x4E` + destination `3x3` AOI sync without `0x43`, or `0x35` cross-server exchange), and persist updated coordinates (`FR-008`, `FR-009`, `FR-012`, `SC-004`).

**Independent Test**: Walk a character onto a town gate trap and a cross-map portal trap in Map `53` (`Ba Lăng Huyện` $\rightarrow$ `Nam Nhạc Trấn` Map `54` or `Vũ Lăng Sơn` Map `70`) and verify seamless transition without dropping the session or re-triggering in a loop.

### Implementation & Tests for User Story 4

- [X] T024 [P] [US4] Create `RegionTrapGrid` (`int RegionX`, `int RegionY`, `uint GetTrapId(int cellX, int cellY)`) in `src/JXServer.GameAssets/WorldMaps/RegionTrapGrid.cs`, `PortalTrapBindingTemplate` (`uint TrapId`, `ushort SourceMapId`, `string ScriptPath`, `PortalTrapActionKind ActionKind`, `ushort TargetMapId`, `int TargetMpsX`, `int TargetMpsY`, `byte TargetFightMode`, `int AlternateMpsX`, `int AlternateMpsY`, `byte AlternateFightMode`, `ushort MinRequiredLevel`, `int BounceBackMpsX`, `int BounceBackMpsY`, `string? DeniedMessage`) in `src/JXServer.GameAssets/WorldMaps/PortalTrapBindingTemplate.cs`, and `ITrapScriptCatalog` in `src/JXServer.GameAssets/WorldMaps/ITrapScriptCatalog.cs`
- [X] T025 [US4] Implement `TrapScriptCatalog` (scanning `script/**/trap/*.lua`, computing `g_FileName2Id` hashes via `PakFilePathHasher.ComputeHash` on `\script\...` GBK paths, and extracting Town Gate `GetFightState`/`SetPos`/`SetFightState` and Cross-Map `GetLevel`/`NewWorld`/`SetPos`/`Talk` bindings) in `src/JXServer.GameAssets/WorldMaps/TrapScriptCatalog.cs`, extend `MapDescriptor` (`RegionTraps`, `uint GetTrapId(int mpsX, int mpsY)`) in `src/JXServer.GameAssets/WorldMaps/MapDescriptor.cs` and `MapCatalog` (parsing `Region_S.dat` Section `1` `TRAP_SECTION`: 12B `KTrapFileHead` + 8B `KSPTrap` horizontal cell runs `cX .. cX + cNumCell - 1`) in `src/JXServer.GameAssets/WorldMaps/MapCatalog.cs`, and register `ITrapScriptCatalog` in `src/JXServer.GameAssets/GameAssetsServiceCollectionExtensions.cs`
- [X] T026 [P] [US4] Create `AttachTransferredPlayerZoneCommand` (`PlayerZoneState Player`, `int ArrivalMpsX`, `int ArrivalMpsY`, `byte ArrivalFightMode`) in `src/JXServer.ZoneEngine/PlayerSessions/AttachTransferredPlayerZoneCommand.cs` and `NotifyPlayerExchangeS2CPacket` (`0x35`, `25B` with plain little-endian `Port` at `+0x15`) in `src/JXServer.Adapter.LegacyJX1/Packets/NotifyPlayerExchangeS2CPacket.cs`
- [X] T027 [US4] Extend `IPlayerOutboundSink` in `src/JXServer.ZoneEngine/PlayerSessions/IPlayerOutboundSink.cs` and `GameServerClientSession` in `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.cs` with `SendWorldMapTransitionSync` (updating session `MapId` and sending `0x49` `s2c_syncworld` + `0x4E` `s2c_syncnpcminplayer` without `0x43`) and `SendNotifyServerExchange` (`0x35`), extend `IZoneClusterRouter` and `ZoneClusterRouter` with `TryTransferPlayerAcrossMaps` in `src/JXServer.ZoneEngine/WorldZones/IZoneClusterRouter.cs` and `src/JXServer.ZoneEngine/WorldZones/ZoneClusterRouter.cs`, and implement 18-Hz trap evaluation (`LastTriggeredTrapId` edge trigger, Town Gate fight-state toggle, Cross-Map level check & transfer, and durable coordinate persistence on transition/detach via `ICharacterAppService.UpdateCharacterWorldSnapshotAsync`) in `src/JXServer.ZoneEngine/WorldZones/MapZoneWorker.cs`
- [X] T028 [P] [US4] Write unit tests for `RegionTrapGrid` (`TRAP_SECTION` binary parsing), `TrapScriptCatalog` (`g_FileName2Id` matching against Map `53` trap IDs), anti-loop `LastTriggeredTrapId`, and same-server map transition in `tests/JXServer.ZoneEngine.Tests/TrapAndMapTransitionTests.cs`, plus end-to-end TCP portal transition tests in `tests/JXServer.Integration.Tests/MapTransitionIntegrationTests.cs`

**Checkpoint**: User Story 4 complete — stepping on town gate traps toggles fight state and stepping on boundary portal traps seamlessly transitions characters across maps.

---

## Phase 7: User Story 5 - Multi-Channel Player Chat & System Broadcasts (Priority: P5)

**Goal**: Process channel subscription masks (`0x60` `c2s_chatsetchannel`), nearby spatial talk (`0x52` `c2s_playertalk` $\rightarrow$ `0x5B` `s2c_playertalk`), structured multi-channel chat (`0x56` `c2s_playersendchat` $\rightarrow$ `0x7E` `s2c_chatgetsomeone` across `Nearby`, `World`, `PrivateWhisper`, `Team`, and `Faction` channels), and server system messages (`0xFB` `s2c_extend`) with full Vietnamese text fidelity (`FR-010`, `FR-011`, `SC-005`).

**Independent Test**: Connect multiple clients/bots across Map `53` and Map `54`, exchange nearby talk (`0x52` $\rightarrow$ `0x5B`), world chat, faction chat, and private whisper (`0x56` $\rightarrow$ `0x7E`), and verify recipients receive accurate Vietnamese messages respecting their `0x60` subscription mask.

### Implementation & Tests for User Story 5

- [X] T029 [P] [US5] Create `UpdateChatChannelMaskZoneCommand` (`Guid CharacterId`, `long LeaseEpoch`, `uint SubscribedChannelMask`) in `src/JXServer.ZoneEngine/Chats/UpdateChatChannelMaskZoneCommand.cs`, `SendNearbyTalkZoneCommand` (`Guid SenderCharacterId`, `long LeaseEpoch`, `string MessageUtf8`) in `src/JXServer.ZoneEngine/Chats/SendNearbyTalkZoneCommand.cs`, `SendStructuredChatZoneCommand` (`Guid SenderCharacterId`, `long LeaseEpoch`, `ChatChannelKind Channel`, `uint TargetNpcId`, `string TargetOrPrefixUtf8`, `string MessageUtf8`) in `src/JXServer.ZoneEngine/Chats/SendStructuredChatZoneCommand.cs`, and `DeliverClusterChatZoneCommand` (`uint SenderNpcId`, `string SenderNameUtf8`, `ChatChannelKind Channel`, `uint TargetNpcId`, `string TargetRoleNameUtf8`, `int SenderFactionId`, `string MessageUtf8`) in `src/JXServer.ZoneEngine/Chats/DeliverClusterChatZoneCommand.cs`
- [X] T030 [P] [US5] Create C2S & S2C chat wire packet structs: `ChatSetChannelC2SPacket` (`0x60`, `5B` `<BI`) in `src/JXServer.Adapter.LegacyJX1/Packets/ChatSetChannelC2SPacket.cs`, `PlayerTalkC2SPacket` (`0x52`, variable `7 + MsgLen`) in `src/JXServer.Adapter.LegacyJX1/Packets/PlayerTalkC2SPacket.cs`, `PlayerTalkS2CPacket` (`0x5B`, variable `11 + MsgLen`) in `src/JXServer.Adapter.LegacyJX1/Packets/PlayerTalkS2CPacket.cs`, `PlayerSendChatC2SPacket` (`0x56`, variable `47 + SentenceLen`) in `src/JXServer.Adapter.LegacyJX1/Packets/PlayerSendChatC2SPacket.cs`, `ChatGetSomeoneS2CPacket` (`0x7E`, variable `37 + MsgLen`) in `src/JXServer.Adapter.LegacyJX1/Packets/ChatGetSomeoneS2CPacket.cs`, and `SystemInfoExtendS2CPacket` (`0xFB`, variable `17 + MsgLen` with header `0x00010032` and `TCVN3` prefix `"HÖ thèng"`) in `src/JXServer.Adapter.LegacyJX1/Packets/SystemInfoExtendS2CPacket.cs`
- [X] T031 [US5] Extend `IPlayerOutboundSink` in `src/JXServer.ZoneEngine/PlayerSessions/IPlayerOutboundSink.cs` and `GameServerClientSession` in `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.cs` with `SendPlayerTalkBroadcast` (`0x5B`), `SendChatMessage` (`0x7E`), and `SendSystemMessage` (`0xFB`), extend `IZoneClusterRouter` and `ZoneClusterRouter` with `RouteClusterChat(DeliverClusterChatZoneCommand chatCommand)` in `src/JXServer.ZoneEngine/WorldZones/IZoneClusterRouter.cs` and `src/JXServer.ZoneEngine/WorldZones/ZoneClusterRouter.cs`, and implement `3x3` AOI talk/nearby chat and cluster-wide `World`/`Whisper`/`Team`/`Faction`/`System` delivery respecting `SubscribedChatChannelMask` in `src/JXServer.ZoneEngine/WorldZones/MapZoneWorker.cs`
- [X] T032 [P] [US5] Implement `ChatSetChannelPacketHandler` (`0x60`) in `src/JXServer.Adapter.LegacyJX1/GameServer/ChatSetChannelPacketHandler.cs`, `PlayerTalkPacketHandler` (`0x52`) in `src/JXServer.Adapter.LegacyJX1/GameServer/PlayerTalkPacketHandler.cs`, and `PlayerSendChatPacketHandler` (`0x56`) in `src/JXServer.Adapter.LegacyJX1/GameServer/PlayerSendChatPacketHandler.cs`, and register them in `src/JXServer.Adapter.LegacyJX1/LegacyJX1AdapterServiceCollectionExtensions.cs`
- [X] T033 [P] [US5] Write unit tests for variable-length `0x52`, `0x5B`, `0x56`, `0x7E`, `0x60`, and `0xFB` packet serialization & `TCVN3` Vietnamese text round-tripping in `tests/JXServer.Protocol.Tests/ChatPacketTests.cs`, and multi-client TCP integration tests for `3x3` AOI speech, World chat, Faction chat, Private Whisper, and `0x60` channel filtering in `tests/JXServer.Integration.Tests/MultiChannelChatIntegrationTests.cs`

**Checkpoint**: User Story 5 complete — all spatial and cross-map chat channels operate with full Vietnamese `TCVN3` $\leftrightarrow$ `UTF-8` fidelity.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final verification of Constitution v1.1.0 gates, zero-warning compilation, and 50-bot high-density AOI/movement/chat benchmark validation (`SC-001`..`SC-005`).

- [X] T034 Write high-density 50-client concurrent AOI boundary crossing, movement, and chat benchmark test verifying 99th-percentile 18-Hz tick duration `< 15ms` (`SC-001`) in `tests/JXServer.ZoneEngine.Tests/HighDensityAoiBenchmarkTests.cs`
- [X] T035 Run `dotnet build JXServer.slnx -warnaserror` and `dotnet test JXServer.slnx` to verify 0 warnings, 100% test pass rate, and strict compliance with Constitution v1.1.0 (`One-Type-Per-File`, sealed classes, pluralized folders) across `d:\Projects\jx-server`

---

## Dependencies & Execution Order

### Phase Dependencies

1. **Phase 1 (Setup)**: No dependencies — starts immediately.
2. **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS** all user story phases.
3. **Phase 3 (`US3` — Static NPC & Monster Spawning)**: Depends on Phase 2 — loads static NPCs from `settings/npcs.txt`, `autoexec.lua`, and `Region_S.dat` Section `2` so `US1` has NPC entities to synchronize.
4. **Phase 4 (`US1` — Spatial 3x3 Region AOI & Neighbor Sync 🎯 MVP)**: Depends on Phase 2 & Phase 3 — establishes `SpatialRegionGrid` and `0x4C`/`0x4D`/`0x4E`/`0x48`/`0x49` synchronization.
5. **Phase 5 (`US2` — Obstacle-Validated Movement & Posture)**: Depends on Phase 4 (`US1`) — moves characters across `SpatialRegionGrid` and broadcasts `0x50`/`0x51`/`0x53`/`0x54` to the `3x3` AOI.
6. **Phase 6 (`US4` — Map Boundary Trap Detection & Map Transitions)**: Depends on Phase 5 (`US2`) — evaluates `RegionTrapGrid` on movement steps and transfers characters across `MapZoneWorker` shards.
7. **Phase 7 (`US5` — Multi-Channel Player Chat & System Broadcasts)**: Depends on Phase 4 (`US1`) — can be implemented in parallel with `US2`/`US4` once `SpatialRegionGrid` is in place.
8. **Phase 8 (Polish & Benchmark)**: Depends on Phases 3–7.

### Parallel Opportunities

- **Within Phase 2 (Foundational)**: `T002`, `T003`, `T004`, `T005`, `T006` touch independent files and can run in parallel.
- **Within Phase 4 (`US1`)**: `T012` (`SpatialRegionGrid`), `T013` (`EntityFullSyncSnapshot`/`RequestEntitySyncZoneCommand`), and `T014` (`0x48`/`0x49`/`0x4D` packet structs) can run in parallel.
- **Within Phase 5 (`US2`)**: `T018` (`*ZoneCommand` records), `T019` (`C2S` packet structs), and `T020` (`S2C` packet structs) can run in parallel.
- **Within Phase 6 (`US4`) & Phase 7 (`US5`)**: `US5` chat commands/packets (`T029`, `T030`) can be developed in parallel with `US4` trap catalogs/packets (`T024`, `T026`).

---

## Parallel Example: User Story 2 (Movement & Posture)

```bash
# Launch all ZoneCommand and Wire Packet tasks for User Story 2 in parallel:
Task T018: "Create MovePlayerZoneCommand, JumpPlayerZoneCommand, TogglePlayerSitZoneCommand in src/JXServer.ZoneEngine/PlayerSessions/"
Task T019: "Create NpcWalkC2SPacket, NpcRunC2SPacket, NpcJumpC2SPacket, NpcSitC2SPacket in src/JXServer.Adapter.LegacyJX1/Packets/"
Task T020: "Create NpcWalkS2CPacket, NpcRunS2CPacket, NpcJumpS2CPacket, NpcSitS2CPacket in src/JXServer.Adapter.LegacyJX1/Packets/"
```

---

## Implementation Strategy

### MVP First (`US3` + `US1`)

1. Complete **Phase 1 (Setup)** and **Phase 2 (Foundational)**.
2. Complete **Phase 3 (`US3` Static NPC Spawning)** and **Phase 4 (`US1` Spatial 3x3 Region AOI)**.
3. **STOP and VALIDATE**: Connect multiple clients/bots to Map `53` and verify that all static town NPCs (`Chưởng quầy tạp hóa`, `Xa Phu`, etc.) and neighboring players appear accurately via `0x4C`/`0x4D`/`0x4E`.

### Incremental Delivery

1. Add **Phase 5 (`US2` Movement & Posture)** $\rightarrow$ Validate walk/run/jump/sit, `IsCheckValueValid`, and `Obstacle_Full` blocking.
2. Add **Phase 6 (`US4` Portal Traps & Map Transitions)** $\rightarrow$ Validate town gate fight-state toggles and cross-map portal transitions (`53` $\rightarrow$ `54`/`70`).
3. Add **Phase 7 (`US5` Multi-Channel Chat)** $\rightarrow$ Validate `0x52` $\rightarrow$ `0x5B` speech bubbles and `0x56` $\rightarrow$ `0x7E` multi-channel chat.
4. Complete **Phase 8 (50-Bot Benchmark & Zero-Warning Gate)**.
