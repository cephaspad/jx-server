# Phase 1 Data Model: Spatial 3x3 Region AOI, Movement, Map Transitions & Multi-Channel Chat

**Feature Branch**: `002-spatial-aoi-movement-chat`  
**Date**: 2026-11-09  
**Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

All types strictly follow the **JX-Server Constitution v1.1.0**:
- **One-Type-Per-File** and **Pluralized Domain/Feature Folders**.
- **Sealed Concrete Classes** (`public sealed class` / `public readonly record struct`) unless designed as an abstract base.
- **5 Real-Time 18-Hz Game Server Naming Conventions**:
  1. Directional wire packets (`*C2SPacket`, `*S2CPacket`).
  2. In-memory 18-Hz simulation state (`*ZoneState`) & channel commands (`*ZoneCommand`).
  3. Unit-explicit spatial suffixes (`*MpsX`, `*MpsY`, `*CellX`, `*CellY`, `*RegionX`, `*RegionY`).
  4. Static asset templates (`*Template`) & catalogs (`I*Catalog`, `*Catalog`).
  5. High-performance source-generated logging (`*LogMessages`).

---

## 1. Tier 1: `JXServer.Primitives` (Domain.Shared Constants & Enums)

### `JXServer.Primitives/WorldZones/`
| Type Name | File Path | Kind | Fields / Values |
| :--- | :--- | :--- | :--- |
| `EntityDoingPosture` | `WorldZones/EntityDoingPosture.cs` | `enum : byte` | `Stand = 1` (`do_stand`), `Walk = 2` (`do_walk`), `Run = 3` (`do_run`), `Sit = 4` (`do_sit`), `Jump = 5` (`do_jump`), `Attack = 6` (`do_attack`), `Skill = 7` (`do_skill`), `Death = 11` (`do_death`) |
| `EntityCamp` | `WorldZones/EntityCamp.cs` | `enum : byte` | `Begin = 0` (`camp_begin`), `Justice = 1` (`camp_justice`), `Evil = 2` (`camp_evil`), `Balance = 3` (`camp_balance`), `Free = 4` (`camp_free`), `Animal = 5` (`camp_animal`), `Event = 6` (`camp_event`) |
| `MovementAntiCheatConsts` | `WorldZones/MovementAntiCheatConsts.cs` | `static class` | `WalkCheckKey = 0x24A1u`, `RunCheckKey = 0x1D78u`, `SkillCheckKey = 0x3AB9u`, `TradeStartCheckKey = 0x451Fu`, `DialogNpcCheckKey = 0x506Cu`, `TradeReplyCheckKey = 0x67CAu`, `IsValidCheckValue(uint checkValue, uint token1, uint token2, uint key)` |
| `PortalTrapActionKind` | `WorldZones/PortalTrapActionKind.cs` | `enum : byte` | `ToggleFightStateAndSetPos = 1`, `ChangeMapPortal = 2` |

### `JXServer.Primitives/Chats/`
| Type Name | File Path | Kind | Fields / Values |
| :--- | :--- | :--- | :--- |
| `ChatChannelKind` | `Chats/ChatChannelKind.cs` | `enum : byte` | `All = 0`, `World = 1`, `Nearby = 2`, `PrivateWhisper = 3`, `Team = 4`, `Faction = 5`, `Tong = 6`, `Screen = 7`, `System = 8` |
| `ChatConsts` | `Chats/ChatConsts.cs` | `static class` | `MaxTalkMessageBytes = 256`, `MaxChatSentenceBytes = 256`, `SenderNameBufferLength = 32`, `SentencePrefixBufferLength = 32`, `DefaultSubscribedChannelMask = 0xFFFFFFFFu`, `SystemExtendHeader = 0x00010032u` |

---

## 2. Tier 2a: `JXServer.GameAssets` (Static Templates, Traps & Map Region Data)

### `JXServer.GameAssets/NpcTemplates/`
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `NpcTemplate` | `NpcTemplates/NpcTemplate.cs` | `sealed record` | Read-only archetype parsed from `settings/npcs.txt` (0-based `TemplateId` matching `KSPNpc.nTemplateID`): `int TemplateId`, `string Name` (UTF-8 from `TCVN3`), `EntityKind Kind`, `EntityCamp Camp`, `ElementSeries Series`, `int WalkSpeed` (default `10`), `int RunSpeed` (default `20`), `int BaseLifeMax`, `int DialogRadius` |
| `StaticNpcSpawnTemplate` | `NpcTemplates/StaticNpcSpawnTemplate.cs` | `sealed record` | Represents a static NPC placement parsed from either `Region_S.dat` (`NPC_SECTION` `KSPNpc`) or `script/global/autoexec.lua` (`adddialognpc`): `int TemplateId`, `ushort MapId`, `int SpawnMpsX`, `int SpawnMpsY`, `short Level`, `EntityKind Kind`, `EntityCamp Camp`, `ElementSeries Series`, `bool IsSpecialNpc`, `string DisplayName` (UTF-8), `string ScriptPath` (UTF-8) |
| `INpcTemplateCatalog` | `NpcTemplates/INpcTemplateCatalog.cs` | `interface` | `bool TryGetTemplate(int templateId, out NpcTemplate? template)`, `IReadOnlyList<StaticNpcSpawnTemplate> GetAutoExecSpawnsForMap(ushort mapId)` |
| `NpcTemplateCatalog` | `NpcTemplates/NpcTemplateCatalog.cs` | `sealed class` | Loads `settings/npcs.txt` (`TCVN3` $\rightarrow$ `UTF-8`, skipping header row 1 and baseline row 2 so data row index `0..N-1` maps to `TemplateId`) and parses `script/global/autoexec.lua` (`adddialognpc` entries, converting `(cellX, cellY)` to `(cellX * 32, cellY * 32)`). |

### `JXServer.GameAssets/WorldMaps/` (Extensions & New Types)
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `RegionTrapGrid` | `WorldMaps/RegionTrapGrid.cs` | `sealed class` | Holds the `uint[16, 32]` cell trap matrix (`uTrapId`) parsed from `Region_S.dat` Section `1` (`TRAP_SECTION`: 12B `KTrapFileHead` + 8B `KSPTrap` horizontal runs `cX .. cX + cNumCell - 1`). Provides `uint GetTrapId(int cellX, int cellY)`. |
| `PortalTrapBindingTemplate` | `WorldMaps/PortalTrapBindingTemplate.cs` | `sealed record` | Maps a 32-bit `TrapId` (`g_FileName2Id` hash) to its execution parameters: `uint TrapId`, `ushort SourceMapId`, `string ScriptPath`, `PortalTrapActionKind ActionKind`, `ushort TargetMapId`, `int TargetMpsX`, `int TargetMpsY`, `byte TargetFightMode`, `int AlternateMpsX`, `int AlternateMpsY`, `byte AlternateFightMode`, `ushort MinRequiredLevel`, `int BounceBackMpsX`, `int BounceBackMpsY`, `string? DeniedMessage` |
| `ITrapScriptCatalog` | `WorldMaps/ITrapScriptCatalog.cs` | `interface` | `bool TryGetTrapBinding(uint trapId, out PortalTrapBindingTemplate? binding)` |
| `TrapScriptCatalog` | `WorldMaps/TrapScriptCatalog.cs` | `sealed class` | Scans `script/**/trap/*.lua` on startup, hashes `\script\...` in GBK via `PakFilePathHasher.ComputeHash`, and extracts `SetPos`, `SetFightState`, `NewWorld`, `GetLevel`, and `Talk` parameters into `PortalTrapBindingTemplate` records. |
| `MapDescriptor` *(Updated)* | `WorldMaps/MapDescriptor.cs` | `sealed class` | Extended with `IReadOnlyDictionary<(int RegionX, int RegionY), RegionTrapGrid> RegionTraps`, `IReadOnlyList<StaticNpcSpawnTemplate> RegionNpcSpawns`, and `uint GetTrapId(int mpsX, int mpsY)`. |
| `MapCatalog` *(Updated)* | `WorldMaps/MapCatalog.cs` | `sealed class` | Extended `ParseRegionSections` to parse Section `0` (`OBSTACLE_SECTION`), Section `1` (`TRAP_SECTION` $\rightarrow$ `RegionTrapGrid`), and Section `2` (`NPC_SECTION` $\rightarrow$ `StaticNpcSpawnTemplate` list, resolving Vietnamese names via `INpcTemplateCatalog`). |

---

## 3. Tier 3: `JXServer.Contracts`, `JXServer.Domain`, `JXServer.Application` (Position Persistence)

### `JXServer.Contracts/Characters/` & `JXServer.Application/Characters/`
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `UpdateCharacterWorldSnapshotDto` *(Updated)* | `JXServer.Contracts/Characters/UpdateCharacterWorldSnapshotDto.cs` | `sealed record` | `Guid CharacterId`, `long ExpectedLeaseEpoch`, `ushort MapId`, `int MapMpsX`, `int MapMpsY`, `int CurrentLife`, `int CurrentMana`, `ushort CurrentStamina`, `byte FightMode` |
| `ICharacterAppService` *(Updated)* | `JXServer.Contracts/Characters/ICharacterAppService.cs` | `interface` | Adds `Task<bool> UpdateCharacterWorldSnapshotAsync(UpdateCharacterWorldSnapshotDto input, CancellationToken cancellationToken = default)` for durable coordinate and vital persistence on map transition and disconnect (`FR-012`). |

---

## 4. Tier 2b: `JXServer.ZoneEngine` (Sharded 18-Hz Spatial AOI, NPC, Movement & Chat State)

### `JXServer.ZoneEngine/SpatialGrid/`
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `SpatialRegionSlot` | `SpatialGrid/SpatialRegionSlot.cs` | `sealed class` | Represents one `(RegionX, RegionY)` (`512 x 1024` Mps) partition inside a `MapZoneWorker`: `int RegionX`, `int RegionY`, `List<PlayerZoneState> Players`, `List<NpcZoneState> Npcs`. Single-threaded access within the owning `MapZoneWorker`. |
| `SpatialRegionGrid` | `SpatialGrid/SpatialRegionGrid.cs` | `sealed class` | Manages the 2D dictionary/array of `SpatialRegionSlot` for a map zone, computes `3x3` region neighborhoods (`GetSurroundingRegions(int regionX, int regionY, Span<(int Rx, int Ry)> buffer)`), and computes entering/leaving region set differences (`ComputeEnteringRegions(int oldRx, int oldRy, int newRx, int newRy, Span<(int Rx, int Ry)> buffer)`). |

### `JXServer.ZoneEngine/NpcEntities/`
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `NpcZoneState` | `NpcEntities/NpcZoneState.cs` | `sealed class` | In-memory 18-Hz state of a static NPC or monster (`NpcId` in `1..9999`): `uint NpcId`, `int TemplateId`, `string Name`, `short Level`, `EntityKind Kind`, `EntityCamp Camp`, `ElementSeries Series`, `bool IsSpecialNpc`, `ushort MapId`, `int MpsX`, `int MpsY`, `int RegionX`, `int RegionY`, `EntityDoingPosture Doing`, `byte Dir`, `int WalkSpeed`, `int RunSpeed`, `uint CurLife`, `uint MaxLife`, `string ScriptPath`, `uint PackedNpcSettingIdx => ((uint)(ushort)Level << 16) | (uint)(ushort)TemplateId`. |

### `JXServer.ZoneEngine/PlayerSessions/` (Updated & New ZoneCommands)
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `PlayerZoneState` *(Updated)* | `PlayerSessions/PlayerZoneState.cs` | `sealed class` | Extended with movement, trap, and chat fields: `int TargetMpsX`, `int TargetMpsY`, `EntityDoingPosture Doing` (replaces raw byte), `int WalkSpeed` (`10`), `int RunSpeed` (`20`), `uint LastTriggeredTrapId`, `uint SubscribedChatChannelMask` (`0xFFFFFFFFu`), `long LastSitRegenTick`. |
| `IPlayerOutboundSink` *(Updated)* | `PlayerSessions/IPlayerOutboundSink.cs` | `interface` | Extended with methods to emit `SendNpcFullSync(in EntityFullSyncSnapshot snapshot)`, `SendNpcCompactSync(uint npcId, int mpsX, int mpsY, EntityDoingPosture doing, byte dir)`, `SendSelfPositionSync(uint playerNpcId, int mpsX, int mpsY, EntityDoingPosture doing, byte dir)`, `SendWalkBroadcast(uint npcId, int destMpsX, int destMpsY)`, `SendRunBroadcast(uint npcId, int destMpsX, int destMpsY)`, `SendJumpBroadcast(uint npcId, int destMpsX, int destMpsY)`, `SendSitBroadcast(uint npcId)`, `SendNormalVitalSync(uint playerNpcId, short curLife, short curMana, short curStamina)`, `SendWorldMapTransitionSync(ushort mapId, string mapName, int regionX, int regionY, int mpsX, int mpsY, EntityDoingPosture doing, byte dir)`, `SendNotifyServerExchange(uint ipAddressLittleEndian, ushort port, Guid handoffTicketId)`, `SendPlayerTalkBroadcast(uint talkerNpcId, string messageUtf8)`, `SendChatMessage(uint senderNpcId, string senderNameUtf8, string messageUtf8)`, and `SendSystemMessage(string messageUtf8)`. |
| `EntityFullSyncSnapshot` | `PlayerSessions/EntityFullSyncSnapshot.cs` | `readonly record struct` | Zero-allocation snapshot passed to `IPlayerOutboundSink.SendNpcFullSync` to serialize `0x4C` (`s2c_syncnpc`): `uint NpcId`, `int MpsX`, `int MpsY`, `uint NpcSettingIdx`, `ElementSeries Series`, `byte LifePer`, `byte Doing`, `byte CurrentCamp`, `byte Dir`, `EntityKind Kind`, `bool IsSpecialNpc`, `string NameUtf8`. |
| `MovePlayerZoneCommand` | `PlayerSessions/MovePlayerZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `int TargetMpsX`, `int TargetMpsY`, `bool IsRun`. |
| `JumpPlayerZoneCommand` | `PlayerSessions/JumpPlayerZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `int TargetMpsX`, `int TargetMpsY`. |
| `TogglePlayerSitZoneCommand` | `PlayerSessions/TogglePlayerSitZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `bool IsSitting`. |
| `RequestEntitySyncZoneCommand` | `PlayerSessions/RequestEntitySyncZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid RequesterCharacterId`, `long LeaseEpoch`, `uint TargetNpcId`, `bool IsPlayerRequest`. |
| `AttachTransferredPlayerZoneCommand` | `PlayerSessions/AttachTransferredPlayerZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: Transfers an active `PlayerZoneState` onto a destination `MapZoneWorker` during a same-server portal trap transition and triggers `0x49` + `0x4E` + `3x3` AOI sync. |

### `JXServer.ZoneEngine/Chats/`
| Type Name | File Path | Kind | Responsibilities & Fields |
| :--- | :--- | :--- | :--- |
| `UpdateChatChannelMaskZoneCommand` | `Chats/UpdateChatChannelMaskZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `uint SubscribedChannelMask` (`0x60`). |
| `SendNearbyTalkZoneCommand` | `Chats/SendNearbyTalkZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid SenderCharacterId`, `long LeaseEpoch`, `string MessageUtf8` (`0x52` $\rightarrow$ `0x5B` to `3x3` AOI). |
| `SendStructuredChatZoneCommand` | `Chats/SendStructuredChatZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: `Guid SenderCharacterId`, `long LeaseEpoch`, `ChatChannelKind Channel`, `uint TargetNpcId`, `string TargetOrPrefixUtf8`, `string MessageUtf8` (`0x56` $\rightarrow$ `0x7E`). |
| `DeliverClusterChatZoneCommand` | `Chats/DeliverClusterChatZoneCommand.cs` | `sealed record` | Implements `IZoneCommand`: Dispatched by `IZoneClusterRouter` to active `MapZoneWorker` shards for `World`, `Whisper`, `Team`, `Faction`, and `System` broadcasts. |

---

## 5. Tier 4a: `JXServer.Adapter.LegacyJX1` (Wire Packets & Handlers)

### `JXServer.Adapter.LegacyJX1/Packets/` (One-Type-Per-File `readonly record struct`)
| Packet Struct Name | Opcode | Direction | Wire Size | File Path |
| :--- | :--- | :--- | :--- | :--- |
| `RequestPlayerC2SPacket` | `0x48` | `C2S` | `5B` (`<BI`) | `Packets/RequestPlayerC2SPacket.cs` |
| `RequestNpcC2SPacket` | `0x49` | `C2S` | `5B` (`<BI`) | `Packets/RequestNpcC2SPacket.cs` |
| `NpcWalkC2SPacket` | `0x4B` | `C2S` | `13B` (`<BiiI`) | `Packets/NpcWalkC2SPacket.cs` |
| `NpcRunC2SPacket` | `0x4C` | `C2S` | `13B` (`<BiiI`) | `Packets/NpcRunC2SPacket.cs` |
| `NpcJumpC2SPacket` | `0x4E` | `C2S` | `13B` (`<BiiI`) | `Packets/NpcJumpC2SPacket.cs` |
| `NpcSitC2SPacket` | `0x71` | `C2S` | `2B` (`<BB`) | `Packets/NpcSitC2SPacket.cs` |
| `PlayerTalkC2SPacket` | `0x52` | `C2S` | `7 + N` (`<BHI...`) | `Packets/PlayerTalkC2SPacket.cs` |
| `PlayerSendChatC2SPacket` | `0x56` | `C2S` | `47 + N` | `Packets/PlayerSendChatC2SPacket.cs` |
| `ChatSetChannelC2SPacket` | `0x60` | `C2S` | `5B` (`<BI`) | `Packets/ChatSetChannelC2SPacket.cs` |
| `SyncNpcMinS2CPacket` | `0x4D` | `S2C` | `18B` | `Packets/SyncNpcMinS2CPacket.cs` |
| `NpcWalkS2CPacket` | `0x50` | `S2C` | `13B` (`<BIii`) | `Packets/NpcWalkS2CPacket.cs` |
| `NpcRunS2CPacket` | `0x51` | `S2C` | `13B` (`<BiIi`) | `Packets/NpcRunS2CPacket.cs` |
| `NpcSitS2CPacket` | `0x53` | `S2C` | `5B` (`<BI`) | `Packets/NpcSitS2CPacket.cs` |
| `NpcJumpS2CPacket` | `0x54` | `S2C` | `13B` (`<BiiI`) | `Packets/NpcJumpS2CPacket.cs` |
| `PlayerTalkS2CPacket` | `0x5B` | `S2C` | `11 + N` | `Packets/PlayerTalkS2CPacket.cs` |
| `ChatGetSomeoneS2CPacket` | `0x7E` | `S2C` | `37 + N` | `Packets/ChatGetSomeoneS2CPacket.cs` |
| `SystemInfoExtendS2CPacket` | `0xFB` | `S2C` | `17 + N` | `Packets/SystemInfoExtendS2CPacket.cs` |
| `NotifyPlayerExchangeS2CPacket` | `0x35` | `S2C` | `25B` | `Packets/NotifyPlayerExchangeS2CPacket.cs` |

*(Note: `SyncWorldS2CPacket` (`0x49`, `34B`), `SyncNpcS2CPacket` (`0x4C`, `61B`), `SyncNpcMinPlayerS2CPacket` (`0x4E`, `13B`), and `SyncCurPlayerNormalS2CPacket` (`0x46`, `14B`) already exist in `JXServer.Adapter.LegacyJX1/Packets/` from Milestone 1 and are reused directly.)*

### `JXServer.Adapter.LegacyJX1/GameServer/` (Packet Handlers)
| Handler Name | Handled Opcode | File Path |
| :--- | :--- | :--- |
| `RequestPlayerPacketHandler` | `0x48` (`c2s_requestplayer`) | `GameServer/RequestPlayerPacketHandler.cs` |
| `RequestNpcPacketHandler` | `0x49` (`c2s_requestnpc`) | `GameServer/RequestNpcPacketHandler.cs` |
| `NpcWalkPacketHandler` | `0x4B` (`c2s_npcwalk`) | `GameServer/NpcWalkPacketHandler.cs` |
| `NpcRunPacketHandler` | `0x4C` (`c2s_npcrun`) | `GameServer/NpcRunPacketHandler.cs` |
| `NpcJumpPacketHandler` | `0x4E` (`c2s_npcjump`) | `GameServer/NpcJumpPacketHandler.cs` |
| `NpcSitPacketHandler` | `0x71` (`c2s_npcsit`) | `GameServer/NpcSitPacketHandler.cs` |
| `PlayerTalkPacketHandler` | `0x52` (`c2s_playertalk`) | `GameServer/PlayerTalkPacketHandler.cs` |
| `PlayerSendChatPacketHandler` | `0x56` (`c2s_playersendchat`) | `GameServer/PlayerSendChatPacketHandler.cs` |
| `ChatSetChannelPacketHandler` | `0x60` (`c2s_chatsetchannel`) | `GameServer/ChatSetChannelPacketHandler.cs` |
