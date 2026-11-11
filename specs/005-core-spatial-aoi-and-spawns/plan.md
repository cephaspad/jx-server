# Implementation Plan: Spatial 3x3 Region AOI Visibility, Entity Synchronization & Static NPC/Monster Spawns

**Branch**: `005-core-spatial-aoi-and-spawns` | **Date**: 2026-11-11 | **Spec**: [spec.md](spec.md)

---

## 1. Summary

Deliver the foundational spatial partitioning, Area-of-Interest (AOI) visibility, entity presence synchronization, and static NPC/monster world population architecture for JXServer (.NET 10, C# 14) in strict compliance with **Constitution v1.2.0** and **`JXData Standard v1`** (`docs/data-architecture-standard.md`):

1. **Spatial 2D Region Grid (`JXServer.ZoneEngine`)**: Partition each map zone into 512x1024 world pixel (16x32 cells) regions via `SpatialRegionGrid` and `SpatialRegionSlot`, maintaining dynamic collections of players and NPCs per region with zero heap allocations on hot path neighborhood lookups.
2. **Dynamic 3x3 Region AOI & Set-Difference Synchronization**: Compute `New3x3 \ Old3x3` and `Old3x3 \ New3x3` set differences upon world entry and region boundary crossings, broadcasting full `s2c_syncnpc` (`0x4C`, 61B) to new observers, periodic `s2c_syncnpcmin` (`0x4D`, 18B) neighbor position updates, and authoritative `s2c_syncnpcminplayer` (`0x4E`, 13B) self-position updates.
3. **On-Demand Entity Detail Resolution**: Process `c2s_requestplayer` (`0x48`, 5B) and `c2s_requestnpc` (`0x49`, 5B) via `WorldSessionCommandHandler` and `IWorldOutboundSink.SendNpcFullSync`, verifying entity presence on the active map and returning `0x4C`.
4. **Static NPC & Monster Population from Map Assets (`JXServer.GameAssets`)**: Populate static service NPCs and monsters into map regions from `settings/npcs.txt` (`INpcTemplateCatalog`), `script/global/autoexec.lua` dialog placements, and binary `Region_S.dat` `NPC_SECTION` placement data with localized Vietnamese names.

---

## 2. Technical Context

- **Language/Version**: C# 14 on .NET 10 (`net10.0`), XML solution (`JXServer.slnx`), centralized `Directory.Build.props` (`TreatWarningsAsErrors=true`, `AnalysisLevel=latest`).
- **Primary Dependencies**:
  - `Microsoft.Extensions.Hosting` / `DependencyInjection` / `Logging.Abstractions` (`10.0.0`)
  - `System.IO.Pipelines` (`10.0.0`) & `System.Text.Encoding.CodePages` (`10.0.0` — `TCVN3` transcoding)
  - `MemoryPack` (`1.21.4` — static catalog snapshots)
- **Storage**:
  - Static game data catalogs (`JXData Standard v1`): `INpcTemplateCatalog`, `IMapCatalog` (`data/**/*.json` and `artifacts/*.pack`).
- **Testing**:
  - `xunit.v3` across `JXServer.Protocol.Tests` (`AoiEntitySyncPacketTests`), `JXServer.ZoneEngine.Tests` (`SpatialRegionGridTests`, `HighDensityAoiBenchmarkTests`, `NpcAssetLoadingTests`), and `JXServer.Integration.Tests` (`SpatialAoiIntegrationTests`).
- **Performance Goals**:
  - Up to 50 concurrent entities moving across region boundaries maintain 18-Hz simulation loop with 99th-percentile tick duration < 15ms.
  - Zero heap allocation during `3x3` region collection via stack-allocated spans.

---

## 3. Constitution Check (v1.2.0)

### Core Principles (I–VI)

| Principle | Status | Verification Evidence |
| :--- | :---: | :--- |
| **I. Hybrid Distributed Control Plane & Sharded 18-Hz Zone Architecture** | **PASS** | `SpatialRegionGrid` is encapsulated strictly within each `MapZoneWorker` shard; zero cross-thread locking or cross-shard shared memory. |
| **II. Multi-Protocol Transport Abstraction & Client Compatibility** | **PASS** | `JXServer.ZoneEngine` uses wire-agnostic `EntityFullSyncSnapshot` and `IWorldOutboundSink`; wire packets (`0x48`, `0x49`, `0x4C`, `0x4D`, `0x4E`) remain isolated in `JXServer.Adapter.LegacyJX1`. |
| **III. Zero-Hallucination Evidence & Ground-Truth Fidelity** | **PASS** | Exact byte offsets, Little-Endian packing, and C++ Kingsoft formulas (`NpcSettingIdx = (Level << 16) \| TemplateId`, `Sub-cell OffX = (MpsX % 32) << 10`) match ground truth. |
| **IV. Zero-Dupe Economy & Lease-Fenced Session Integrity** | **PASS** | Session attachments and spatial registrations require active monotonic `LeaseEpoch`. |
| **V. UTF-8 Domain Core, Dual-Tier Game Data Standard & Boundary-Only Transcoding** | **PASS** | Internal names are UTF-8; boundary transcoding from/to `TCVN3` occurs strictly at the wire adapter layer (`SyncNpcFullS2CPacket`). |
| **VI. Clean Modular Boundaries, ABP DDD Role Separation & C# 14 Scripting** | **PASS** | One type per file, sealed concrete classes, pluralized folders (`SpatialGrid/`, `NpcEntities/`, `NpcTemplates/`), explicit unit suffixes (`*MpsX`, `*MpsY`, `*CellX`, `*CellY`). |

### Verification Gates (1–5)

| Gate | Status | Enforcement |
| :--- | :---: | :--- |
| **1. Ground-Truth Specification Gate** | **PASS** | Ground-truth wire formats, hex test vectors, and region dimensions preserved in `contracts/`. |
| **2. Symmetric Protocol & Formula Unit Gate** | **PASS** | Validated by `AoiEntitySyncPacketTests` and `SpatialRegionGridTests`. |
| **3. Autonomous Swarm & Headless Integration Gate** | **PASS** | Validated by `SpatialAoiIntegrationTests`. |
| **4. High-Density Stress & Fault Isolation Gate** | **PASS** | Validated by `HighDensityAoiBenchmarkTests` (50+ entities moving across boundaries). |
| **5. Structural & Zero-Warning Build Gate** | **PASS** | 0 compiler warnings under `TreatWarningsAsErrors=true`; strict one-type-per-file. |

---

## 4. Project Structure

### Documentation

```text
specs/005-core-spatial-aoi-and-spawns/
├── spec.md                                          # WHAT & WHY, 16/16 requirements verified
├── plan.md                                          # Technical context & architecture
├── research.md                                      # Ground-truth analysis & formulas
├── data-model.md                                    # Spatial grid and entity schemas
├── quickstart.md                                    # Build & verification instructions
├── checklists/
│   └── requirements.md                              # 16/16 verification checklist
└── contracts/
    ├── spatial-aoi-and-spawns-protocol.md           # Wire protocol contracts (0x48, 0x49, 0x4C, 0x4D, 0x4E)
    └── spatial-grid-and-spawns-ports.md             # Internal engine and catalog ports
```

### Source Code

```text
src/
├── JXServer.GameAssets/
│   ├── NpcTemplates/
│   │   ├── INpcTemplateCatalog.cs                   # Catalog query interface
│   │   ├── NpcTemplate.cs                           # Read-only NPC archetype
│   │   ├── NpcTemplateCatalog.cs                    # npcs.txt & autoexec.lua loader
│   │   └── StaticNpcSpawnTemplate.cs                # Static spawn point definition
│   └── WorldMaps/
│       ├── MapCatalog.cs                            # Map & region data loader
│       └── MapDescriptor.cs                         # Region bounding & static spawn lists
│
├── JXServer.ZoneEngine/
│   ├── SpatialGrid/
│   │   ├── SpatialRegionGrid.cs                     # 2D region spatial partitioning & 3x3 AOI
│   │   └── SpatialRegionSlot.cs                     # Per-region player & NPC membership collections
│   ├── NpcEntities/
│   │   └── NpcZoneState.cs                          # In-memory NPC runtime instance state
│   ├── PlayerSessions/
│   │   ├── EntityFullSyncSnapshot.cs                # Unified entity appearance sync snapshot
│   │   ├── IWorldOutboundSink.cs                    # World & AOI outbound notifications
│   │   └── RequestEntitySyncZoneCommand.cs          # Zone command for on-demand entity queries
│   └── WorldZones/
│       └── WorldSessionCommandHandler.cs            # Dispatches RequestEntitySyncZoneCommand
│
└── JXServer.Adapter.LegacyJX1/
    ├── GameServer/
    │   ├── GameServerClientSession.WorldSink.cs     # Implements IWorldOutboundSink (0x4C, 0x4D, 0x4E)
    │   ├── RequestPlayerPacketHandler.cs            # Handles 0x48 c2s_requestplayer
    │   └── RequestNpcPacketHandler.cs               # Handles 0x49 c2s_requestnpc
    └── Packets/
        ├── RequestPlayerC2SPacket.cs                # 0x48 wire layout (5B)
        ├── RequestNpcC2SPacket.cs                   # 0x49 wire layout (5B)
        ├── SyncNpcFullS2CPacket.cs                  # 0x4C wire layout (61B)
        ├── SyncNpcMinS2CPacket.cs                   # 0x4D wire layout (18B)
        └── SyncNpcMinPlayerS2CPacket.cs             # 0x4E wire layout (13B)

tests/
├── JXServer.Protocol.Tests/
│   └── AoiEntitySyncPacketTests.cs                  # Wire serialization round-trip tests
├── JXServer.ZoneEngine.Tests/
│   ├── SpatialRegionGridTests.cs                    # 3x3 AOI diff & membership tests
│   ├── HighDensityAoiBenchmarkTests.cs              # Multi-entity boundary crossing benchmark
│   └── NpcAssetLoadingTests.cs                      # Static NPC asset loading & name resolution tests
└── JXServer.Integration.Tests/
    └── SpatialAoiIntegrationTests.cs                # End-to-end multi-client AOI integration tests
```
