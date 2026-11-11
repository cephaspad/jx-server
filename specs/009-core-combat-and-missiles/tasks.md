# Tasks: Core Combat and 64-Direction Missiles

**Feature Branch**: `009-core-combat-and-missiles`  
**Status**: Completed & Verified  

---

## Task List

- [X] **T01 — Mathematical & Primitive Foundations** `src/JXServer.Primitives/Combat/`
  - [X] Implement 64-direction discrete trigonometric sin/cos lookup buffers and direction index math: `SixtyFourDirectionTrigTable.cs`
  - [X] Implement Five-Elements generation (*Tương Sinh*) and conquest (*Tương Khắc*) interaction matrices: `FiveElementsSeriesMatrix.cs`
  - [X] Define combat constants and mitigation limits: `CombatConsts.cs`
  - [X] Define combat domain enumerations: `SkillStyleKind.cs`, `MissileMoveKind.cs`, `DamageChannelKind.cs`, `PlayerPkMode.cs`, `EntityRelationKind.cs`
  - [X] Unit test verification: `tests/JXServer.Protocol.Tests/SixtyFourDirectionAndFiveElementsMathTests.cs`

- [X] **T02 — Wire Packet Serialization & Deserialization** `src/JXServer.Adapter.LegacyJX1/Packets/`
  - [X] Define client skill execution packet struct (`0x4D`): `NpcSkillC2SPacket.cs`
  - [X] Define auto-attack envelope packet struct (`0x87`): `AutoAttackNpcC2SPacket.cs`
  - [X] Define aura skill switch packet struct (`0x6F`): `ChangeAuraSkillC2SPacket.cs`
  - [X] Define PK flag mode change packet struct (`0x76`): `PkApplyChangeNormalFlagC2SPacket.cs`
  - [X] Define server skill cast broadcast packet struct (`0x5A`): `SkillCastS2CPacket.cs`
  - [X] Define direct instant cast packet struct (`0x85`): `CastSkillDirectlyS2CPacket.cs`
  - [X] Define hurt recoil net command packet struct (`0x56`): `BroadcastNetCommandS2CPacket.cs`
  - [X] Wire packet test verification: `tests/JXServer.Protocol.Tests/CombatAndSkillPacketTests.cs`

- [X] **T03 — Game Asset Skill & Missile Catalogs** `src/JXServer.GameAssets/Skills/`
  - [X] Define static skill templates and 3-parameter scaling curves: `SkillTemplate.cs`, `SkillCostScalingTemplate.cs`, `SkillEffectScalingTemplate.cs`, `SkillParameterCurveTemplate.cs`
  - [X] Define static missile movement and collision templates: `MissileTemplate.cs`
  - [X] Implement catalog contracts: `ISkillTemplateCatalog.cs`, `IMissileTemplateCatalog.cs`

- [X] **T04 — Zone Simulation Missile Loop & Damage Calculator** `src/JXServer.ZoneEngine/Combat/`
  - [X] Implement pooled 18-Hz projectile slot: `ActiveMissileSlot.cs`
  - [X] Implement 6-channel damage payload structure: `SkillDamagePayload.cs`
  - [X] Implement Five-Elements combat damage resolution: `CombatDamageCalculator.cs`
  - [X] Implement Phase 300 18-Hz missile simulation tick system: `MissileZoneTickSystem.cs`, `MissileSimulationTickSystem.cs`
  - [X] Zone missile simulation tests: `tests/JXServer.ZoneEngine.Tests/CombatAndMissileSimulationTests.cs`

- [X] **T05 — Status Effects & Hurt Recoil Tick System** `src/JXServer.ZoneEngine/Combat/`
  - [X] Implement status effect tracking state (Cold, Poison, Stun, Hurt): `EntityStatusEffectState.cs`
  - [X] Implement Phase 100 18-Hz status effect tick system: `StatusEffectZoneTickSystem.cs`
  - [X] Wire hurt notification broadcast to `ICombatOutboundSink.SendHurtBroadcast`: `src/JXServer.ZoneEngine/PlayerSessions/ICombatOutboundSink.cs`

- [X] **T06 — Command Handlers & Outbound Notification Sinks**
  - [X] Define combat commands: `CastSkillZoneCommand.cs`, `ChangeAuraSkillZoneCommand.cs`, `ChangePkModeZoneCommand.cs`
  - [X] Implement command handler for skill execution and aura toggling: `CombatProgressionCommandHandler.cs`
  - [X] Implement legacy game server combat outbound sink: `src/JXServer.Adapter.LegacyJX1/GameServer/GameServerClientSession.CombatSink.cs`
  - [X] End-to-end integration test verification: `tests/JXServer.Integration.Tests/CombatSkillIntegrationTests.cs`
