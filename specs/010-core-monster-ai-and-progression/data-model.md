# Data Model: Core Monster AI, Death, Progression & Revival

**Feature Branch**: `010-core-monster-ai-and-progression`  
**Date**: 2026-11-11  

---

## 1. Primitives & Enums (`JXServer.Primitives/Combat/`)

| Type Name | Type Kind | Values / Description |
| :--- | :--- | :--- |
| `MonsterAiStateKind` | `enum : byte` | `IdlePatrol = 0`, `Pursuit = 1`, `Attack = 2`, `LeashReturn = 3`, `DeadRespawnWait = 4`. |
| `BaseAttributeKind` | `enum : byte` | `Strength = 0`, `Dexterity = 1`, `Vitality = 2`, `Energy = 3`. |

---

## 2. Static Templates & Catalogs (`JXServer.GameAssets/`)

### 2.1 NPC / Monster Templates (`Npcs/`)
- **`NpcTemplate` (`sealed record`)**:
  - `Id`, `Code`, `Name`: Identification.
  - `Kind`: `EntityKind.Monster` vs `EntityKind.DialogNpc`.
  - `Level`, `Series`: Level and Five-Elements alignment.
  - `LifeMax`, `ManaMax`, `SpeedMpsPerFrame`: Base combat vitals.
  - `AttackRadiusMps`, `VisionRadiusMps`, `MaxLeashRadiusMps`: AI spatial thresholds.
  - `AttackSkillId`, `AttackSkillLevel`, `AttackRateFrames`: Combat action triggers.
  - `ExperienceValue`: Experience awarded upon defeat.
  - `RespawnFrames`: Delay before reappearing after death.
  - `LootTableCode`: Loot drop table reference.

### 2.2 Player Progression Templates (`PlayerTemplates/`)
- **`CharacterBaseTemplate` (`sealed partial record`)**:
  - Baseline vitality, energy, strength, dexterity attributes and gender/series definitions.
- **`PlayerSeriesGrowthRuleTemplate` (`sealed partial record`)**:
  - Class-based health/mana/stamina growth multipliers for Strength, Dexterity, Vitality, and Energy (`level_add.txt`).
- **`IPlayerTemplateCatalog` / `PlayerTemplateCatalog`**:
  - `GetRequiredExpForNextLevel(int currentLevel)`: Experience thresholds per level (`level_exp.txt`).
  - `GetSeriesGrowthRule(ElementSeries series)`: Series growth multiplier lookup.

---

## 3. Zone State & Commands (`JXServer.ZoneEngine/`)

### 3.1 `NpcZoneState` (`NpcEntities/NpcZoneState.cs`)
In-memory 18-Hz simulation representation for monsters:
- `NpcId`: Runtime numeric identifier (`1..9999`).
- `Template`: Reference to static `NpcTemplate`.
- `CurrentMpsX`, `CurrentMpsY`, `SpawnMpsX`, `SpawnMpsY`: World coordinates.
- `AiState`: Current `MonsterAiStateKind`.
- `CurrentLife`, `CurrentMana`: Real-time vitals.
- `TargetNpcId`, `TargetCharacterId`: Current aggro target.
- `NextDecisionTick`, `AttackCooldownTicks`: Action timing timers.
- `DeathTick`, `RespawnTick`: Respawn scheduling.

### 3.2 Zone Commands
- **`AllocateBaseAttributeZoneCommand`**:
  - `Guid CharacterId`, `long LeaseEpoch`, `BaseAttributeKind Attribute`, `int PointsToAdd`.
- **`AllocateSkillPointZoneCommand`**:
  - `Guid CharacterId`, `long LeaseEpoch`, `int SkillId`, `int PointsToAdd`.
- **`RevivePlayerZoneCommand`**:
  - `Guid CharacterId`, `long LeaseEpoch`, `int ReviveType` (`0` = Town revival).

---

## 4. Domain & Persistence Entities (`JXServer.Domain/Characters/`)

- **`Character` (Progression Fields)**:
  - `Level` (`ushort`), `Experience` (`long`), `LeadExperience` (`long`).
  - `BaseStrength`, `BaseDexterity`, `BaseVitality`, `BaseEnergy`.
  - `UnallocatedAttributePoints` (`int`), `UnallocatedSkillPoints` (`int`).
  - `CurrentLife`, `MaxLife`, `CurrentMana`, `MaxMana`, `CurrentStamina`, `MaxStamina`.
  - `RevivalMapId` (`ushort`), `RevivalPointId` (`ushort`).
  - `Skills`: Persisted list of `CharacterSkillValue` (`SkillId`, `BaseLevel`, `BonusLevel`, `ExpPercent`).

---

## 5. Wire Packets (`JXServer.Adapter.LegacyJX1/Packets/`)

| Packet Name | Opcode | Direction | Size | Layout Summary |
| :--- | :---: | :---: | :---: | :--- |
| `PlayerAddBaseAttributeC2SPacket` | `0x57` | C2S | 14B | `<BBiII`: Opcode, AttributeKind (`0..3`), AddPoints, Reserved. |
| `PlayerApplyAddSkillPointC2SPacket` | `0x58` | C2S | 17B | `<BiiII`: Opcode, SkillId, AddPoints, Reserved. |
| `PlayerReviveC2SPacket` | `0x74` | C2S | 13B | `<BiII`: Opcode, ReviveType (`0`), Reserved. |
| `NpcDeathS2CPacket` | `0x57` | S2C | 5B | `<BI`: Opcode, NpcId. |
| `NpcReviveS2CPacket` | `0x89` | S2C | 6B | `<BIB`: Opcode, NpcId, ReviveType. |
| `PlayerAttributeS2CPacket` | `0x5D` | S2C | 14B | `<BBiii`: Opcode, AttributeKind, BasePoint, CurPoint, LeavePoints. |
| `PlayerSkillLevelS2CPacket` | `0x5E` | S2C | 15B | `<Biiih`: Opcode, SkillId, SkillLevel, LeavePoints, ExpPercent. |
| `PlayerExpS2CPacket` | `0x5C` | S2C | 5B | `<Bi`: Opcode, CurrentExp. |
| `PlayerLevelUpS2CPacket` | `0x5B` | S2C | 17B | `<Biiii`: Opcode, LeaveAttrPoints, LeaveSkillPoints, NewLevel, NextLevelMaxExp. |
