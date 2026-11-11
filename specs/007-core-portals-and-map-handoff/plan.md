# Implementation Plan: Boundary Portals & Map Handoff

**Branch**: `007-core-portals-and-map-handoff` | **Date**: 2026-11-11 | **Spec**: [spec.md](spec.md)

---

## 1. Summary

Deliver authoritative map boundary portal detection, edge-triggered anti-loop protection, town-gate combat state toggles (`FightMode`), same-node in-memory world transitions (`NewWorld`), cross-node cluster redirection, and level-gated travel restrictions for JXServer (.NET 10, C# 14) in strict compliance with **Constitution v1.2.0** and **`JXData Standard v1`**:

1. **Cell Trap Parsing & Hash Indexing (`JXServer.GameAssets`)**: Parse binary `TRAP_SECTION` (Section 1: 12B `KTrapFileHead` + 8B `KSPTrap` cell runs) from map region files, indexing trap scripts via 32-bit `g_FileName2Id` path hash (`PakFilePathHasher.ComputeHash`) into `ITrapScriptCatalog`.
2. **Edge-Triggered Detection & Gate Toggles (`JXServer.ZoneEngine`)**: In `MovementAndPostureZoneTickSystem` (Phase 500), evaluate player cell occupancy against `uTrapId`. Enforce `LastTriggeredTrapId` to prevent duplicate triggers on the same cell run. For town gates, toggle `FightMode` `0 <-> 1` and nudge player coordinates past the gate threshold.
3. **Same-Node In-Memory Map Transfer (`IZoneClusterRouter`)**: Execute in-memory player handoff via `TryTransferPlayerAcrossMaps`, removing player from the source spatial grid and attaching them to the destination map worker via `AttachTransferredPlayerZoneCommand`. Dispatch `s2c_syncworld` (`0x49`, 34B), self-position sync (`0x4E`, 13B), and destination 3x3 AOI entities (`0x4C` / `0x4D`) without resetting the TCP stream cipher and without sending `0x43`.
4. **Cross-Node Cluster Redirection & Level Gating**: Emit `s2c_notifyplayerexchange` (`0x35`, 25B) with single-use handoff ticket `GUID` and plain Little-Endian port (`6666 = 0x1A0A`, NOT `htons`) for external nodes. Bounce under-leveled players back to safe source coordinates with an advisory system message (`0xFB`).

---

## 2. Technical Context

- **Language/Version**: C# 14 on .NET 10 (`net10.0`), centralized `Directory.Build.props` (`TreatWarningsAsErrors=true`, `AnalysisLevel=latest`).
- **Primary Dependencies**:
  - `Microsoft.Extensions.Hosting` / `DependencyInjection` / `Logging.Abstractions` (`10.0.0`)
  - `System.IO.Pipelines` (`10.0.0`)
- **Storage**:
  - Map region trap definitions and script bindings loaded into immutable `ITrapScriptCatalog`.
  - Durable player world coordinates persisted via `ICharacterAppService.UpdateCharacterWorldSnapshotAsync`.
- **Testing**:
  - `xunit.v3` across `JXServer.ZoneEngine.Tests` (`TrapAndMapTransitionTests`) and `JXServer.Integration.Tests` (`MapTransitionIntegrationTests`).
- **Performance Goals**:
  - Complete same-node in-memory handoff in under 5ms of shard CPU time.
  - Zero duplicate trap executions while remaining stationary on a trap cell run.

---

## 3. Constitution Check (v1.2.0)

### Core Principles (I–VI)

| Principle | Status | Verification Evidence |
| :--- | :---: | :--- |
| **I. Hybrid Distributed Control Plane & Sharded 18-Hz Zone Architecture** | **PASS** | Shards execute independently; inter-shard handoff is coordinated via lock-free message passing in `IZoneClusterRouter`. |
| **II. Multi-Protocol Transport Abstraction & Client Compatibility** | **PASS** | In-memory transitions preserve wire socket and rolling stream cipher state without protocol renegotiation. |
| **III. Zero-Hallucination Evidence & Ground-Truth Fidelity** | **PASS** | Preserves plain Little-Endian port in `0x35` (`6666 = 0x1A0A`), `0x49` packed subworld region, and `g_FileName2Id` hash formula. |
| **IV. Zero-Dupe Economy & Lease-Fenced Session Integrity** | **PASS** | Map handoffs verify active `LeaseEpoch` before completing transfer; destination registration retains epoch. |
| **V. UTF-8 Domain Core, Dual-Tier Game Data Standard & Boundary-Only Transcoding** | **PASS** | Map names in domain logic are UTF-8; boundary encoding to TCVN3 occurs only at wire packet serialization. |
| **VI. Clean Modular Boundaries, ABP DDD Role Separation & C# 14 Scripting** | **PASS** | One type per file, sealed concrete classes, pluralized folders (`WorldMaps/`, `PlayerSessions/`). |

### Verification Gates (1–5)

| Gate | Status | Enforcement |
| :--- | :---: | :--- |
| **1. Ground-Truth Specification Gate** | **PASS** | Documented in `contracts/portal-and-handoff-protocol.md`. |
| **2. Symmetric Protocol & Formula Unit Gate** | **PASS** | Verified in `TrapAndMapTransitionTests`. |
| **3. Autonomous Swarm & Headless Integration Gate** | **PASS** | Verified in `MapTransitionIntegrationTests`. |
| **4. High-Density Stress & Fault Isolation Gate** | **PASS** | Validates rapid pacing across trap cells without memory leaks or race conditions. |
| **5. Structural & Zero-Warning Build Gate** | **PASS** | 0 compiler warnings under `TreatWarningsAsErrors=true`. |

---

## 4. Project Structure

### Documentation

```text
specs/007-core-portals-and-map-handoff/
├── spec.md                                          # WHAT & WHY, 16/16 requirements verified
├── plan.md                                          # Technical context & architecture
├── research.md                                      # Ground-truth analysis & formulas
├── data-model.md                                    # Portal entities and catalog schemas
├── quickstart.md                                    # Build & verification instructions
├── checklists/
│   └── requirements.md                              # 16/16 verification checklist
└── contracts/
    ├── portal-and-handoff-protocol.md               # Wire protocol contracts (0x35, 0x49, 0x4E)
    └── portal-and-cluster-ports.md                  # Internal cluster router & catalog ports
```

### Source Code

```text
src/
├── JXServer.Primitives/
│   └── WorldZones/
│       └── PortalTrapActionKind.cs                  # Action kind enum (ToggleFightState, ChangeMap)
│
├── JXServer.Contracts/
│   └── Characters/
│       └── UpdateCharacterWorldSnapshotDto.cs       # Coordinates, MapId, FightMode persistence DTO
│
├── JXServer.GameAssets/
│   └── WorldMaps/
│       ├── ITrapScriptCatalog.cs                    # Trap catalog interface
│       ├── PakFilePathHasher.cs                     # g_FileName2Id 32-bit hash function
│       ├── PortalTrapBindingTemplate.cs             # Portal destination template
│       ├── RegionTrapGrid.cs                        # Region 16x32 cell trap ID grid
│       └── TrapScriptCatalog.cs                     # In-memory trap catalog implementation
│
├── JXServer.ZoneEngine/
│   ├── PlayerSessions/
│   │   ├── AttachTransferredPlayerZoneCommand.cs    # Attaches player to destination shard
│   │   └── IWorldOutboundSink.cs                    # SendWorldMapTransitionSync, SendNotifyServerExchange
│   └── WorldZones/
│       ├── IZoneClusterRouter.cs                    # Sharded cluster router interface
│       ├── MovementAndPostureZoneTickSystem.cs      # Phase 500 trap detection & execution
│       └── ZoneClusterRouter.cs                     # Sharded cluster router implementation
│
└── JXServer.Adapter.LegacyJX1/
    ├── GameServer/
    │   └── GameServerClientSession.WorldSink.cs     # Emits 0x49, 0x35, 0x4E
    └── Packets/
        ├── NotifyPlayerExchangeS2CPacket.cs         # 0x35 wire layout (25B)
        ├── SyncNpcMinPlayerS2CPacket.cs             # 0x4E wire layout (13B)
        └── SyncWorldS2CPacket.cs                    # 0x49 wire layout (34B)

tests/
├── JXServer.ZoneEngine.Tests/
│   └── TrapAndMapTransitionTests.cs                 # Trap catalog, anti-loop & transition tests
└── JXServer.Integration.Tests/
    └── MapTransitionIntegrationTests.cs             # Live TCP in-memory & cross-map tests
```
