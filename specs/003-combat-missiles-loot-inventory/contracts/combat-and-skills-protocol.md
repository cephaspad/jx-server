# Wire Protocol Contract: Combat, Skills, Missiles, Progression & Revival (Port 6666)

**Feature Branch**: `003-combat-missiles-loot-inventory`  
**Date**: 2026-11-09  
**Ground-Truth Sources**:
- `jx-swarm/docs/02-game-systems/movement-and-combat.md`
- `jx-win-src/Sources/Core/Src/KProtocol.h`, `KPlayer.cpp`, `KNpc.cpp`, `KSkills.cpp`

All packets are little-endian and encrypted on TCP Port `6666` via the session's `KsgStreamCipher` + `SecondLayerXorCipher`.

---

## 1. Skill Casting, Auto-Attack & Aura Selection (`C2S` & `S2C`)

### 1.1 `c2s_npcskill` (`0x4D`, Fixed `17` Bytes — `NPC_SKILL_COMMAND`)
Sent when the player casts a martial arts skill or basic attack.
- **`+0x00` (`byte`)**: `ProtocolType = 0x4D`
- **`+0x01` (`int`)**: `TargetMpsYOrNpcId` — When `TargetMpsX == -1`, holds the locked `TargetNpcId` (`1..9999` or player runtime ID); otherwise holds the ground target `MpsY`.
- **`+0x05` (`int`)**: `SkillId` — ID of the skill to cast.
- **`+0x09` (`int`)**: `TargetMpsX` — Ground target `MpsX`, or `-1` (`0xFFFFFFFF`) when locked onto an entity.
- **`+0x0D` (`uint`)**: `CheckValue` — Anti-cheat token verified against `MovementAntiCheatConsts.SkillCheckKey = 0x3AB9u` (`KPlayer::IsCheckValueValid`).

### 1.2 `c2s_autoattacknpc` (`0x87`, Fixed `26` Bytes — `PLAYER_AUTO_ATTACK_COMMAND`)
Client auto-play / emergency envelope packet.
- **`+0x00` (`byte`)**: `ProtocolType = 0x87`
- **`+0x01` (`byte`)**: `SubCommand`:
  - `0x02`: Emergency Town Portal (`KPlayer::UseTownPortal`) — consumes one Town Portal scroll from bag/belt and teleports to town revival point.
  - `0x03`: Wrapped inner movement/combat command starting at `+0x02`:
    - If `buffer[2] == 0x4B`: 13-byte `c2s_npcwalk` frame at `+0x02..+0x0E`.
    - If `buffer[2] == 0x4D`: 17-byte `c2s_npcskill` frame at `+0x02..+0x12`.
- **`+0x02..+0x19` (`24` bytes)**: Inner payload + zero padding.

### 1.3 `c2s_changeauraskill` (`0x6F`, Fixed `5` Bytes — `PLAYER_CHANGE_AURA_SKILL`)
- **`+0x00` (`byte`)**: `ProtocolType = 0x6F`
- **`+0x01` (`int`)**: `AuraSkillId` — Learned aura skill ID to activate on the right-hand slot.

### 1.4 `s2c_skillcast` (`0x5A`, Fixed `25` Bytes — `NPC_SKILL_SYNC`) & `s2c_castskilldirectly` (`0x85`, Fixed `25` Bytes)
Broadcast to all players in the `3x3` region AOI when a skill cast is accepted (`0x5A` for character cast animation, `0x85` for direct/instant skill casts).
- **`+0x00` (`byte`)**: `ProtocolType = 0x5A` (or `0x85`)
- **`+0x01` (`int`)**: `TargetMpsX` (`-1` when entity-locked)
- **`+0x05` (`uint`)**: `SkillId`
- **`+0x09` (`int`)**: `TargetMpsYOrNpcId`
- **`+0x0D` (`uint`)**: `CasterNpcId`
- **`+0x11` (`uint`)**: `SkillLevel`
- **`+0x15` (`uint`)**: `Reserved` (`0`)

---

## 2. Combat Hurt, PK Flag, Death & Revival (`C2S` & `S2C`)

### 2.1 `c2s_pkapplychangenormalflag` (`0x76`, Fixed `2` Bytes)
- **`+0x00` (`byte`)**: `ProtocolType = 0x76`
- **`+0x01` (`byte`)**: `PkFlag` (`0` = Peace/Luyện công, `1` = Fight/Chiến đấu, `2` = Slaughter/Đồ sát). Switching back to `0` enforces a `3240`-frame (3-minute) cooldown.

### 2.2 `s2c_broadcasenetcommand` (`0x56`, Fixed `22` Bytes — `NPC_NETCOMMAND_SYNC`)
Broadcast to the `3x3` region AOI when an entity takes a hit that triggers hurt recoil (`CmdKind = 9`).
- **`+0x00` (`byte`)**: `ProtocolType = 0x56`
- **`+0x01` (`uint`)**: `NpcId`
- **`+0x05` (`byte`)**: `CmdKind` (`9` = `do_hurt`)
- **`+0x06` (`int`)**: `Param1` (`CurrentMpsX`)
- **`+0x0A` (`int`)**: `Param2` (`CurrentMpsY`)
- **`+0x0E` (`int`)**: `Param3` (`HurtRecoverFrames`)
- **`+0x12` (`int`)**: `Param4` (`0`)

### 2.3 `s2c_npcdeath` (`0x57`, Fixed `5` Bytes — `NPC_DEATH_SYNC`)
Broadcast to the `3x3` region AOI when a monster or player's `CurrentLife` reaches `0`.
- **`+0x00` (`byte`)**: `ProtocolType = 0x57`
- **`+0x01` (`uint`)**: `NpcId`

### 2.4 `c2s_playerrevive` (`0x74`, Fixed `13` Bytes — `PLAYER_REVIVE_COMMAND`)
Sent by a dead player to revive at their saved town revival point.
- **`+0x00` (`byte`)**: `ProtocolType = 0x74`
- **`+0x01` (`int`)**: `ReviveType` (`0` = Town revival)
- **`+0x05` (`uint`)**: `Reserved1` (`0`)
- **`+0x09` (`uint`)**: `Reserved2` (`0`)

### 2.5 `s2c_npcrevive` (`0x89`, Fixed `6` Bytes — `NPC_REVIVE_SYNC`)
- **`+0x00` (`byte`)**: `ProtocolType = 0x89`
- **`+0x01` (`uint`)**: `NpcId`
- **`+0x05` (`byte`)**: `ReviveType` (`0` = Town revival, `1` = In-place revival)

---

## 3. Experience, Level-Up & Attribute/Skill Allocation (`C2S` & `S2C`)

### 3.1 `s2c_playerexp` (`0x5C`, Fixed `5` Bytes — `PLAYER_EXP_SYNC`)
- **`+0x00` (`byte`)**: `ProtocolType = 0x5C`
- **`+0x01` (`int`)**: `CurrentExp` (updated cumulative experience within current level)

### 3.2 `s2c_playerlevelup` (`0x5B`, Fixed `17` Bytes — `PLAYER_LEVEL_UP_SYNC`)
- **`+0x00` (`byte`)**: `ProtocolType = 0x5B`
- **`+0x01` (`int`)**: `BaseAttributePoints` (`UnallocatedAttributePoints`)
- **`+0x05` (`int`)**: `SkillPoints` (`UnallocatedSkillPoints`)
- **`+0x09` (`int`)**: `NewLevel`
- **`+0x0D` (`int`)**: `MaxExpForNextLevel`

### 3.3 `c2s_playeraddbaseattribute` (`0x57`, Fixed `14` Bytes) $\rightarrow$ `s2c_playerattribute` (`0x5D`, Fixed `14` Bytes)
- **Request (`0x57`, `14B`)**:
  - `+0x00` (`byte`): `0x57`
  - `+0x01` (`byte`): `Attribute` (`0` = Strength, `1` = Dexterity, `2` = Vitality, `3` = Energy)
  - `+0x02` (`int`): `AddNo` (points to allocate, $> 0$)
  - `+0x06` (`uint[2]`): `Reserved`
- **Response (`0x5D`, `14B`)**:
  - `+0x00` (`byte`): `0x5D`
  - `+0x01` (`byte`): `Attribute` (`0..3`)
  - `+0x02` (`int`): `BasePoint` (new base attribute value)
  - `+0x06` (`int`): `CurPoint` (new effective attribute value including equipment bonuses)
  - `+0x0A` (`int`): `LeavePoint` (remaining `UnallocatedAttributePoints`)

### 3.4 `c2s_playerapplyaddskillpoint` (`0x58`, Fixed `17` Bytes) $\rightarrow$ `s2c_playerskilllevel` (`0x5E`, Fixed `15` Bytes)
- **Request (`0x58`, `17B`)**:
  - `+0x00` (`byte`): `0x58`
  - `+0x01` (`int`): `SkillId`
  - `+0x05` (`int`): `AddPoint` (points to allocate, $> 0$)
  - `+0x09` (`uint[2]`): `Reserved`
- **Response (`0x5E`, `15B`)**:
  - `+0x00` (`byte`): `0x5E`
  - `+0x01` (`int`): `SkillId`
  - `+0x05` (`int`): `SkillLevel` (effective skill level)
  - `+0x09` (`int`): `LeavePoint` (remaining `UnallocatedSkillPoints`)
  - `+0x0D` (`short`): `SkillExpPercent` (`0..100`)
