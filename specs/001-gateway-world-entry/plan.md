# Implementation Plan: Foundational Gateway Authentication, Character Lifecycle & World Entry

**Branch**: `001-gateway-world-entry` | **Date**: 2026-11-08 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-gateway-world-entry/spec.md`

## Summary

Deliver **Milestone 1 (`001-gateway-world-entry`)** of the modern `.NET 10` (`net10.0`, C# 14) Võ Lâm Truyền Kỳ server (`JXServer`):
1. **Static Game Asset Pipeline (`JXServer.GameAssets`)**: Load original map descriptors (`maplist.ini`, `*.wor`), region obstacle matrices (`Region_S.dat`, supporting both unpacked `/maps` and UCL-compressed `pak/maps.pak`), starter villages (`nativeplacelist.ini`, `revivalid.ini`, `revivepos.ini`), character base templates (`newplayerini00..09.ini`, `level_add.txt`), and prohibited role-name filters (`goddess_rolename.flt`) with boundary-only `TCVN3`/`GBK` $\leftrightarrow$ `UTF-8` transcoding.
2. **ABP DDD Control Plane & Persistence (`Domain`, `Contracts`, `Application`, `Infrastructure`)**: Model `Account`, `Character`, and `SessionLease` aggregate roots backed by **EF Core 10 (`SQL Server` + `SQLite`)**. Enforce monotonically increasing `LeaseEpoch` fencing tokens and single-use 16-byte handoff tickets (`GUID`) to eradicate the legacy "account already in use" lockup.
3. **Sharded 18-Hz Zone Engine (`JXServer.ZoneEngine`)**: Run each configured map as an isolated, lock-free single-threaded `MapZoneWorker` fed by `Channel<IZoneCommand>` at `18 Hz` (`~55.5ms` tick via `TimeProvider`), managing `PlayerZoneState` slots, initial world synchronization (`0x49`, `0x44`, `0x45`, `0x46`, `0x4C`, `0xA4`, `0x43`), heartbeat pings (`0x82`/`0x70`/`0x99`), and clean logout (`0x9B`).
4. **Legacy JX1 Transport Adapter (`JXServer.Adapter.LegacyJX1`)**: Provide `System.IO.Pipelines` TCP listeners on port `5622` (`Bishop`) and port `6666` (`GameServer`) implementing the 44-byte `0x20` key handshake, 5,679-entry `g_uPublicKeys` `KSG_DecodeEncode` stream cipher, and second-layer `0x3042`/`0x38BC` payload ciphers.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (`net10.0`), XML solution (`JXServer.slnx`), `Directory.Build.props` (`Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `AllowUnsafeBlocks=true`), `Directory.Packages.props` (Central Package Management).

**Primary Dependencies**:
- `Microsoft.Extensions.Hosting` (`10.0.0`)
- `System.IO.Pipelines` (`10.0.0`)
- `System.Text.Encoding.CodePages` (`10.0.0`)
- `Microsoft.EntityFrameworkCore.SqlServer` (`10.0.0`) & `Microsoft.EntityFrameworkCore.Sqlite` (`10.0.0`)
- `Microsoft.Extensions.TimeProvider.Testing` (`10.0.0`) & `xunit.v3` (`2.0.0`)

**Storage**: Relational database via EF Core 10 (**Microsoft SQL Server** for production deployments; **SQLite WAL / InMemory** for local development and automated test suites) + read-only original file system assets (`d:\Projects\jx-linux-bin\server1` and `gateway`).

**Testing**: `xunit.v3` unit, protocol vector, deterministic zone-tick, and end-to-end TCP integration tests (plus live smoke validation via `JXSwarm.Drone.Host`).

**Target Platform**: Cross-platform .NET 10 Server (Windows Server / Windows 11 & Linux x64/ARM64).

**Project Type**: Modular Monolith to Distributed Cluster MMORPG Server (`JXServer.slnx`).

**Performance Goals**:
- Full `Bishop (5622)` $\rightarrow$ `GameServer (6666)` world entry in `< 50ms` server processing time (`< 2s` end-to-end including network handshake).
- `18-Hz` (`55.5ms`) deterministic map tick budget with `< 1ms` per-tick overhead for 100+ active sessions in Milestone 1.
- Immediate (`< 3s`) session takeover and lease recovery on abrupt disconnect or `bKickOnline = 1`.

**Constraints**:
- Zero compiler warnings (`-warnaserror`), strict `One-Type-Per-File` rule, pluralized aggregate folders, ABP DDD role suffixes (`*Consts`, `*ErrorCodes`, `*Manager`, `I*Repository`/`EfCore*Repository`, `I*AppService`/`*AppService`, directional DTOs), and the 5 Real-Time 18-Hz Game Server conventions (`*C2SPacket`/`*S2CPacket`, `*ZoneState`/`*ZoneCommand`, unit-explicit `*MpsX`/`*CellX`/`*RegionX`, `*Template`/`I*Catalog`, `*LogMessages`).
- Zero blocking I/O or `DbContext` usage inside the 18-Hz `MapZoneWorker` loop.

**Scale/Scope**: Milestone 1 covers foundational static asset loading, Bishop gateway authentication & character lifecycle (`0x20`, `0x41`, `0x37`, `0x45`, `0x3A`, `0x8C`, `0x64`, `0x34`), lease-fenced session handoff, and GameServer world entry + heartbeat (`0x42`, `0x49`, `0x44`, `0x45`, `0x46`, `0x4C`, `0x4E`, `0xA4`, `0x43`, `0x82`, `0x70`, `0x99`, `0x9B`).

## Constitution Check

*GATE: Passed before Phase 0 research and re-verified after Phase 1 design.*

| Constitution Principle / Gate | Status | Verification Evidence in Design |
| :--- | :--- | :--- |
| **I. Hybrid Distributed Control Plane & Sharded 18-Hz Zone Architecture** | **PASS** | `JXServer.Application` + `JXServer.Domain` manage accounts, characters, and leases; `JXServer.ZoneEngine` isolates each map into a lock-free 18-Hz `MapZoneWorker` over `Channel<IZoneCommand>`. |
| **II. Multi-Protocol Transport Abstraction** | **PASS** | Wire framing, `KSG_DecodeEncode`, and legacy opcodes are strictly isolated inside `JXServer.Adapter.LegacyJX1`, communicating with the core solely via UTF-8 `I*AppService` and `IZoneClusterRouter` ports. |
| **III. Zero-Hallucination Evidence & Ground-Truth Fidelity** | **PASS** | Every packet size, offset, cipher constant (`0x3042`, `0x38BC`, `g_uPublicKeys`), and starter template (`newplayerini00..09.ini`, `revivalid.ini`, `revivepos.ini`) is verified in [research.md](research.md) and [contracts/](contracts/). |
| **IV. Zero-Dupe Economy & Lease-Fenced Session Integrity** | **PASS** | `Account.CurrentLeaseEpoch` and `SessionLease` enforce monotonic fencing epochs across Bishop and GameServer handoffs (`0x34` $\rightarrow$ `0x42`), immediately evicting superseded sessions. |
| **V. UTF-8 Domain Core & Boundary-Only Legacy Transcoding** | **PASS** | `Tcvn3Encoding` and `GbkEncoding` operate strictly at `JXServer.Adapter.LegacyJX1` and `JXServer.GameAssets` boundaries; all entities, DTOs, and SQL Server/SQLite columns are UTF-8 Unicode. |
| **VI. Clean Modular Boundaries & ABP DDD Role Separation** | **PASS** | Enforces inward dependency flow, ABP DDD naming (`*Manager` without `SaveChangesAsync`, `*AppService` returning DTOs, `EfCore*Repository`), `One-Type-Per-File`, and pluralized feature folders. |

## Project Structure

### Documentation (this feature)

```text
specs/001-gateway-world-entry/
├── plan.md                                  # This file (/speckit-plan output)
├── research.md                              # Phase 0 ground-truth research
├── data-model.md                            # Phase 1 ABP DDD & 18-Hz Zone data model
├── quickstart.md                            # Phase 1 build, test, and JX-Swarm smoke guide
├── checklists/
│   └── requirements.md                      # Specification quality checklist (16/16 passed)
└── contracts/
    ├── bishop-gateway-protocol.md           # Port 5622 wire protocol contract
    ├── gameserver-entry-protocol.md         # Port 6666 wire protocol contract
    └── application-and-zone-ports.md        # Internal ABP AppService & ZoneCommand ports
```

### Source Code (repository root `d:\Projects\jx-server`)

```text
JXServer.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── src/
│   ├── JXServer.Primitives/                 # Tier 1: Domain.Shared / Zero-dependency primitives
│   │   ├── Accounts/                        # AccountConsts, AccountStatus, LoginResultCode
│   │   ├── Characters/                      # CharacterConsts, CharacterGender, ElementSeries, EntityKind
│   │   ├── Encodings/                       # Tcvn3Encoding, GbkEncoding, KsgPasswordHasher
│   │   ├── ErrorCodes/                      # JXServerErrorCodes, BusinessException
│   │   ├── SessionLeases/                   # SessionLeaseConsts, SessionLeaseState
│   │   └── WorldZones/                      # WorldCoordinateConsts, WorldPosition
│   │
│   ├── JXServer.GameAssets/                 # Tier 2a: Static asset loaders & read-only catalogs
│   │   ├── Archives/                        # PakFilePathHasher, UclNrv2bDecompressor, XPackArchiveReader
│   │   ├── Configuration/                   # GameAssetsOptions, IniFileParser, TabFileParser
│   │   ├── Logging/                         # GameAssetsLogMessages
│   │   ├── PlayerTemplates/                 # CharacterBaseTemplate, StarterVillageTemplate, IPlayerTemplateCatalog, PlayerTemplateCatalog
│   │   ├── WorldMaps/                       # MapDescriptor, RegionObstacleGrid, IMapCatalog, MapCatalog
│   │   └── GameAssetsServiceCollectionExtensions.cs
│   │
│   ├── JXServer.Contracts/                  # Tier 3a: ABP Application.Contracts (DTOs, ETOs, I*AppService)
│   │   ├── Accounts/                        # IAccountAppService, AuthenticateAccountInput, AuthenticateAccountResultDto
│   │   ├── Characters/                      # ICharacterAppService, CharacterDto, CharacterSummaryDto, CreateCharacterDto, DeleteCharacterDto
│   │   └── SessionLeases/                   # ISessionLeaseAppService, SelectCharacterInput, WorldHandoffTicketDto, RedeemHandoffTicketInput
│   │
│   ├── JXServer.Domain/                     # Tier 3b: ABP DDD Domain (Aggregates, *Manager, I*Repository)
│   │   ├── Accounts/                        # Account, AccountManager, IAccountRepository
│   │   ├── Characters/                      # Character, CharacterSkillValue, CharacterManager, ICharacterRepository
│   │   └── SessionLeases/                   # SessionLease, SessionLeaseManager, ISessionLeaseRepository, ISessionEvictionNotifier
│   │
│   ├── JXServer.ZoneEngine/                 # Tier 2b: Sharded 18-Hz Deterministic Simulation Engine
│   │   ├── Configuration/                   # ZoneEngineOptions
│   │   ├── Logging/                         # ZoneEngineLogMessages
│   │   ├── PlayerSessions/                  # PlayerZoneState, IPlayerOutboundSink, PlayerDetachReason, AttachPlayerZoneCommand, CompletePlayerSyncZoneCommand, ProcessPlayerPingZoneCommand, DetachPlayerZoneCommand
│   │   ├── WorldZones/                      # IZoneCommand, IZoneClusterRouter, MapZoneWorker, ZoneClusterRouter, ZoneEngineHostedService
│   │   └── ZoneEngineServiceCollectionExtensions.cs
│   │
│   ├── JXServer.Application/                # Tier 3c: ABP DDD Application Use-Case Orchestrators
│   │   ├── Accounts/                        # AccountAppService
│   │   ├── Characters/                      # CharacterAppService
│   │   ├── Logging/                         # ApplicationLogMessages
│   │   ├── SessionLeases/                   # SessionLeaseAppService
│   │   └── ApplicationServiceCollectionExtensions.cs
│   │
│   ├── JXServer.Infrastructure/             # Tier 3d: EF Core 10 (SQL Server + SQLite) & Session Store
│   │   ├── Accounts/                        # EfCoreAccountRepository
│   │   ├── Characters/                      # EfCoreCharacterRepository
│   │   ├── Configuration/                   # PersistenceOptions
│   │   ├── EntityFrameworkCore/             # JXServerDbContext, DatabaseInitializerHostedService
│   │   ├── SessionLeases/                   # InMemorySessionLeaseRepository
│   │   └── InfrastructureServiceCollectionExtensions.cs
│   │
│   ├── JXServer.Adapter.LegacyJX1/          # Tier 4a: Legacy TCP 5622 (Bishop) & 6666 (GameServer) Adapter
│   │   ├── Bishop/                          # BishopTcpListenerHostedService, BishopClientSession, IBishopPacketHandler, AccountLoginPacketHandler, CreateCharacterPacketHandler, DeleteCharacterPacketHandler, SelectCharacterPacketHandler
│   │   ├── Ciphers/                         # KsgStreamCipher, KsgPublicKeyTable, SecondLayerXorCipher, HandshakeKeyObfuscator
│   │   ├── Configuration/                   # LegacyGatewayOptions
│   │   ├── Framing/                         # JxFramePipeProcessor, ProtocolSizeTable
│   │   ├── GameServer/                      # GameServerTcpListenerHostedService, GameServerClientSession, IGameServerPacketHandler, LogicLoginPacketHandler, SyncEndPacketHandler, PingReplyPacketHandler, CloseConnectionPacketHandler
│   │   ├── Logging/                         # LegacyAdapterLogMessages
│   │   ├── Packets/                         # ProtocolOpcodes, *C2SPacket & *S2CPacket structs (one type per file)
│   │   └── LegacyJX1AdapterServiceCollectionExtensions.cs
│   │
│   └── JXServer.Host/                       # Tier 4b: Executable .NET 10 Server Host
│       ├── Program.cs
│       └── appsettings.json
│
└── tests/
    ├── JXServer.Protocol.Tests/             # Cipher, framing, and symmetric packet serialization tests
    ├── JXServer.ZoneEngine.Tests/           # Asset loading, template resolution, and 18-Hz FakeTimeProvider tests
    └── JXServer.Integration.Tests/          # End-to-end Bishop (5622) -> GameServer (6666) login & lease-fencing tests
```

**Structure Decision**: Adopted a 9-project `.NET 10` modular architecture (`JXServer.slnx`) that enforces strict inward dependencies, ABP Framework DDD conventions in the Control Plane, and data-oriented 18-Hz isolation in `JXServer.ZoneEngine`.

## Complexity Tracking

No Constitution violations exist. Every module has a single, well-bounded architectural responsibility required by Principles I–VI.
