# Quickstart: Five-Elements Combat, 64-Direction Missiles, Monster AI, Loot Generation & Inventory Management

**Feature Branch**: `003-combat-missiles-loot-inventory`  
**Date**: 2026-11-09  

---

## 1. Prerequisites

- **.NET 10 SDK (`net10.0`)** installed.
- Ground-truth reference assets present at `d:\Projects\jx-linux-bin\server1` (for running the offline `tools/JXServer.DataExporter` CLI).

---

## 2. Export Canonical `JXData Standard v1` Catalogs & `MemoryPack` Snapshots

Run the standalone offline exporter CLI to extract legacy `.pak`/`.dat`/`.txt`/`.ini` and static `.lua` curves into canonical UTF-8 JSON (`data/**/*.json`), validate against `schemas/*.schema.json`, and compile `artifacts/*.pack`:

```powershell
cd d:\Projects\jx-server
dotnet run --project tools/JXServer.DataExporter -- `
  --source "d:\Projects\jx-linux-bin\server1" `
  --data-dir "d:\Projects\jx-server\data" `
  --schemas-dir "d:\Projects\jx-server\schemas" `
  --artifacts-dir "d:\Projects\jx-server\artifacts"
```

---

## 3. Build & Run Automated Verification Suites

```powershell
cd d:\Projects\jx-server
dotnet build JXServer.slnx -warnaserror
dotnet test JXServer.slnx --no-build
```

### Key Test Suites Verified
1. **`JXServer.Protocol.Tests`**:
   - Byte-exact serialization/deserialization symmetry for all Milestone 3 combat, progression, ground object, and inventory packets (`0x4A`, `0x4D`, `0x56`, `0x57`, `0x58`, `0x59`, `0x5A`, `0x5B`, `0x5D`, `0x5E`, `0x5F`, `0x60`, `0x61`, `0x64`, `0x67`, `0x6F`, `0x72`, `0x74`, `0x76`, `0x83`, `0x85`, `0x87`, `0x89`, `0x8E`).
   - 64-direction trigonometry (`SixtyFourDirectionTrigTable`) and `SkillCheckKey = 0x3AB9` anti-cheat validation.
2. **`JXServer.ZoneEngine.Tests`**:
   - JSON Schema (`schemas/*.schema.json`) and `MemoryPack` (`artifacts/*.pack`) catalog loading (`SkillTemplateCatalog`, `MissileTemplateCatalog`, `ItemTemplateCatalog`, `LootTableCatalog`).
   - Deterministic 18-Hz `MissileSimulationTickSystem` trajectory stepping, obstacle collision, and Five-Elements `CombatDamageCalculator` (Cold slow, Poison DoT blending, Lightning stun).
   - `MonsterAiTickSystem` state transitions (`IdlePatrol` $\rightarrow$ `Pursuit` $\rightarrow$ `Attack` $\rightarrow$ `LeashReturn` $\rightarrow$ `DeadRespawnWait`), experience/level-up progression, and attribute/skill point allocation.
   - `ItemGenerator` 6-affix Blue equipment generation (`m_wVersion = 4`), `PlayerInventoryGrid` `6x10` placement & `KItemList::ms_ActivedEquip` Five-Elements hidden affix activation, and concurrent ground-item pickup race protection.
3. **`JXServer.Integration.Tests`**:
   - End-to-end TCP Port `5622` + `6666` workflow: login $\rightarrow$ world entry with starter potions/scroll (`0x5F`, `0x61`) $\rightarrow$ walk out town gate (`FightMode = 1`) $\rightarrow$ cast skill (`0x4D`) at field monster $\rightarrow$ projectile hit (`0x56` `do_hurt`) $\rightarrow$ monster death (`0x57`) + XP/level-up (`0x59`, `0x5B`) + ground loot spawn (`0x64`) $\rightarrow$ allocate attribute/skill points (`0x57`, `0x58`) $\rightarrow$ pick up gold & 6-affix Blue equipment (`0x5A` $\rightarrow$ `0x5F`, `0x61`, `0x67`) $\rightarrow$ equip item (`0x5B` $\rightarrow$ `0x8E`) $\rightarrow$ drink potion / use Town Portal (`0x59` / `0x87`) $\rightarrow$ disconnect & verify database persistence.

---

## 4. Live 50-Bot `JX-Swarm` Combat & Looting Verification

1. Start `JXServer.Host`:
   ```powershell
   dotnet run --project src/JXServer.Host
   ```
2. Launch `JXSwarm.Drone.Host` with 50 bots entering Map `53` (`Ba Lăng Huyện`), exiting town gates into `FightMode = 1`, hunting field monsters, looting dropped items, and verifying `99th-percentile` tick latency remains `< 15ms`.
