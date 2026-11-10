# Quickstart & Verification Guide: Spatial 3x3 Region AOI, Movement, Map Transitions & Multi-Channel Chat

**Feature Branch**: `002-spatial-aoi-movement-chat`  
**Date**: 2026-11-09  
**Spec**: [spec.md](spec.md)

---

## 1. Build & Run Automated Verification Suite

From `d:\Projects\jx-server`:

```powershell
# 1. Compile entire solution with strict zero-warnings gate
dotnet build JXServer.slnx -warnaserror

# 2. Run protocol framing, anti-cheat checksum, and packet symmetry tests
dotnet test tests/JXServer.Protocol.Tests/JXServer.Protocol.Tests.csproj

# 3. Run 18-Hz spatial 3x3 AOI, NPC/Trap loading, movement obstacle, and map transition tests
dotnet test tests/JXServer.ZoneEngine.Tests/JXServer.ZoneEngine.Tests.csproj

# 4. Run end-to-end multi-client TCP integration tests (AOI visibility, walk/run, traps, chat)
dotnet test tests/JXServer.Integration.Tests/JXServer.Integration.Tests.csproj
```

---

## 2. Start `JXServer.Host` Against Original `jx-linux-bin` Assets

From `d:\Projects\jx-server`:

```powershell
dotnet run --project src/JXServer.Host/JXServer.Host.csproj
```

On startup, verify the structured console logs report:
- `NpcTemplateCatalog` loaded templates from `d:\Projects\jx-linux-bin\server1\settings\npcs.txt` and `script\global\autoexec.lua`.
- `TrapScriptCatalog` indexed trap scripts (`script\**\trap\*.lua`) by `g_FileName2Id` hash.
- `MapCatalog` loaded configured maps (including Map `53` Ba Lăng Huyện and adjacent destination maps `54` Nam Nhạc Trấn, `70` Vũ Lăng Sơn) with `OBSTACLE_SECTION`, `TRAP_SECTION`, and `NPC_SECTION` static spawns.
- `ZoneEngineHostedService` spawned static NPCs into their respective `SpatialRegionSlot` instances across all active `MapZoneWorker` shards.

---

## 3. Live Validation with `JX-Swarm` & Original `game.exe` Client

### 3.1 Multi-Bot AOI, Walk/Run & Chat Smoke Test (`jx-swarm`)
In a second terminal (`d:\Projects\jx-swarm`):

```powershell
dotnet run --project src/JXSwarm.Drone.Host/JXSwarm.Drone.Host.csproj -- --bots 50
```

Expected behavior:
1. All 50 bots authenticate via Bishop (`5622`), enter Map `53` on GameServer (`6666`), and immediately receive `s2c_syncnpc` (`0x4C`) for static town NPCs (`Chưởng quầy tạp hóa`, `Xa Phu`, etc.) and neighboring bots in their `3x3` region AOI.
2. Bots issue `c2s_npcwalk` (`0x4B`) and `c2s_npcrun` (`0x4C`) commands with valid `dwCheckValue` checksums; surrounding bots receive `s2c_npcwalk` (`0x50`) / `s2c_npcrun` (`0x51`) and `s2c_syncnpcmin` (`0x4D`) updates as bots cross region boundaries.
3. Bots exchange nearby speech (`0x52` $\rightarrow$ `0x5B`) and multi-channel chat (`0x56` $\rightarrow$ `0x7E`) with full Vietnamese text preservation.

### 3.2 In-Game `game.exe` Client Checklist
1. Log into Ba Lăng Huyện (Map `53`) using `game.exe`.
2. Confirm town NPCs (`Chưởng quầy tạp hóa`, `Chủ dược điển`, `Xa Phu`, `Thần Tiên Tỷ Tỷ`) and nearby players appear with their accurate Vietnamese names and positions.
3. Walk/run through town gates (`Đông Môn`, `Tây Môn`, `Nam Môn`, `Bắc Môn`) to verify gate traps toggle fight state (`FightMode 0 <-> 1`) and nudge position via `0x4E`.
4. Walk onto a map boundary portal trap (e.g., road to Nam Nhạc Trấn or Vũ Lăng Sơn) to verify level check / seamless map transition (`0x49` + `0x4E` + destination `3x3` AOI sync).
5. Press `V` to sit (`0x71` $\rightarrow$ `0x53`) and confirm periodic HP/MP regeneration (`0x46`), then send Nearby, World, and Private Whisper chat messages.
