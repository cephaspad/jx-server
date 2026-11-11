# Quickstart: Core Monster AI, Death, Progression & Revival

**Feature Branch**: `010-core-monster-ai-and-progression`  
**Date**: 2026-11-11  

---

## 1. Prerequisites

- .NET 10 SDK (`net10.0`)
- Working directory: `d:\Projects\jx-server`

---

## 2. Build Verification

Build the entire solution with zero warnings:

```powershell
dotnet build JXServer.slnx -warnaserror
```

---

## 3. Running Unit and Simulation Tests

### 3.1 Wire Packet Serialization & Validation Tests
Verifies byte layouts of `0x57`, `0x58`, `0x74`, `0x89`, `0x5D`, `0x5E`, `0x5C`, and `0x5B`:

```powershell
dotnet test tests/JXServer.Protocol.Tests --filter "FullyQualifiedName~ProgressionAndRevivalPacketTests" --no-restore
```

### 3.2 Monster AI State Machine & Progression Unit Tests
Verifies 18-Hz patrol, aggro pursuit, leash return, respawn timing, and point allocation math:

```powershell
dotnet test tests/JXServer.ZoneEngine.Tests --filter "FullyQualifiedName~MonsterAiAndProgressionTests" --no-restore
```

### 3.3 End-to-End AI & Progression Integration Tests
Verifies end-to-end combat kill, experience gain, level-up trigger, point allocation, player death, and town revival:

```powershell
dotnet test tests/JXServer.Integration.Tests --filter "FullyQualifiedName~MonsterAiAndProgressionIntegrationTests" --no-restore
```

---

## 4. Key Verification Checklist

- [x] Monster AI transitions correctly across `IdlePatrol` $\rightarrow$ `Pursuit` $\rightarrow$ `Attack` $\rightarrow$ `LeashReturn` $\rightarrow$ `DeadRespawnWait`.
- [x] Monsters pulled beyond leash distance safely disengage and return to spawn coordinates without pathing locks.
- [x] Slaying a monster awards experience points and triggers level-up when crossing threshold.
- [x] Level-up accurately awards 5 base attribute points and 1 skill point per level advanced.
- [x] Base attribute allocation updates derived stats (HP, Mana, Damage, Evasion) and synchronizes via `0x5D`.
- [x] Skill point allocation increments learned skill level and synchronizes via `0x5E`.
- [x] Player death resets posture to peaceful mode and revival repositions the player at their saved town coordinates.
