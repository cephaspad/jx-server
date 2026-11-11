# Quickstart: Core Combat and 64-Direction Missiles

**Feature Branch**: `009-core-combat-and-missiles`  
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

### 3.1 64-Direction Trigonometry & Five-Elements Math Tests
Verifies discrete heading vectors, sin/cos lookup buffers, and element interaction matrices:

```powershell
dotnet test tests/JXServer.Protocol.Tests --filter "FullyQualifiedName~SixtyFourDirectionAndFiveElementsMathTests" --no-restore
```

### 3.2 Wire Packet Framing & Cipher Serialization Tests
Verifies byte-level layout of `0x4D`, `0x87`, `0x6F`, `0x5A`, `0x85`, and `0x56`:

```powershell
dotnet test tests/JXServer.Protocol.Tests --filter "FullyQualifiedName~CombatAndSkillPacketTests" --no-restore
```

### 3.3 Zone Simulation Harness & Missile Trajectory Tests
Verifies deterministic 18-Hz missile stepping, hit resolution, and status effects:

```powershell
dotnet test tests/JXServer.ZoneEngine.Tests --filter "FullyQualifiedName~CombatAndMissileSimulationTests" --no-restore
```

### 3.4 End-to-End Combat & Skill Integration Tests
Verifies full loopback skill cast, AOI broadcast, projectile collision, and vital reductions:

```powershell
dotnet test tests/JXServer.Integration.Tests --filter "FullyQualifiedName~CombatSkillIntegrationTests" --no-restore
```

---

## 4. Key Verification Checklist

- [x] SixtyFourDirectionTrigTable produces exact heading vectors for all 64 discrete directions.
- [x] FiveElementsSeriesMatrix accurately evaluates generational and conquest multiplier scaling.
- [x] MissileZoneTickSystem steps projectiles without floating-point accumulation errors.
- [x] StatusEffectZoneTickSystem decrements freeze, stun, and hurt timers while evaluating poison ticks.
- [x] CastSkillZoneCommand enforces cooldowns, vital costs, range, and safe-zone restrictions.
