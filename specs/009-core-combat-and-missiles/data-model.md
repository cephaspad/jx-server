# Data Model: Core Combat and 64-Direction Missiles

**Feature Branch**: `009-core-combat-and-missiles`  
**Date**: 2026-11-11  

---

## 1. Primitives & Mathematical Foundations (`JXServer.Primitives/Combat/`)

| Type Name | Type Kind | Description & Key Members |
| :--- | :--- | :--- |
| `SixtyFourDirectionTrigTable` | `static class` | Pre-calculated 1024-scaled trigonometric tables (`SinTable[64]`, `CosTable[64]`), `GetDirectionIndex(srcX, srcY, destX, destY)`, and direction conversion helpers. |
| `FiveElementsSeriesMatrix` | `static class` | Elemental rules: `Generates(src, target)` (Kim $\rightarrow$ Thủy $\rightarrow$ Mộc $\rightarrow$ Hỏa $\rightarrow$ Thổ $\rightarrow$ Kim) and `Conquers(src, target)` (Kim $\rightarrow$ Mộc $\rightarrow$ Thổ $\rightarrow$ Thủy $\rightarrow$ Hỏa $\rightarrow$ Kim). |
| `CombatConsts` | `static class` | Clamps and factors: `MinHitPercent = 5`, `MaxHitPercent = 95`, `DefaultMaxResistPercent = 75`, `HardCapResistPercent = 95`, `DefaultPkDamageRatePercent = 20`, `BroadcastNetCmdHurt = 9`. |
| `DamageChannelKind` | `enum : byte` | `Physics = 0`, `Cold = 1`, `Fire = 2`, `Light = 3`, `Poison = 4`, `Magic = 5`. |
| `SkillStyleKind` | `enum : byte` | `Melee = 0`, `Projectile = 1`, `SelfBuff = 2`, `TargetBuff = 3`, `Aura = 4`, `Passive = 5`, `SummonNpc = 6`, `Jump = 7`. |
| `MissileMoveKind` | `enum : byte` | `Stand = 0`, `Stationary = 1`, `Line64 = 2`, `Random = 3`, `Circle = 4`, `Spread = 5`, `Follow = 8`, `Parabola = 10`. |
| `PlayerPkMode` | `enum : byte` | `Peace = 0`, `Fight = 1`, `Slaughter = 2`. |
| `EntityRelationKind` | `enum : byte` | Bitmask: `None = 1`, `Friend = 2`, `Enemy = 4`, `Self = 8`, `All = 15`. |

---

## 2. Static Catalogs (`JXServer.GameAssets/Skills/`)

| Type Name | Type Kind | Description & Key Fields |
| :--- | :--- | :--- |
| `SkillTemplate` | `sealed record` | Static template: `Id`, `Code`, `Name`, `Style`, `ElementSeries`, `IsOffensive`, `IsPhysical`, `IsMelee`, `IsAura`, `CastRangeMps`, `WaitFrames`, `MissileId`, `CostScaling`, `EffectScaling`. |
| `MissileTemplate` | `sealed record` | Static template: `Id`, `Code`, `Name`, `MoveKind`, `SpeedMpsPerFrame`, `LifeFrames`, `HitRadiusMps`, `DamageIntervalFrames`, `PierceCount`, `CollideVanish`, `AutoExplode`. |
| `SkillCostScalingTemplate` | `sealed record` | Evaluates mana/stamina costs per skill level: `BaseMana`, `ManaPerLevel`, `BaseStamina`, `ValuesByLevel`. |
| `SkillEffectScalingTemplate` | `sealed record` | Multi-parameter curve definitions mapping skill level to physical and elemental damage ranges. |

---

## 3. Zone Simulation State & Commands (`JXServer.ZoneEngine/Combat/`)

### 3.1 Simulation Structures
- **`ActiveMissileSlot` (`sealed class`)**:
  Pre-allocated slot representing an in-flight projectile:
  - `SlotIndex`, `IsActive`: Pooling control.
  - `CasterNpcId`, `CasterCharacterId`, `CasterCamp`: Attacker provenance.
  - `SkillId`, `SkillLevel`, `MissileTemplateId`, `MoveKind`: Skill linkage.
  - `CurrentMpsX`, `CurrentMpsY`, `FixedX`, `FixedY`: Integer world & fixed-point coordinates.
  - `StepFixedX`, `StepFixedY`, `DirIndex`: Velocity vector and 64-direction heading.
  - `RemainingLifeFrames`, `HitRadiusMps`, `RemainingPierceCount`, `CollideVanish`: Flight invariants.
  - `DamagePayload`: Attached pre-computed `SkillDamagePayload`.

- **`SkillDamagePayload` (`readonly record struct`)**:
  - `AttackRating`, `IgnoreDefensePercent`.
  - `PhysicsMin`, `PhysicsMax`, `ColdMin`, `ColdMax`, `ColdFreezeFrames`.
  - `FireMin`, `FireMax`, `LightMin`, `LightMax`, `LightStunFrames`.
  - `PoisonDamagePerTick`, `PoisonDurationFrames`, `PoisonIntervalFrames`.
  - `MagicMin`, `MagicMax`, `UseAttackRating`, `DoHurtAnimation`, `IsMelee`.

- **`EntityStatusEffectState` (`struct`)**:
  - `FreezeRemainingFrames`: Cold slowdown timer.
  - `PoisonRemainingFrames`, `PoisonDamagePerTick`, `PoisonIntervalFrames`, `NextPoisonTickFrame`: Poison tick counters.
  - `StunRemainingFrames`: Stun immobilization timer.
  - `HurtRecoveryRemainingFrames`: Hurt recoil lockout timer.

### 3.2 Zone Commands
- `CastSkillZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `int SkillId`, `int TargetMpsX`, `int TargetMpsYOrNpcId`.
- `ChangeAuraSkillZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `int AuraSkillId`.
- `ChangePkModeZoneCommand`: `Guid CharacterId`, `long LeaseEpoch`, `PlayerPkMode RequestedPkMode`.

---

## 4. Wire Packets (`JXServer.Adapter.LegacyJX1/Packets/`)

| Packet Name | Opcode | Direction | Size | Layout Summary |
| :--- | :---: | :---: | :---: | :--- |
| `NpcSkillC2SPacket` | `0x4D` | C2S | 17B | `<BiiiI`: Opcode, TargetMpsYOrNpcId, SkillId, TargetMpsX, CheckValue. |
| `AutoAttackNpcC2SPacket` | `0x87` | C2S | 26B | `<BB24s`: Opcode, SubCommand (`0x03` wrapped skill / `0x02` portal), payload. |
| `ChangeAuraSkillC2SPacket` | `0x6F` | C2S | 5B | `<Bi`: Opcode, AuraSkillId. |
| `PkApplyChangeNormalFlagC2SPacket` | `0x76` | C2S | 2B | `<BB`: Opcode, PkFlag (`0` Peace, `1` Fight, `2` Slaughter). |
| `SkillCastS2CPacket` | `0x5A` | S2C | 25B | `<BiIiIII`: Opcode, TargetMpsX, SkillId, TargetMpsYOrNpcId, CasterNpcId, Level, Reserved. |
| `CastSkillDirectlyS2CPacket` | `0x85` | S2C | 25B | `<BiIiIII`: Direct instant cast broadcast. |
| `BroadcastNetCommandS2CPacket` | `0x56` | S2C | 22B | `<BIBiiii`: Opcode, NpcId, CmdKind (`9` `do_hurt`), MpsX, MpsY, HurtFrames, Reserved. |
