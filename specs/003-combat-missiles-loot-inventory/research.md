# Phase 0 Research: Five-Elements Combat, 64-Direction Missiles, Monster AI, Loot Generation & Inventory Management

**Feature Branch**: `003-combat-missiles-loot-inventory`  
**Date**: 2026-11-09  
**Spec**: [spec.md](spec.md)  
**Ground-Truth Sources**:
- `jx-server/docs/data-architecture-standard.md` (`JXData Standard v1`) & `schemas/*.schema.json`
- `jx-swarm/docs/02-game-systems/movement-and-combat.md`
- `jx-swarm/docs/02-game-systems/npc-dialog-inventory-and-economy.md`
- `jx-swarm/docs/03-asset-formats/map-and-settings.md`
- `jx-win-src/Sources/Core/Src/` (`KMath.h`, `KMath.cpp`, `KSkills.cpp`, `KMissle.cpp`, `KNpc.cpp`, `KNpcSet.cpp`, `KNpcAI.cpp`, `KNpcAttribModify.cpp`, `KItemGenerator.cpp`, `KItemList.cpp`, `KItem.cpp`, `KObj.cpp`, `KPlayer.cpp`)
- `jx-linux-bin/server1/settings/` (`skills.txt`, `missles.txt`, `npcs.txt`, `item/004/*.txt`, `droprate/*.ini`, `npc/player/level_exp.txt`, `npc/player/level_add.txt`, `obj/objdata.txt`) and `script/skill/**/*.lua`, `script/npclevelscript/*.lua`

---

## 1. Dual-Tier Game Data Pipeline (`JXData Standard v1`) & Offline Ground-Truth Export

- **Decision**: Transition `JXServer.GameAssets` completely to the **Dual-Tier Game Data Standard (`JXData Standard v1`)** mandated by Constitution `v1.2.0` and `docs/data-architecture-standard.md`:
  1. **Offline Exporter CLI (`tools/JXServer.DataExporter`)**: Reads legacy `.pak`/`.dat`/`.txt`/`.ini` files and evaluates static Lua curve scripts (`script/skill/**/*.lua`, `script/npclevelscript/*.lua`, `script/**/trap/*.lua`, `script/global/autoexec.lua`) from `d:\Projects\jx-linux-bin\server1`, transcoding `TCVN3`/`GBK` strings into UTF-8 and emitting canonical JSON catalogs under `data/` (`maps/`, `npcs/`, `players/`, `skills/`, `items/`, `loot/`, `shops/`) validated against `schemas/*.schema.json`, plus compiled `MemoryPack` snapshots under `artifacts/*.pack`.
  2. **Runtime Catalog Loader (`JXServer.GameAssets`)**: Loads exclusively from `artifacts/*.pack` (in production/swarm mode) or `data/**/*.json` (in development/test mode) into immutable `FrozenDictionary` / `ReadOnlyMemory<T>` registries (`IMapCatalog`, `IPlayerTemplateCatalog`, `INpcTemplateCatalog`, `ISkillTemplateCatalog`, `IMissileTemplateCatalog`, `IItemTemplateCatalog`, `ILootTableCatalog`). Zero legacy `.txt`, `.ini`, `.dat`, or `.lua` files are parsed at server runtime.
- **Rationale**: Eliminates 105-column untyped tab files, legacy `TCVN3`/`GBK` encodings, and runtime Lua execution from the 18-Hz server while preserving 100% numeric and wire-protocol fidelity via `id` + `code` dual identifiers.
- **Alternatives Considered**:
  - *Embedding a Lua 4.0 VM inside the 18-Hz Zone Loop*: Rejected by Constitution Principle VI (`C# 14 Fluent Scripting API` & `Declarative Data Rules`).
  - *Parsing raw `.txt`/`.ini` files on server startup*: Rejected by Constitution Principle V (`JXData Standard v1`).

---

## 2. 64-Direction Fixed-Point Trigonometry & Projectile (`KMissle`) Physics

- **Decision**: Implement `SixtyFourDirectionTrigTable` in `JXServer.Primitives` using the exact 64-element fixed-point sine and cosine lookup tables (`g_nSinBuffer[64]` and `g_nCosBuffer[64]` scaled by `1024`) and direction index lookup (`g_GetDirIndex`) verified in `jx-win-src/Sources/Core/Src/KMath.cpp` and `KMath.h`:
  - **`g_nSin[64]`**:
    ```text
     1024,  1019,  1004,   979,   946,   903,   851,   791,
      724,   649,   568,   482,   391,   297,   199,   100,
        0,  -100,  -199,  -297,  -391,  -482,  -568,  -649,
     -724,  -791,  -851,  -903,  -946,  -979, -1004, -1019,
    -1024, -1019, -1004,  -979,  -946,  -903,  -851,  -791,
     -724,  -649,  -568,  -482,  -391,  -297,  -199,  -100,
        0,   100,   199,   297,   391,   482,   568,   649,
      724,   791,   851,   903,   946,   979,  1004,  1019
    ```
  - **`g_nCos[64]`**:
    ```text
        0,  -100,  -199,  -297,  -391,  -482,  -568,  -649,
     -724,  -791,  -851,  -903,  -946,  -979, -1004, -1019,
    -1024, -1019, -1004,  -979,  -946,  -903,  -851,  -791,
     -724,  -649,  -568,  -482,  -391,  -297,  -199,  -100,
        0,   100,   199,   297,   391,   482,   568,   649,
      724,   791,   851,   903,   946,   979,  1004,  1019,
     1024,  1019,  1004,   979,   946,   903,   851,   791,
      724,   649,   568,   482,   391,   297,   199,   100
    ```
  - **Direction Index Formula (`g_GetDirIndex(x1, y1, x2, y2)`)**:
    1. `distance = (int)Math.Sqrt((x1 - x2)*(x1 - x2) + (y1 - y2)*(y1 - y2))` (returns `-1` if `0`).
    2. `nSin = ((y2 - y1) << 10) / distance`.
    3. Scan `i = 0..31`: while `nSin <= g_nSin[i]`, set `nRet = i`; else `break`.
    4. If `(x2 - x1) > 0`, mirror across the vertical axis: `nRet = 63 - nRet`.
  - **Per-Tick Projectile Step (`KMissle::OnFly` & `KSkills::CastExtractiveLineMissle`)**:
    - For a 64-direction projectile (`DirIndex` in `0..63`) with `SpeedMpsPerFrame`:
      - Fixed-point sub-pixel accumulators (`FixedX = MpsX << 10`, `FixedY = MpsY << 10`) advance by `DeltaFixedX = SpeedMpsPerFrame * g_nCos[DirIndex]` and `DeltaFixedY = SpeedMpsPerFrame * g_nSin[DirIndex]` each 18-Hz tick (`MpsX = FixedX >> 10`, `MpsY = FixedY >> 10`).
      - For single-projectile direct line casts (`CastExtractiveLineMissle`), exact `(nCos, nSin)` factors toward `(DesPX, DesPY)` are used.
      - For `spread` (`CastSpread`), child projectiles fan out around `DirIndex` separated by `Param1` direction steps (`(DirIndex + offset) & 63`).
      - For `wall` (`CastWall`), projectiles spawn along the perpendicular axis `(DirIndex + 16) & 63`.
      - For `circle` (`CastCircle`), `count` projectiles distribute evenly across `0..63`.
    - Each tick checks map obstacle cells (`Obstacle_Full == 1` or `Obstacle_Fly == 3` in `KMissle::TestBarrier`) and hostile targets within `CollideRadiusCells` / `HitRadiusMps`.
- **Rationale**: Byte-for-byte and frame-for-frame identical to `KMath.cpp`, `KSkills.cpp`, and `KMissle.cpp`, ensuring client-rendered projectiles match server hit detection without sending per-frame missile position packets.

---

## 3. Five-Elements (`Ngũ Hành`) Combat, Relations, Resistances & Status Ailments

- **Decision**: Implement `CombatResolutionTickSystem` and `CombatDamageCalculator` in `JXServer.ZoneEngine` directly from `KNpc.cpp` (`AppendSkillEffect`, `CheckHitTarget`, `ReceiveDamage`, `CalcDamage`) and `KNpcSet.cpp` (`GetRelation`):
  1. **Hostile Target Relation (`KNpcSet::GetRelation`)**:
     - Self (`CasterNpcId == TargetNpcId`) $\rightarrow$ `RelationSelf`.
     - Either entity has `Kind == DialogNpc` or `FightMode == 0` (Peace zone) $\rightarrow$ `RelationNone` (cannot be damaged).
     - Player vs. Monster (`Kind == Monster`) with different `CurrentCamp` (`Camp != Animal/Evil` vs player camp) $\rightarrow$ `RelationEnemy`.
     - Player vs. Player: requires both players to have `FightMode == 1`, non-identical `CurrentCamp` (or `Free` camp), and active normal PK flag (`PkState > 0`, toggled via `0x76` `c2s_pkapplychangenormalflag` with `3240`-frame / 3-minute cooldown when switching back to `0` Peace).
  2. **Attack Rating & Evasion (`KNpc::CheckHitTarget`)**:
     - When `skill.UseAttackRating == true`:
       - `effectiveDefense = targetDefense * (100 - attackerIgnoreDefensePercent) / 100`
       - `hitPercent = (attackRating + effectiveDefense == 0) ? 50 : (attackRating * 100) / (attackRating + effectiveDefense)`
       - Clamped to `[MIN_HIT_PERCENT (5), MAX_HIT_PERCENT (95)]`.
  3. **Six Damage Channels & Resistance Mitigation (`KNpc::CalcDamage`)**:
     - Channels: `Physics`, `Cold`, `Fire`, `Light` (Lightning), `Poison`, and `Magic` (unmitigated).
     - Base weapon physical damage (`KItemList::GetWeaponDamage`):
       - Unarmed: `Min = Max = CurStrength / 5 + 1` (`STRENGTH_SET_DAMAGE_VALUE = 5`).
       - Armed: `(BaseMin + FlatMinBonus) * (100 + WeaponEnhancePercent) / 100` to `(BaseMax + FlatMaxBonus) * (100 + WeaponEnhancePercent) / 100`, scaled by `physicsenhance_p` + weapon mastery (`meleeenhance_p` / `rangeenhance_p` / `handenhance_p`).
     - Five-Elements Counter (`g_IsConquer` / `fiveElementsMatrix` from `KMath.cpp` & `series.ini`):
       - `Metal (0)` conquers `Wood (1)` $\rightarrow$ `Wood (1)` conquers `Earth (4)` $\rightarrow$ `Earth (4)` conquers `Water (2)` $\rightarrow$ `Water (2)` conquers `Fire (3)` $\rightarrow$ `Fire (3)` conquers `Metal (0)`.
       - Elemental resistance for each channel is clamped to `min(CurrentResist, MaxResist)` (default `MaxResist = 75%`, max cap `95%`).
       - Final channel damage: `damage = rawDamage * (100 - effectiveResist) / 100` (and scaled by `PKDamageRate` in PvP).
  4. **Elemental Status Effects (`KNpc::ReceiveDamage`)**:
     - **Cold (`m_FreezeState`)**: Applies movement/attack slow for `freezeFrames * (100 - FreezeTimeReducePercent) / 100` ticks when not already frozen.
     - **Poison (`m_PoisonState`)**: Blends periodic poison damage `d`, duration `t` (reduced by `PoisonTimeReducePercent`), and tick interval `c` using the exact weighted formula from `KNpc.cpp`:
       - `newDamage = ((c1 + c2) * d1 / c1 + (c1 + c2) * d2 / c2) / 2`
       - `newTime = (t1 * d1 * c2 + t2 * d2 * c1) / (d1 * c2 + d2 * c1)`
       - `newInterval = (c1 + c2) / 2`
     - **Lightning Stun (`m_StunState`)**: Applies stun duration frames (`StunFrames * (100 - StunTimeReducePercent) / 100`) when rolled.
  5. **Hurt Animation & Vital Sync**:
     - When `damage > 0` and `doHurtAnimation == true` (and target is not on hurt recovery cooldown), transition posture to `Hurt` for `HitRecoverFrames` and broadcast `0x56` (`s2c_broadcasenetcommand`, 22B, `CmdKind = 9` `do_hurt`) to the `3x3` region AOI.
     - Send `0x46` (`s2c_synccurplayernormal`, 14B) to the damaged player and `0x4D` (`s2c_syncnpcmin`, 18B with updated `LifePer`) to surrounding observers.

---

## 4. Monster AI State Machine, Experience, Leveling & Revival

- **Decision**:
  1. **Monster 18-Hz AI State Machine (`MonsterAiTickSystem`)**:
     - Derived from `KNpcAI.cpp` (`ProcessAI`, `FollowPeople`, `MoveAround`):
       - `IdlePatrol`: Monster waits for `AiMaxTimeFrames` (`36` frames = 2s), occasionally walks to a random obstacle-free point within `ActiveRadiusMps` of `(SpawnMpsX, SpawnMpsY)`, and scans `3x3` regions for hostile players within `VisionRadiusMps` (when `aiMode` is aggressive or monster was attacked).
       - `Pursuit`: Moves toward `TargetNpcId` at `WalkSpeedMpsPerFrame` / `RunSpeedMpsPerFrame`. If distance from `(SpawnMpsX, SpawnMpsY)` exceeds `PursuitRadiusMps` or target dies/enters Peace mode/leaves map, transitions to `LeashReturn`.
       - `Attack`: When within `AttackRadiusMps` and `NextSkillReadyTick <= currentTick`, selects a skill from its 4-slot `skills` list (weighted by `castWeight`), broadcasts `0x5A` (`s2c_skillcast`), and spawns melee hitboxes or projectiles.
       - `LeashReturn`: Walks back to `(SpawnMpsX, SpawnMpsY)`, clears aggro, restores full HP, and resumes `IdlePatrol`.
       - `DeadRespawnWait`: Broadcasts `0x57` (`s2c_npcdeath`, 5B), removes monster from active collision/AOI until `RespawnReadyTick = currentTick + RespawnFrames`, then respawns at `(SpawnMpsX, SpawnMpsY)` with full `MaxLife` and broadcasts `0x4C` (`s2c_syncnpc`) to the `3x3` region AOI.
  2. **Experience, Level-Up & Attribute/Skill Allocation**:
     - On monster death, killer receives `monsterExp` scaled by `mapRules.expMultiplierPercent` and level difference.
     - When `Experience >= levelExpTable[Level].expRequired`, increments `Level`, adds `attributePointsPerLevel` (`5`) to `UnallocatedAttributePoints` and `skillPointsPerLevel` (`1`) to `UnallocatedSkillPoints`, increases `MaxLife`, `MaxMana`, and `MaxStamina` per `seriesGrowthRules` (`level_add.txt`), refills vitals, and sends `0x44` (`s2c_synccurplayer`) + `0x46` (`s2c_synccurplayernormal`).
     - `0x57` (`c2s_playeraddbaseattribute`, 14B) $\rightarrow$ allocates `AddNo` points to `Strength (0)`, `Dexterity (1)`, `Vitality (2)`, or `Energy (3)`, updates `MaxLife`/`MaxMana`/`MaxStamina`/unarmed damage, and replies with `0x5D` (`s2c_playerattribute`, 14B) + `0x46`.
     - `0x58` (`c2s_playerapplyaddskillpoint`, 17B) $\rightarrow$ increments learned skill level by `AddPoint` (validating `UnallocatedSkillPoints >= AddPoint` and `newLevel <= skill.MaxLevel`), recalculates passive/aura stats if applicable, and replies with `0x5E` (`s2c_playerskilllevel`, 15B).
  3. **Player Death & Town Revival**:
     - When player `CurrentLife <= 0`, sets `Doing = Death`, clears monster aggro, and broadcasts `0x57` (`s2c_npcdeath`, 5B).
     - When dead player sends `0x74` (`c2s_playerrevive`, 13B, `ReviveType = 0`), restores `CurrentLife = MaxLife`, `CurrentMana = MaxMana`, `CurrentStamina = MaxStamina`, sets `FightMode = 0` and `Doing = Stand`, teleports player to their saved revival coordinates (`RevivalMapId`, `RevivalPointId` from `starterVillages` in `data/players/progression.players.json`), and broadcasts `0x89` (`s2c_npcrevive`, 6B) + map/position sync (`0x49` + `0x4E` + `0x46`).

---

## 5. 6-Affix Blue Equipment Generation, Five-Elements Activation & `6x10` Inventory

- **Decision**:
  1. **Weighted Loot Drops & 6-Affix Blue Equipment Generator (`ItemGenerator` in `JXServer.GameAssets`)**:
     - Follows `KItemGenerator.cpp` (`Gen_Equipment`, `Gen_MagicAttrib`) and `KNpcTemplate` drop-rate logic with `m_wVersion = 4` (`settings/item/004/*.txt` exported to `data/items/*.items.json` and `data/loot/*.loot.json`):
     - On monster death, rolls `treasureDropCount` iterations against the resolved `LootTableTemplate`:
       - Rolls `moneyDropChance` to spawn a gold pile (`Genre = Money`) scaled by `minMoneyGold..maxMoneyGold`.
       - Rolls `randRange` (`1,000,000`) against `weightedPools`. If an equipment family `(genre = 0, detailType, particularType)` is selected, resolves `itemLevel` (`1..10`) from monster level and rolls up to 6 magic attribute levels `naryMALevel[0..5]` where even slots (`0, 2, 4`) are explicit (`isSuffix = false`, always active) and odd slots (`1, 3, 5`) are hidden Five-Elements affixes (`isSuffix = true`, requiring elemental activation).
       - For each slot `i = 0..5` where `naryMALevel[i] > 0`, filters `magicAffixes` by `isSuffix == (i & 1) != 0`, equipment part weight (`weightsByEquipPart` > 0), element series (`elementSeries == itemSeries` for hidden slots `1, 3, 5`, or any series weighted by `weightsBySeries` for explicit slots `0, 2, 4`), and `level <= naryMALevel[i]`, preventing duplicate `attributeId`s on the same item and rolling parameter values in `[param0Min..param0Max]` using the item's `RandomSeed` and killer's `Luck`.
       - Color classification (`ColorID` in `0x64` `s2c_objadd`): `0` = White (`Quality == 0` with `0` magic affixes), `1` = Blue (`Quality == 0` with $\ge 1$ magic affix), `2` = Purple (`Quality == 2`), `3` = Gold (`Quality == 1`).
  2. **Five-Elements Hidden Affix Activation Matrix (`KItemList::ms_ActivedEquip`)**:
     - Verified directly from `jx-win-src/Sources/Core/Src/KItemList.cpp`:
       - Each equipped item in `pos_equip` (`itempart_head = 0` .. `itempart_pendant = 9`) has up to 3 hidden Five-Elements affix slots (indices `1`, `3`, `5`).
       - Hidden slot `1` and `3` are unlocked by the **2 activating equipment parts** defined in `KItemList::ms_ActivedEquip[part][0..1]` when the equipping player wears an item in those slots whose `ElementSeries` **generates (`Tương Sinh`)** the target item's `ElementSeries` (`g_nAccrueSeries[sourceSeries] == targetSeries`: Metal $\rightarrow$ Water $\rightarrow$ Wood $\rightarrow$ Fire $\rightarrow$ Earth $\rightarrow$ Metal) **AND** the player's own character `Series` also aligns (or unlocks the 3rd hidden slot `5` when all 3 conditions are met):
         - `Head (0)` is activated by `Body (1)` and `Amulet/Neck (6)`
         - `Body (1)` is activated by `Ring2 (8)` and `Belt (2)`
         - `Belt (2)` is activated by `Pendant (9)` and `Cuff (5)`
         - `Weapon (3)` is activated by `Amulet/Neck (6)` and `Body (1)`
         - `Foot/Boot (4)` is activated by `Weapon (3)` and `Head (0)`
         - `Cuff (5)` is activated by `Foot/Boot (4)` and `Ring1 (7)`
         - `Amulet/Neck (6)` is activated by `Belt (2)` and `Ring2 (8)`
         - `Ring1 (7)` is activated by `Weapon (3)` and `Head (0)`
         - `Ring2 (8)` is activated by `Cuff (5)` and `Pendant (9)`
         - `Pendant (9)` is activated by `Foot/Boot (4)` and `Ring1 (7)`
  3. **Ground Objects & `6x10` Spatial Inventory Grid**:
     - Ground objects (`GroundObjectSlot`) reside in `SpatialRegionSlot.GroundObjects`, assigned runtime `ObjId` (`1..MAX_OBJECT`), synchronized via `0x64` (`s2c_objadd`, `35 + NameLen` bytes) on spawn and `3x3` region entry, and removed via `0x67` (`s2c_objremove`, 6B) on pickup (`Reason = 1`) or expiration (`Reason = 0`, default `1080` frames = 60 seconds; killer ownership protection = `180` frames = 10 seconds).
     - Player inventory (`PlayerInventoryGrid` inside `PlayerZoneState`) tracks `pos_hand (1)`, `pos_equip (2)` (`15` slots), `pos_equiproom (3)` (`6 x 10` boolean/item occupancy matrix), and `pos_immediacy (7)` (`3 x 1` belt slots for medicines/scrolls).
     - All inventory mutations execute deterministically on the single-threaded 18-Hz `MapZoneWorker` loop and persist via `ICharacterAppService.SaveCharacterProgressionAndInventoryAsync` under lease-epoch fencing.
