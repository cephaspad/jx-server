# Implementation Plan: Core Combat and 64-Direction Missiles

**Branch**: `009-core-combat-and-missiles` | **Date**: 2026-11-11 | **Spec**: [spec.md](spec.md)

## Summary

Deliver the **009-core-combat-and-missiles** vertical slice of `JXServer` in strict compliance with **Constitution v1.2.0** and **JXData Standard v1**:
1. **Martial Skill Execution & Wire Dispatch**: Support client skill commands (`0x4D` `c2s_npcskill`, wrapped auto-attack `0x87` `c2s_autoattacknpc`, aura switch `0x6F` `c2s_changeauraskill`) routed to `CastSkillZoneCommand`, `ChangeAuraSkillZoneCommand`, and `ChangePkModeZoneCommand` handled by `CombatProgressionCommandHandler`.
2. **64-Direction Fixed-Point Missile Simulation**: Stepping pooled `ActiveMissileSlot` instances on Phase 300 (`MissileZoneTickSystem` / `MissileSimulationTickSystem`) using `SixtyFourDirectionTrigTable` (1024-scaled integer sine/cosine vectors) with hit-radius queries against `SpatialRegionGrid`.
3. **Five-Elements Damage Math & Mitigations**: Deterministic multi-channel damage evaluation in `CombatDamageCalculator` using `FiveElementsSeriesMatrix`, physical/elemental defense reductions, attack rating vs defense rating hit probability clamping, and status effect generation.
4. **Status Ailments & Outbound Sinks**: Manage Cold slow, Poison DoT ticks, Lightning stuns, and hurt recovery in `EntityStatusEffectState` stepped by Phase 100 (`StatusEffectZoneTickSystem`), broadcasting hurt reactions (`0x56` `s2c_broadcasenetcommand` with `do_hurt`), skill casts (`0x5A` / `0x85`), and vitals via `ICombatOutboundSink`.

---

## Technical Context

- **Platform & Language**: .NET 10 (`net10.0`), C# 14 with `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`, `AllowUnsafeBlocks=true`.
- **Primary Projects**:
  - `src/JXServer.Primitives`: Math tables (`SixtyFourDirectionTrigTable`), Five-Elements matrices (`FiveElementsSeriesMatrix`), constants (`CombatConsts`), and enumerations (`SkillStyleKind`, `MissileMoveKind`, `DamageChannelKind`, `PlayerPkMode`).
  - `src/JXServer.GameAssets`: `ISkillTemplateCatalog`, `IMissileTemplateCatalog`, `SkillTemplate`, `MissileTemplate`, `SkillCostScalingTemplate`, `SkillEffectScalingTemplate`.
  - `src/JXServer.ZoneEngine`: `ActiveMissileSlot`, `SkillDamagePayload`, `EntityStatusEffectState`, `CombatDamageCalculator`, `MissileZoneTickSystem`, `StatusEffectZoneTickSystem`, `CombatProgressionCommandHandler`, `CastSkillZoneCommand`, `ChangeAuraSkillZoneCommand`, `ChangePkModeZoneCommand`, `ICombatOutboundSink`.
  - `src/JXServer.Adapter.LegacyJX1`: Wire packets `NpcSkillC2SPacket` (`0x4D`), `AutoAttackNpcC2SPacket` (`0x87`), `ChangeAuraSkillC2SPacket` (`0x6F`), `PkApplyChangeNormalFlagC2SPacket` (`0x76`), `SkillCastS2CPacket` (`0x5A`), `CastSkillDirectlyS2CPacket` (`0x85`), `BroadcastNetCommandS2CPacket` (`0x56`), and packet handlers.
- **Verification Suites**:
  - `tests/JXServer.Protocol.Tests/CombatAndSkillPacketTests.cs`
  - `tests/JXServer.Protocol.Tests/SixtyFourDirectionAndFiveElementsMathTests.cs`
  - `tests/JXServer.ZoneEngine.Tests/CombatAndMissileSimulationTests.cs`
  - `tests/JXServer.Integration.Tests/CombatSkillIntegrationTests.cs`

---

## Constitution Check (v1.2.0)

| Principle | Status | Evaluation |
| :--- | :---: | :--- |
| **I. Hybrid Distributed Control Plane & Sharded 18-Hz Zone Loop** | **PASS** | Missiles and combat tick entirely within single-threaded 18-Hz `MapZoneWorker` shards. High missile activity in one map shard never impacts other zones. |
| **II. Multi-Protocol Transport Abstraction** | **PASS** | `ZoneEngine` operates solely on strongly-typed `CastSkillZoneCommand` and domain `ICombatOutboundSink`. Legacy opcodes and wire layouts stay in `Adapter.LegacyJX1`. |
| **III. Zero-Hallucination & Ground-Truth Fidelity** | **PASS** | Derived verbatim from Kingsoft reference code (`KSkills.cpp`, `KMissle.cpp`, `KNpc.cpp`, `KNpcAttribModify.cpp`) and `jx-swarm` test vectors. |
| **IV. Zero-Dupe Economy & Lease-Fenced Integrity** | **PASS** | Combat commands validate active `LeaseEpoch` against current player session before processing mutations. |
| **V. UTF-8 Domain Core & Boundary-Only Transcoding** | **PASS** | Skill and missile codes/names use standard UTF-8. Boundary transcoding applies only at wire packet edge. |
| **VI. Clean Modular Boundaries, ABP DDD & C# 14** | **PASS** | 1 type per `.cs` file, sealed concrete classes, pluralized folders (`Combat/`), explicit unit suffixes (`*MpsX`, `*MpsY`, `*Frames`). |

---

## Project Structure

```text
specs/009-core-combat-and-missiles/
├── spec.md
├── checklists/
│   └── requirements.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── combat-and-skills-protocol.md
└── tasks.md
```

### Actual Codebase Mapping

```text
src/
├── JXServer.Primitives/
│   └── Combat/
│       ├── SixtyFourDirectionTrigTable.cs
│       ├── FiveElementsSeriesMatrix.cs
│       ├── CombatConsts.cs
│       ├── DamageChannelKind.cs
│       ├── SkillStyleKind.cs
│       ├── MissileMoveKind.cs
│       ├── PlayerPkMode.cs
│       └── EntityRelationKind.cs
├── JXServer.GameAssets/
│   └── Skills/
│       ├── ISkillTemplateCatalog.cs
│       ├── IMissileTemplateCatalog.cs
│       ├── SkillTemplate.cs
│       ├── MissileTemplate.cs
│       ├── SkillCostScalingTemplate.cs
│       ├── SkillEffectScalingTemplate.cs
│       └── SkillParameterCurveTemplate.cs
├── JXServer.ZoneEngine/
│   ├── Combat/
│   │   ├── ActiveMissileSlot.cs
│   │   ├── SkillDamagePayload.cs
│   │   ├── EntityStatusEffectState.cs
│   │   ├── CombatDamageCalculator.cs
│   │   ├── MissileZoneTickSystem.cs
│   │   ├── MissileSimulationTickSystem.cs
│   │   ├── StatusEffectZoneTickSystem.cs
│   │   ├── CombatProgressionCommandHandler.cs
│   │   ├── CastSkillZoneCommand.cs
│   │   ├── ChangeAuraSkillZoneCommand.cs
│   │   └── ChangePkModeZoneCommand.cs
│   └── PlayerSessions/
│       ├── ICombatOutboundSink.cs
│       └── NoOpCombatOutboundSink.cs
└── JXServer.Adapter.LegacyJX1/
    ├── Packets/
    │   ├── NpcSkillC2SPacket.cs
    │   ├── AutoAttackNpcC2SPacket.cs
    │   ├── ChangeAuraSkillC2SPacket.cs
    │   ├── PkApplyChangeNormalFlagC2SPacket.cs
    │   ├── SkillCastS2CPacket.cs
    │   ├── CastSkillDirectlyS2CPacket.cs
    │   └── BroadcastNetCommandS2CPacket.cs
    └── GameServer/
        ├── GameServerClientSession.CombatSink.cs
        ├── NpcSkillPacketHandler.cs
        ├── AutoAttackNpcPacketHandler.cs
        ├── ChangeAuraSkillPacketHandler.cs
        └── PkApplyChangeNormalFlagPacketHandler.cs

tests/
├── JXServer.Protocol.Tests/
│   ├── CombatAndSkillPacketTests.cs
│   └── SixtyFourDirectionAndFiveElementsMathTests.cs
├── JXServer.ZoneEngine.Tests/
│   └── CombatAndMissileSimulationTests.cs
└── JXServer.Integration.Tests/
    └── CombatSkillIntegrationTests.cs
```
