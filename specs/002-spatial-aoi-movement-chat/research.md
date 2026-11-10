# Phase 0 Ground-Truth Research: Spatial 3x3 Region AOI, Movement, Map Transitions & Multi-Channel Chat

**Feature Branch**: `002-spatial-aoi-movement-chat`  
**Date**: 2026-11-09  
**Spec**: [spec.md](spec.md)

This document resolves all technical unknowns for **Milestone 2 (`002-spatial-aoi-movement-chat`)** against the original binary (`jx_linux_y`, `game.exe`, `pak/maps.pak`), C++ reference source (`d:\Projects\jx-win-src\Sources\Core\Src`), Lua map trap scripts (`d:\Projects\jx-linux-bin\server1\script`), and `jx-swarm/docs` ground-truth specifications.

---

## 1. Binary Layout of `Region_S.dat` Sections 1 (`TRAP_SECTION`) and 2 (`NPC_SECTION`)

### Decision
Extend `JXServer.GameAssets` (`MapCatalog` / `RegionTrapGrid` / `RegionNpcPlacement`) to parse **Section `1` (`TRAP_SECTION`, index `1`)** and **Section `2` (`NPC_SECTION`, index `2`)** from each `v_{RY:D3}\{RX:D3}_Region_S.dat` file (both unpacked filesystem and `pak/maps.pak`) using the exact `SceneDataDef.h` binary layouts verified against `server1/pak/maps.pak`.

### Ground-Truth Evidence (`SceneDataDef.h:265-307`, `KRegion.cpp:380-445`, `maps.pak` verified)
Each `Region_S.dat` begins with `uint uNumSection` (`4 bytes`) followed by `KCombinFileSection[uNumSection]` (`8 bytes` each: `uint uOffset, uint uLength`):
- **Section `0` (`OBSTACLE_SECTION`, index `0`)**: Already parsed in Milestone 1 (`RegionObstacleGrid`, `2048 bytes` = `int[16, 32]`).
- **Section `1` (`TRAP_SECTION`, index `1`)**:
  - Present when `uNumSection >= 2` and `sections[1].uLength >= 12`.
  - Starts with `KTrapFileHead` (`12 bytes`):
    - `+0x00` (`4B`): `uint uNumTrap`
    - `+0x04` (`8B`): `uint[2] uReserved`
  - Followed by `uNumTrap` records of `KSPTrap` (`8 bytes` each; total length `12 + uNumTrap * 8`):
    - `+0x00` (`1B`): `byte cX` (Starting cell X inside region, `0..15`)
    - `+0x01` (`1B`): `byte cY` (Cell Y inside region, `0..31`)
    - `+0x02` (`1B`): `byte cNumCell` (Count of consecutive **horizontal** cells: `KRegion::LoadServerTrap` sets `m_dwTrap[cX + j][cY] = uTrapId` for `j = 0 .. cNumCell - 1`)
    - `+0x03` (`1B`): `byte cReserved`
    - `+0x04` (`4B`): `uint uTrapId` (`g_FileName2Id` 32-bit hash of the trap script path, e.g., `0x9399B5E0` in Map `53` region `v_099\099_Region_S.dat`).
- **Section `2` (`NPC_SECTION`, index `2`)**:
  - Present when `uNumSection >= 3` and `sections[2].uLength >= 12`.
  - Starts with `KNpcFileHead` (`12 bytes`):
    - `+0x00` (`4B`): `uint uNumNpc`
    - `+0x04` (`8B`): `uint[2] uReserved`
  - Followed by `uNumNpc` variable-length `KSPNpc` records, each consisting of a **60-byte fixed header** (`sizeof(KSPNpc) - MAX_RESOURCE_FILE_NAME_LEN`) followed by **`nScriptNameLen` bytes** of raw GBK script path:
    - `+0x00` (`4B`): `int nTemplateID` (0-based template index into `settings/npcs.txt` data rows)
    - `+0x04` (`4B`): `int nPositionX` (`MpsX` world coordinate)
    - `+0x08` (`4B`): `int nPositionY` (`MpsY` world coordinate)
    - `+0x0C` (`1B`): `byte bSpecialNpc` (`0` = normal, `1` = special/golden)
    - `+0x0D` (`3B`): `byte[3] cReserved`
    - `+0x10` (`32B`): `byte[32] szName` (Null-terminated raw name; in `maps.pak` this often retains Chinese GBK like `杂货铺老板` or `金猫`)
    - `+0x30` (`2B`): `short nLevel` (NPC level; `<= 0` defaults to `1`)
    - `+0x32` (`2B`): `short nCurFrame`
    - `+0x34` (`2B`): `short nHeadImageNo`
    - `+0x36` (`2B`): `short shKind` (`0` = `kind_normal` Monster, `3` = `kind_dialoger` Town NPC)
    - `+0x38` (`1B`): `byte cCamp` (Initial camp `0..5`)
    - `+0x39` (`1B`): `byte cSeries` (Five-elements series `0..4`)
    - `+0x3A` (`2B`): `ushort nScriptNameLen` (Byte length of trailing script path)
    - `+0x3C` (`nScriptNameLen` bytes): `byte[] szScript` (GBK path to NPC Lua script, e.g., `\script\两湖区\巴陵县\npc\巴陵县_杂货店老板对话.lua`).

### Rationale & Alternatives Rejected
- **Why combine `settings/npcs.txt` + `Region_S.dat` + `script/global/autoexec.lua`**: Verified against `server1/pak/maps.pak` for Map `53` (Ba Lăng Huyện), which contains `347` regions with static NPCs/monsters (`KSPNpc`), while `server1/script/global/autoexec.lua` (`adddialognpc` table) spawns additional localized town NPCs. Furthermore, because `KSPNpc.szName` in `maps.pak` retains Chinese GBK strings (`杂货铺老板`), resolving the display name from `settings/npcs.txt` (`TCVN3` $\rightarrow$ `UTF-8`) by `nTemplateID` (unless overridden by `autoexec.lua`) guarantees 100% authentic Vietnamese NPC names (`Chưởng quầy tạp hóa`, `Mèo rừng`, `Nhím`, etc.).

---

## 2. Trap Script Hashing & Portal Action Resolution (`script/**/trap/*.lua`)

### Decision
Implement `TrapScriptCatalog` (`ITrapScriptCatalog`) in `JXServer.GameAssets` to scan `script/**/trap/*.lua` on startup, compute each file's canonical `g_FileName2Id` hash (`\script\...` in GBK bytes via `PakFilePathHasher.ComputeHash`), and parse the Lua trap actions into structured `PortalTrapBinding` records (plus support JSON/INI override bindings).

### Ground-Truth Evidence (`KPlayer.cpp`, `KRegion.cpp`, `server1/script/两湖区/巴陵县/trap/*.lua`)
- When `KRegion::LoadServerTrap` loads `KSPTrap`, `uTrapId` is the 32-bit `g_FileName2Id` hash of the relative script path starting with `\script\...` (encoded in GBK).
- Direct inspection of `server1/script/Á½ºþÇø/°ÍÁêÏØ/trap/*.lua` (`\script\两湖区\巴陵县\trap\*.lua`) reveals two canonical trap patterns across all VLTK 1 maps:
  1. **Town Gate Fight-State Trap** (e.g., `巴陵县东门.lua`, `西门.lua`, `南门.lua`, `北门.lua`):
     ```lua
     if ( GetFightState() == 0 ) then
         SetPos(cellXOut, cellYOut)
         SetFightState(1)
     else
         SetPos(cellXIn, cellYIn)
         SetFightState(0)
     end
     ```
     Converts `(cellX, cellY)` to `MpsX = cellX * 32`, `MpsY = cellY * 32`, toggles `PlayerZoneState.FightMode` (`0 <-> 1`), moves the player on the same map, and syncs `s2c_syncnpcminplayer` (`0x4E`).
  2. **Cross-Map Portal Trap** (e.g., `巴陵县to南岳镇.lua`, `巴陵县to武陵山.lua`, `巴陵县to神农架.lua`, `巴陵县to武陵洞.lua`):
     ```lua
     if (GetLevel() >= minLevel) then
         SetFightState(targetFightState)
         NewWorld(targetMapId, targetCellX, targetCellY)
     else
         Talk(1, "", "<msg>")
         SetPos(bounceCellX, bounceCellY)
     end
     ```
     Checks minimum character level (`MinRequiredLevel`, e.g., `5` for leaving starter villages); if met, transitions to `(targetMapId, targetCellX * 32, targetCellY * 32)`. If not met, bounces the player back to `(bounceCellX * 32, bounceCellY * 32)` via `0x4E` and sends a `0xFB` system/talk notification.
- **Anti-Loop Edge Trigger**: `PlayerZoneState.LastTriggeredTrapId` stores the `uTrapId` of the cell that triggered the trap. While the player remains on cells with the same `uTrapId` (or arrives on a destination trap cell), the trap does not re-fire. Stepping onto a cell with `uTrapId == 0` resets `LastTriggeredTrapId = 0`.

---

## 3. Spatial 3x3 Region AOI Partitioning & Diff Synchronization

### Decision
Each `MapZoneWorker` owns a `SpatialRegionGrid` containing `RegionSlot` instances indexed by `(RegionX, RegionY)`. Every region change or world entry computes the mathematical set difference between the old `3x3` neighborhood and the new `3x3` neighborhood.

### Ground-Truth Evidence (`KSubWorld.cpp`, `KRegion.cpp`, `KNpc.cpp`)
- A map's region grid spans `RegionLeft..RegionRight` and `RegionTop..RegionBottom` from `.wor`.
- `WorldPosition` already computes `RegionX = MpsX / 512` and `RegionY = MpsY / 1024`.
- The `3x3` neighborhood of `(rx, ry)` is the set of valid regions `(x, y)` where `|x - rx| <= 1` and `|y - ry| <= 1` (at most 9 regions).
- **World Entry / Map Arrival (`oldRegion == none`)**:
  - All valid regions in the `3x3` neighborhood around `(newRx, newRy)` are treated as **entering regions**.
  - The arriving player receives `s2c_syncnpc` (`0x4C`, `61B`) for every static NPC and every other online player in those 9 regions.
  - Every other online player in those 9 regions receives `s2c_syncnpc` (`0x4C`, `61B`) for the arriving player.
- **Region Boundary Crossing (`(oldRx, oldRy) -> (newRx, newRy)`)**:
  - Remove player from `RegionSlot(oldRx, oldRy)` and add to `RegionSlot(newRx, newRy)`.
  - For each region `(x, y)` in `New3x3 \ Old3x3` (at most 3 newly entered regions on cardinal steps or 5 on diagonal steps):
    - Send `s2c_syncnpc` (`0x4C`) of all NPCs and players in `(x, y)` to the moving player.
    - Send `s2c_syncnpc` (`0x4C`) of the moving player to all players in `(x, y)`.
- **Periodic & On-Demand Sync**:
  - During `18-Hz` ticks, active players broadcast `s2c_syncnpcminplayer` (`0x4E`, `13B`) to themselves and `s2c_syncnpcmin` (`0x4D`, `18B`) to other players in their `3x3` neighborhood.
  - When a client sends `c2s_requestnpc` (`0x49`, `5B`) or `c2s_requestplayer` (`0x48`, `5B`) with a target `NpcId`, the `MapZoneWorker` looks up the NPC or player by `NpcId` and replies with `s2c_syncnpc` (`0x4C`, `61B`).
  - Note on `s2c_syncnpc` (`0x4C`) `NpcSettingIdx` (`+0x1C`): For static NPCs (`NpcId < 10000`), `NpcSettingIdx = ((uint)Level << 16) | (uint)(ushort)TemplateId`; for player entities (`NpcId >= 10000`), `NpcSettingIdx = 0`.

---

## 4. 18-Hz Authoritative Movement, Obstacle Validation & Posture Regeneration

### Decision
Implement deterministic step-by-step vector progression inside `MapZoneWorker.ExecuteTick()` at `18 Hz`, coupled with `IsCheckValueValid` verification in `GameServerClientSession` / packet handlers.

### Ground-Truth Evidence (`KNpc.cpp:DoWalk`, `DoRun`, `ServeMove`, `KPlayer::IsCheckValueValid`)
- **Anti-Cheat Checksum (`0x080bf0a0`)**:
  - `c2s_syncend` (`0x43`, `9B`) supplies `ClientToken1` (`+0x01`) and `ClientToken2` (`+0x05`), already stored on `PlayerZoneState`.
  - For `c2s_npcwalk` (`0x4B`, `uKey = 0x24A1`) and `c2s_npcrun` (`0x4C`, `uKey = 0x1D78`):
    `dwCheckValue != 0 && ((dwCheckValue ^ ClientToken1 ^ ClientToken2) % uKey) == 0`.
- **Walk / Run Initiation & Broadcast**:
  - Check that destination `(destMpsX, destMpsY)` is within map bounds.
  - Interrupt `do_sit` (`Doing = 4`) if currently sitting.
  - Set `TargetMpsX = destMpsX`, `TargetMpsY = destMpsY`, `Doing = do_walk (2)` or `do_run (3)`.
  - Broadcast `s2c_npcwalk` (`0x50`: `NpcID` at `+1`, `nMpsY` at `+5`, `nMpsX` at `+9`) or `s2c_npcrun` (`0x51`: `nMpsY` at `+1`, `NpcID` at `+5`, `nMpsX` at `+9`) to all players in the `3x3` region AOI (including the moving player).
- **18-Hz Tick Progression (`ServeMove`)**:
  - Each tick while `Doing` is `do_walk (2)` or `do_run (3)`:
    - Compute remaining vector `(dx = TargetMpsX - MpsX, dy = TargetMpsY - MpsY)` and Euclidean distance `dist`.
    - Step distance `step = Doing == do_run ? RunSpeed : WalkSpeed` (default `WalkSpeed = 10`, `RunSpeed = 20` Mps/tick).
    - Compute candidate coordinate `(nextMpsX, nextMpsY)` (clamped to `(TargetMpsX, TargetMpsY)` when `dist <= step`).
    - Query `MapDescriptor.GetObstacleType(nextMpsX, nextMpsY)`:
      - If `Obstacle_Full (1)` (or outside loaded regions), halt at current `(MpsX, MpsY)`, set `Doing = do_stand (1)`, and send `s2c_syncnpcminplayer` (`0x4E`) + `s2c_syncnpcmin` (`0x4D`) to the `3x3` AOI.
      - Otherwise advance `(MpsX, MpsY) = (nextMpsX, nextMpsY)`, update `Dir` (`0..63`), check for region crossing (`(oldRx, oldRy) != (newRx, newRy)`), check for cell trap (`GetTrapId(MpsX, MpsY)`), and if `(MpsX, MpsY) == (TargetMpsX, TargetMpsY)`, transition to `Doing = do_stand (1)`.
- **Jump (`0x4E` $\rightarrow$ `0x54`) & Sit (`0x71` $\rightarrow$ `0x53`)**:
  - `c2s_npcjump` (`0x4E`, `13B`): Validates that destination cell is walkable (`Obstacle != Obstacle_Full`), updates coordinates/target, sets `Doing = do_jump (5)` $\rightarrow$ `do_stand (1)`, and broadcasts `s2c_npcjump` (`0x54`, `13B`: `nMpsY` at `+1`, `nMpsX` at `+5`, `NpcID` at `+9`) + `0x4E` self-sync.
  - `c2s_npcsit` (`0x71`, `2B`): If `SitFlag != 0`, sets `Doing = do_sit (4)` and broadcasts `s2c_npcsit` (`0x53`, `5B`: `NpcID` at `+1`). If `SitFlag == 0`, sets `Doing = do_stand (1)` and broadcasts `s2c_syncnpcmin` (`0x4D`) / `0x4E`. While `Doing == do_sit (4)`, every `18` ticks (`1 second`) regenerates `CurLife` and `CurMana` up to `MaxLife`/`MaxMana` and sends `s2c_synccurplayernormal` (`0x46`, `14B`) when changed.

---

## 5. Same-Server vs. Cross-Server Map Transitions (`0x49`/`0x4E` vs. `0x35`)

### Decision
Coordinate cross-map transfers through `IZoneClusterRouter` (`ZoneClusterRouter`):
- When `TargetMapId` is hosted on the local `ZoneClusterRouter`, perform an in-memory worker transfer (`DetachForMapTransfer` on source `MapZoneWorker` $\rightarrow$ `AttachTransferredPlayerZoneCommand` on destination `MapZoneWorker`) and emit `s2c_syncworld` (`0x49`, `34B`) + `s2c_syncnpcminplayer` (`0x4E`, `13B`) + destination `3x3` AOI `0x4C` packets over the existing TCP connection **without** sending `s2c_syncend` (`0x43`) or resetting the stream cipher.
- When `TargetMapId` is mapped to an external GameServer endpoint, issue `s2c_notifyplayerexchange` (`0x35`, `25B`, plain little-endian port at `+0x15`).

### Ground-Truth Evidence (`KNpc.cpp:ChangeWorld` @ `0x0809e410`, `gameserver-session-and-sync.md` Section 5)
- `KNpc::ChangeWorld` checks whether the destination `SubWorld` is loaded locally.
- For same-server transitions, the client does **not** reconnect and does **not** expect `0x43` (`s2c_syncend`); receiving `0x49` (`s2c_syncworld`) followed by `0x4E` (`s2c_syncnpcminplayer`) immediately loads the new map `.wor` on the client and places the camera at `(TargetMpsX, TargetMpsY)`.
- Persisting `(MapId, MpsX, MpsY)` asynchronously via `ICharacterAppService.UpdateCharacterPositionAsync` on map transition and disconnect ensures durable position recovery (`FR-012`).

---

## 6. Variable-Length Chat Framing & Multi-Channel Routing (`0x52`, `0x5B`, `0x56`, `0x7E`, `0x60`, `0xFB`)

### Decision
1. Update `ProtocolSizeTable.TryValidateC2SPacketSize` to validate variable-length C2S opcodes using their 16-bit little-endian `wLengthMinusOne` field at `+0x01`:
   - `0x52` (`c2s_playertalk`, min `8 bytes`, max `320 bytes`): `BinaryPrimitives.ReadUInt16LittleEndian(payload[1..3]) + 1 == payload.Length`.
   - `0x56` (`c2s_playersendchat`, min `48 bytes`, max `360 bytes`): `BinaryPrimitives.ReadUInt16LittleEndian(payload[1..3]) + 1 == payload.Length`.
2. Route spatial talk (`0x52` $\rightarrow$ `0x5B`) and Nearby chat (`0x56` Channel `2`/`7` $\rightarrow$ `0x7E`) inside the speaker's `MapZoneWorker` (`3x3` region AOI).
3. Route cross-map channels (`World` = `1`, `Whisper` = `3`, `Team` = `4`, `Faction` = `5`) across all active `MapZoneWorker` shards via `IZoneClusterRouter.BroadcastClusterChatAsync`.

### Ground-Truth Evidence (`chat-and-social.md`, `KProtocolProcess.cpp`, `KPlayerChat.cpp`)
- **`c2s_chatsetchannel` (`0x60`, `5B`, `<BI`)**: Updates `PlayerZoneState.SubscribedChatChannelMask` (`uint32` bitmask, default `0xFFFFFFFFu` on world entry so players receive all standard channels unless explicitly filtered).
- **`c2s_playertalk` (`0x52`, `7 + MsgLen` bytes) $\rightarrow$ `s2c_playertalk` (`0x5B`, `11 + MsgLen` bytes)**:
  - `0x52` carries `wLengthMinusOne` (`+0x01`), `dwTalkerID` (`+0x03`), and raw `TCVN3` message bytes (`+0x07`).
  - `0x5B` carries `wLengthMinusOne` (`+0x01`), `dwTalkerID` (`+0x03`), `dwReserved` (`+0x07`), and raw `TCVN3` message bytes (`+0x0B`).
- **`c2s_playersendchat` (`0x56`, `47 + MsgLen` bytes) $\rightarrow$ `s2c_chatgetsomeone` (`0x7E`, `37 + MsgLen` bytes)**:
  - `0x56` carries `wLengthMinusOne` (`+0x01`), `btCurrChannel` (`+0x03`), `btType` (`+0x04`), `btChatPrefixLen` (`+0x05`), `wSentenceLen` (`+0x06`), `dwTargetID` (`+0x08`), `nTargetIdx` (`+0x0C`), `szSentencePrefix[32]` (`+0x10`, target player name in `TCVN3` for whisper or channel prefix), and `szSentence[wSentenceLen]` (`+0x30`, `TCVN3` text).
  - `0x7E` carries `wLengthMinusOne` (`+0x01`), `dwSendID` (`+0x03`, sender `NpcId`), `szSender[32]` (`+0x07`, sender character name in `TCVN3`), and `szSentence[MsgLen]` (`+0x27`, null-terminated `TCVN3` message).
- **`s2c_extend` (`0xFB`, `16 + MsgLen` bytes)**:
  - Used for server system announcements and level-gate trap feedback (`SendSystemInfo`), carrying `wLengthMinusOne` (`+0x01`), `ExtendHeader = 0x00010032` (`+0x03`), `szPrefix = "HÖ thèng"` (`+0x07`, `8B` `TCVN3`), `wMsgLen` (`+0x0F`), and `szMessage` (`+0x11`).
