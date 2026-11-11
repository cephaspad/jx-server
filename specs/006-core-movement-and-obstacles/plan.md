# Implementation Plan: Movement, Posture & Obstacle Validation

**Branch**: `006-core-movement-and-obstacles` | **Date**: 2026-11-11 | **Spec**: [spec.md](spec.md)

---

## 1. Summary

Deliver the authoritative movement, anti-cheat security verification, 18-Hz simulation stepping, obstacle collision clamping, and posture regeneration subsystem for JXServer (.NET 10, C# 14) in strict compliance with **Constitution v1.2.0** and **`JXData Standard v1`**:

1. **Anti-Cheat Checksum Verification (`JXServer.Primitives`)**: Validate the 4-byte `dwCheckValue` field on incoming walk (`0x4B`, key `0x24A1`) and run (`0x4C`, key `0x1D78`) packets using the canonical formula `dwCheckValue != 0 && ((dwCheckValue ^ token1 ^ token2) % key) == 0`.
2. **Deterministic 18-Hz Movement Pipeline (`JXServer.ZoneEngine`)**: Process `MovePlayerZoneCommand`, `JumpPlayerZoneCommand`, and `TogglePlayerSitZoneCommand` inside `MovementCommandHandler`, broadcasting `0x50` (`s2c_npcwalk`) or `0x51` (`s2c_npcrun`) to the 3x3 region AOI. In `MovementAndPostureZoneTickSystem` (Phase 500), step coordinates frame-by-frame at 10 Mps/tick (walk) or 20 Mps/tick (run).
3. **Obstacle Collision Detection (`JXServer.GameAssets`)**: Check target cells against `RegionObstacleGrid` (`Obstacle_Full == 1`). If blocked, halt progression at the last passable coordinate and return to standing posture (`do_stand`).
4. **Sitting Meditation & Vital Regeneration**: Transition characters via `0x71` (`c2s_npcsit`) -> `0x53` (`s2c_npcsit`), accelerating health and mana regeneration while seated, and instantly breaking sit posture on movement.

---

## 2. Technical Context

- **Language/Version**: C# 14 on .NET 10 (`net10.0`), XML solution (`JXServer.slnx`), centralized `Directory.Build.props` (`TreatWarningsAsErrors=true`, `AnalysisLevel=latest`).
- **Primary Dependencies**:
  - `Microsoft.Extensions.Hosting` / `DependencyInjection` / `Logging.Abstractions` (`10.0.0`)
  - `System.IO.Pipelines` (`10.0.0`)
- **Storage**:
  - Static map obstacle grids loaded from canonical JSON / snapshots into immutable `RegionObstacleGrid` structures.
- **Testing**:
  - `xunit.v3` across `JXServer.Protocol.Tests` (`MovementAndPosturePacketTests`), `JXServer.ZoneEngine.Tests` (`MovementSimulationTests`), and `JXServer.Integration.Tests` (`MovementIntegrationTests`).
- **Performance Goals**:
  - 18-Hz simulation stepping must execute under 5 microseconds per moving entity.
  - Zero heap allocations during obstacle collision checking.

---

## 3. Constitution Check (v1.2.0)

### Core Principles (I–VI)

| Principle | Status | Verification Evidence |
| :--- | :---: | :--- |
| **I. Hybrid Distributed Control Plane & Sharded 18-Hz Zone Architecture** | **PASS** | Movement stepping executes strictly inside single-threaded `MapZoneWorker` tick pipeline (Phase 500) without locks. |
| **II. Multi-Protocol Transport Abstraction & Client Compatibility** | **PASS** | Asymmetric wire packets (`0x50` vs `0x51`) are isolated in `JXServer.Adapter.LegacyJX1`. |
| **III. Zero-Hallucination Evidence & Ground-Truth Fidelity** | **PASS** | Anti-cheat keys (`0x24A1`, `0x1D78`), speed constants (10/20 Mps), and asymmetric packet layouts verified from `KNpc::DoWalk` and `KNpc::DoRun`. |
| **IV. Zero-Dupe Economy & Lease-Fenced Session Integrity** | **PASS** | Movement commands carry valid `LeaseEpoch` to prevent orphaned session actions. |
| **V. UTF-8 Domain Core, Dual-Tier Game Data Standard & Boundary-Only Transcoding** | **PASS** | Movement simulation uses pure numeric coordinates (`MpsX`, `MpsY`); zero text encoding in movement pipeline. |
| **VI. Clean Modular Boundaries, ABP DDD Role Separation & C# 14 Scripting** | **PASS** | Phased separation: `MovementCommandHandler` handles commands; `MovementAndPostureZoneTickSystem` executes Phase 500 tick. |

### Verification Gates (1–5)

| Gate | Status | Enforcement |
| :--- | :---: | :--- |
| **1. Ground-Truth Specification Gate** | **PASS** | Ground-truth formulas, keys, and byte layouts documented in `contracts/`. |
| **2. Symmetric Protocol & Formula Unit Gate** | **PASS** | Validated by `MovementAndPosturePacketTests` and `MovementSimulationTests`. |
| **3. Autonomous Swarm & Headless Integration Gate** | **PASS** | Validated by `MovementIntegrationTests`. |
| **4. High-Density Stress & Fault Isolation Gate** | **PASS** | Benchmark movement and obstacle collision across high density paths. |
| **5. Structural & Zero-Warning Build Gate** | **PASS** | 0 compiler warnings under `TreatWarningsAsErrors=true`; strict one-type-per-file. |

---

## 4. Project Structure

### Documentation

```text
specs/006-core-movement-and-obstacles/
├── spec.md                                          # WHAT & WHY, 16/16 requirements verified
├── plan.md                                          # Technical context & architecture
├── research.md                                      # Ground-truth analysis & formulas
├── data-model.md                                    # Movement states and obstacle schemas
├── quickstart.md                                    # Build & verification instructions
├── checklists/
│   └── requirements.md                              # 16/16 verification checklist
└── contracts/
    ├── movement-and-posture-protocol.md             # Wire protocol contracts (0x4B, 0x4C, 0x50, 0x51, 0x4E, 0x54, 0x71, 0x53)
    └── movement-ports-and-simulation.md             # Internal engine and tick system ports
```

### Source Code

```text
src/
├── JXServer.Primitives/
│   └── WorldZones/
│       ├── EntityDoingPosture.cs                    # Posture enum (Stand, Walk, Run, Sit, Jump)
│       └── MovementAntiCheatConsts.cs               # dwCheckValue verification keys & algorithm
│
├── JXServer.GameAssets/
│   └── WorldMaps/
│       └── RegionObstacleGrid.cs                    # Fast bitmask obstacle grid for 16x32 cells
│
├── JXServer.ZoneEngine/
│   ├── PlayerSessions/
│   │   ├── JumpPlayerZoneCommand.cs                 # Zone command for character jump
│   │   ├── MovePlayerZoneCommand.cs                 # Zone command for walk and run
│   │   └── TogglePlayerSitZoneCommand.cs            # Zone command for sit/stand toggle
│   └── WorldZones/
│       ├── MovementCommandHandler.cs                # Modular handler for movement zone commands
│       └── MovementAndPostureZoneTickSystem.cs      # Phase 500 tick system (speed stepping & obstacles)
│
└── JXServer.Adapter.LegacyJX1/
    ├── GameServer/
    │   ├── GameServerClientSession.WorldSink.cs     # Broadcasts 0x50, 0x51, 0x53, 0x54
    │   ├── NpcJumpPacketHandler.cs                  # Handles 0x4E c2s_npcjump
    │   ├── NpcRunPacketHandler.cs                   # Handles 0x4C c2s_npcrun
    │   ├── NpcSitPacketHandler.cs                   # Handles 0x71 c2s_npcsit
    │   └── NpcWalkPacketHandler.cs                  # Handles 0x4B c2s_npcwalk
    └── Packets/
        ├── NpcJumpC2SPacket.cs                      # 0x4E wire layout (13B)
        ├── NpcJumpS2CPacket.cs                      # 0x54 wire layout (13B)
        ├── NpcRunC2SPacket.cs                       # 0x4C wire layout (13B)
        ├── NpcRunS2CPacket.cs                       # 0x51 wire layout (13B, asymmetric)
        ├── NpcSitC2SPacket.cs                       # 0x71 wire layout (2B)
        ├── NpcSitS2CPacket.cs                       # 0x53 wire layout (5B)
        ├── NpcWalkC2SPacket.cs                      # 0x4B wire layout (13B)
        └── NpcWalkS2CPacket.cs                      # 0x50 wire layout (13B, asymmetric)

tests/
├── JXServer.Protocol.Tests/
│   └── MovementAndPosturePacketTests.cs             # Packet serialization & check value tests
├── JXServer.ZoneEngine.Tests/
│   └── MovementSimulationTests.cs                  # 18-Hz speed progression & obstacle clamping tests
└── JXServer.Integration.Tests/
    └── MovementIntegrationTests.cs                 # Live TCP movement & anti-cheat verification tests
```
