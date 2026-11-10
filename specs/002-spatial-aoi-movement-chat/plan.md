# Implementation Plan: Spatial 3x3 Region AOI, Movement, Map Transitions & Multi-Channel Chat

**Branch**: `002-spatial-aoi-movement-chat` | **Date**: 2026-11-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-spatial-aoi-movement-chat/spec.md`

## Summary

Deliver **Milestone 2 (`002-spatial-aoi-movement-chat`)** of the modern `.NET 10` (`net10.0`, C# 14) Võ Lâm Truyền Kỳ server (`JXServer`):
1. **Static NPC & Map Region Asset Pipeline (`JXServer.GameAssets`)**: Parse `settings/npcs.txt` (`INpcTemplateCatalog`), `script/global/autoexec.lua` (`adddialognpc`), `script/**/trap/*.lua` (`ITrapScriptCatalog` indexed by 32-bit `g_FileName2Id` hash), and binary `TRAP_SECTION` (Section `1`: 12B `KTrapFileHead` + 8B `KSPTrap` horizontal cell runs) and `NPC_SECTION` (Section `2`: 12B `KNpcFileHead` + `60 + nScriptNameLen`-byte `KSPNpc` records) inside each map region's `Region_S.dat` file (both unpacked `/maps` and UCL-compressed `pak/maps.pak`), resolving authentic Vietnamese display names (`TCVN3` $\rightarrow$ `UTF-8`).
2. **Spatial 3x3 Region Grid & Neighbor Synchronization (`JXServer.ZoneEngine`)**: Partition each `MapZoneWorker` into `SpatialRegionSlot` instances (`512 x 1024` Mps / `16 x 32` cells), compute entering/leaving `3x3` region set differences on world entry and boundary crossings, broadcast full entity appearance (`0x4C` `s2c_syncnpc`, `61B`) and periodic compact updates (`0x4D` `s2c_syncnpcmin`, `18B`; `0x4E` `s2c_syncnpcminplayer`, `13B`), and answer on-demand entity detail queries (`0x48` `c2s_requestplayer`, `0x49` `c2s_requestnpc`).
3. **Obstacle-Validated 18-Hz Movement, Anti-Cheat & Posture (`JXServer.ZoneEngine` & `JXServer.Adapter.LegacyJX1`)**: Enforce `KPlayer::IsCheckValueValid` (`0x24A1` for Walk `0x4B`, `0x1D78` for Run `0x4C`) against `c2s_syncend` tokens, broadcast `s2c_npcwalk` (`0x50`) / `s2c_npcrun` (`0x51`) / `s2c_npcjump` (`0x54`) / `s2c_npcsit` (`0x53`), step moving characters across 18-Hz simulation ticks while halting at `Obstacle_Full (1)` cells, and regenerate HP/MP (`0x46`) while seated (`do_sit`).
4. **Edge-Triggered Portal Traps & Seamless Map Transitions**: Evaluate `RegionTrapGrid` cell trap IDs each movement tick with `LastTriggeredTrapId` anti-loop protection, executing both **Town Gate Fight-State Traps** (`FightMode` toggle + `SetPos` via `0x4E`) and **Cross-Map Portal Traps** (`NewWorld` level-gate check, in-memory same-server worker transfer via `0x49` + `0x4E` + destination `3x3` AOI sync without `0x43`, or `0x35` `s2c_notifyplayerexchange` for external nodes), and persist updated coordinates via `ICharacterAppService.UpdateCharacterWorldSnapshotAsync`.
5. **Multi-Channel Player Chat & System Broadcasts**: Validate variable-length chat frames (`0x52`, `0x56`) in `ProtocolSizeTable`, broadcast nearby spatial speech (`0x52` $\rightarrow$ `0x5B`) and Nearby chat (`Channel 2`/`7` $\rightarrow$ `0x7E`) within the `3x3` region AOI, route `World` (`1`), `Whisper` (`3`), `Team` (`4`), and `Faction` (`5`) chat across all active `MapZoneWorker` shards via `IZoneClusterRouter` respecting `0x60` (`c2s_chatsetchannel`) masks, and emit server system announcements (`0xFB`).

## Technical Context

**Language/Version**: C# 14 on .NET 10 (`net10.0`), XML solution (`JXServer.slnx`), `Directory.Build.props` (`Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `AllowUnsafeBlocks=true`), `Directory.Packages.props` (Central Package Management).

**Primary Dependencies**:
- `Microsoft.Extensions.Hosting` (`10.0.0`)
- `System.IO.Pipelines` (`10.0.0`)
- `System.Text.Encoding.CodePages` (`10.0.0`)
- `Microsoft.EntityFrameworkCore.SqlServer` (`10.0.0`) & `Microsoft.EntityFrameworkCore.Sqlite` (`10.0.0`)
- `Microsoft.Extensions.TimeProvider.Testing` (`10.0.0`) & `xunit.v3` (`2.0.0`)

**Storage**: Relational database via EF Core 10 (**Microsoft SQL Server** for production; **SQLite WAL / InMemory** for development and automated tests) + read-only original file system / `.pak` assets (`d:\Projects\jx-linux-bin\server1`).

**Testing**: `xunit.v3` unit tests, protocol byte-symmetry tests, deterministic `FakeTimeProvider` 18-Hz zone simulation tests, and end-to-end multi-client TCP integration tests (plus live 50-bot `JXSwarm.Drone.Host` validation).

**Target Platform**: Cross-platform .NET 10 Server (Windows Server / Windows 11 & Linux x64/ARM64).

**Project Type**: Modular Monolith to Distributed Cluster MMORPG Server (`JXServer.slnx`).

**Performance Goals**:
- Maintain `18-Hz` (`~55.5ms` tick) simulation loop with 99th-percentile tick duration `< 15ms` under 50 concurrent moving/chatting bots and static NPCs in Map `53`.
- `< 500ms` end-to-end portal trap map transition latency; `< 200ms` multi-channel chat delivery latency.

**Constraints**:
- Zero compiler warnings (`-warnaserror`), strict `One-Type-Per-File` rule, pluralized feature folders, sealed concrete classes, ABP DDD role suffixes, and the 5 Real-Time 18-Hz Game Server naming conventions (`*C2SPacket`/`*S2CPacket`, `*ZoneState`/`*ZoneCommand`, unit-explicit `*MpsX`/`*CellX`/`*RegionX`, `*Template`/`I*Catalog`, `*LogMessages`).
- Zero blocking I/O or `DbContext` access inside the 18-Hz `MapZoneWorker` loop (coordinate persistence on map transition/disconnect runs asynchronously via `ICharacterAppService`).

**Scale/Scope**: Milestone 2 covers `3x3` region AOI visibility, static NPC/monster spawning (`npcs.txt` + `Region_S.dat` + `autoexec.lua`), walk/run/jump/sit movement with `IsCheckValueValid` and obstacle collision, portal trap map transitions (`script/**/trap/*.lua` + `Region_S.dat`), and multi-channel player chat (`0x48`, `0x49`, `0x4B`, `0x4C`, `0x4D`, `0x4E`, `0x50`, `0x51`, `0x52`, `0x53`, `0x54`, `0x56`, `0x5B`, `0x60`, `0x71`, `0x7E`, `0xFB`, `0x35`).

## Constitution Check

*GATE: Passed before Phase 0 research and re-verified after Phase 1 design.*

| Constitution Principle / Gate | Status | Verification Evidence in Design |
| :--- | :--- | :--- |
| **I. Hybrid Distributed Control Plane & Sharded 18-Hz Zone Architecture** | **PASS** | Each `MapZoneWorker` owns its `SpatialRegionGrid`, `NpcZoneState` instances, and `PlayerZoneState` slots on a single-threaded 18-Hz loop; cross-map portals and global chat route lock-free through `IZoneClusterRouter`. |
| **II. Multi-Protocol Transport Abstraction** | **PASS** | Variable-length framing (`0x52`, `0x56`), anti-cheat `dwCheckValue` parsing, and `TCVN3` packet serialization are confined to `JXServer.Adapter.LegacyJX1`, communicating with `JXServer.ZoneEngine` via `IZoneCommand` and `IPlayerOutboundSink`. |
| **III. Zero-Hallucination Evidence & Ground-Truth Fidelity** | **PASS** | Exact `Region_S.dat` binary layouts (`KTrapFileHead` + `KSPTrap` `8B`; `KNpcFileHead` + `KSPNpc` `60B + nScriptNameLen`), `0x50` vs. `0x51` field-order quirk, `IsCheckValueValid` keys (`0x24A1`, `0x1D78`), and same-server `0x49`+`0x4E` transition rules are verified in [research.md](research.md) and [contracts/](contracts/). |
| **IV. Zero-Dupe Economy & Lease-Fenced Session Integrity** | **PASS** | Every `MovePlayerZoneCommand`, `JumpPlayerZoneCommand`, `TogglePlayerSitZoneCommand`, chat command, and `UpdateCharacterWorldSnapshotDto` carries and verifies the player's monotonic `LeaseEpoch`. |
| **V. UTF-8 Domain Core & Boundary-Only Legacy Transcoding** | **PASS** | `NpcTemplateCatalog`, `MapCatalog`, and chat packet handlers transcode `TCVN3`/`GBK` $\leftrightarrow$ `UTF-8` strictly at the asset/network boundary; all `NpcZoneState`, `PlayerZoneState`, and chat commands use UTF-8 `string`. |
| **VI. Clean Modular Boundaries & ABP DDD Role Separation** | **PASS** | Strictly enforces inward dependencies, `One-Type-Per-File`, sealed concrete classes, pluralized folders, and the 5 Real-Time 18-Hz Game Server naming conventions (`*C2SPacket`/`*S2CPacket`, `*ZoneState`, `*ZoneCommand`, `*MpsX`/`*CellX`/`*RegionX`, `*Template`/`I*Catalog`). |

## Project Structure

### Documentation (this feature)

```text
specs/002-spatial-aoi-movement-chat/
├── plan.md                                  # This file (/speckit-plan output)
├── research.md                              # Phase 0 ground-truth binary & C++ research
├── data-model.md                            # Phase 1 ABP DDD & 18-Hz Zone data model
├── quickstart.md                            # Phase 1 build, test, and JX-Swarm verification guide
├── checklists/
│   └── requirements.md                      # Specification quality checklist (16/16 passed)
└── contracts/
    ├── aoi-and-movement-protocol.md         # Port 6666 AOI, Movement & Map Transition wire contract
    ├── chat-protocol.md                     # Port 6666 Multi-Channel Chat & System message wire contract
    └── zone-spatial-and-chat-ports.md       # Internal C# 14 Catalog, Router & OutboundSink ports
```

### Source Code (repository root `d:\Projects\jx-server`)

```text
JXServer.slnx
├── src/
│   ├── JXServer.Primitives/                 # Tier 1: Domain.Shared / Zero-dependency primitives
│   │   ├── Chats/                           # [NEW] ChatChannelKind, ChatConsts
│   │   └── WorldZones/                      # [UPDATED] EntityDoingPosture, EntityCamp, MovementAntiCheatConsts, PortalTrapActionKind
│   │
│   ├── JXServer.GameAssets/                 # Tier 2a: Static asset loaders & read-only catalogs
│   │   ├── Logging/                         # [UPDATED] GameAssetsLogMessages
│   │   ├── NpcTemplates/                    # [NEW] NpcTemplate, StaticNpcSpawnTemplate, INpcTemplateCatalog, NpcTemplateCatalog
│   │   ├── WorldMaps/                       # [UPDATED] MapDescriptor, MapCatalog, RegionTrapGrid, PortalTrapBindingTemplate, ITrapScriptCatalog, TrapScriptCatalog
│   │   └── GameAssetsServiceCollectionExtensions.cs
│   │
│   ├── JXServer.Contracts/                  # Tier 3a: ABP Application.Contracts
│   │   └── Characters/                      # [UPDATED] ICharacterAppService, UpdateCharacterPositionInput
│   │
│   ├── JXServer.Domain/                     # Tier 3b: ABP DDD Domain
│   │   └── Characters/                      # [UPDATED] Character (UpdatePosition method)
│   │
│   ├── JXServer.ZoneEngine/                 # Tier 2b: Sharded 18-Hz Deterministic Simulation Engine
│   │   ├── Chats/                           # [NEW] UpdateChatChannelMaskZoneCommand, SendNearbyTalkZoneCommand, SendStructuredChatZoneCommand, DeliverClusterChatZoneCommand
│   │   ├── Logging/                         # [UPDATED] ZoneEngineLogMessages
│   │   ├── NpcEntities/                     # [NEW] NpcZoneState
│   │   ├── PlayerSessions/                  # [UPDATED] PlayerZoneState, IPlayerOutboundSink, EntityFullSyncSnapshot, MovePlayerZoneCommand, JumpPlayerZoneCommand, TogglePlayerSitZoneCommand, RequestEntitySyncZoneCommand, AttachTransferredPlayerZoneCommand
│   │   ├── SpatialGrid/                     # [NEW] SpatialRegionSlot, SpatialRegionGrid
│   │   └── WorldZones/                      # [UPDATED] IZoneClusterRouter, ZoneClusterRouter, MapZoneWorker, ZoneEngineHostedService
│   │
│   ├── JXServer.Application/                # Tier 3c: ABP DDD Application Use-Case Orchestrators
│   │   └── Characters/                      # [UPDATED] CharacterAppService (UpdateCharacterPositionAsync)
│   │
│   ├── JXServer.Adapter.LegacyJX1/          # Tier 4a: Legacy TCP 5622 & 6666 Adapter
│   │   ├── Framing/                         # [UPDATED] ProtocolSizeTable (variable-length 0x52, 0x56 validation)
│   │   ├── GameServer/                      # [UPDATED] GameServerClientSession + 9 new *PacketHandler classes (RequestPlayer, RequestNpc, NpcWalk, NpcRun, NpcJump, NpcSit, PlayerTalk, PlayerSendChat, ChatSetChannel)
│   │   ├── Logging/                         # [UPDATED] LegacyAdapterLogMessages
│   │   ├── Packets/                         # [UPDATED] ProtocolOpcodes + 18 new *C2SPacket / *S2CPacket structs (one type per file)
│   │   └── LegacyJX1AdapterServiceCollectionExtensions.cs
│   │
│   └── JXServer.Host/                       # Tier 4b: Executable .NET 10 Server Host
│       └── appsettings.json                 # [UPDATED] ConfiguredMapIds (53, 54, 70, 74, 199)
│
└── tests/
    ├── JXServer.Protocol.Tests/             # [UPDATED] Anti-cheat checksum, variable-length chat framing, and M2 packet symmetry tests
    ├── JXServer.ZoneEngine.Tests/           # [UPDATED] NpcTemplateCatalog, TrapScriptCatalog, Region_S.dat NPC/Trap parsing, 3x3 AOI diff, 18-Hz movement obstacle & portal transition tests
    └── JXServer.Integration.Tests/          # [UPDATED] Multi-client TCP 6666 AOI discovery, walk/run broadcast, trap map transition, and multi-channel chat integration tests
```

**Structure Decision**: Extends the existing 9-project modular architecture established in Milestone 1 (`JXServer.slnx`), adding dedicated pluralized feature folders (`Chats/`, `NpcTemplates/`, `NpcEntities/`, `SpatialGrid/`) while preserving strict `One-Type-Per-File` and inward dependency boundaries.

## Complexity Tracking

> **No Constitution Check violations exist.** All gates pass without exception.
