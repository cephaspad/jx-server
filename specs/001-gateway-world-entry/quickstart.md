# Quickstart & Verification Guide: Milestone 1 (`001-gateway-world-entry`)

**Date**: 2026-11-08  
**Feature**: [spec.md](spec.md) | **Contracts**: [contracts/](contracts/)

## 1. Prerequisites

- **.NET 10 SDK (`net10.0`, C# 14)** installed on Windows or Linux.
- **Static Game Assets**: Available at `d:\Projects\jx-linux-bin\server1` (containing `settings/` and `maps/` or `pak/maps.pak`) and `d:\Projects\jx-linux-bin\gateway\settings\goddess_rolename.flt`.
- **Database**:
  - **Default Local/Dev Mode**: Uses **SQLite WAL** (`Data Source=jxserver.db`) or **SQL Server LocalDB / SQL Server** via `ConnectionStrings:Default` in `appsettings.json`.
  - **Production Mode**: Uses **Microsoft SQL Server** (`DatabaseProvider = "SqlServer"`).

---

## 2. Build & Automated Verification Gates (Zero Warnings)

Run from `d:\Projects\jx-server`:

```powershell
dotnet format JXServer.slnx --verify-no-changes
dotnet build JXServer.slnx -warnaserror
dotnet test JXServer.slnx --no-build
```

### Expected Test Suites in Milestone 1
1. **`JXServer.Protocol.Tests`**:
   - Verifies 44-byte `0x20` handshake key obfuscation/deobfuscation round-trip, 5,679-entry `g_uPublicKeys` SHA-256 integrity, `KSG_DecodeEncode` stream cipher vectors, `0x3042` role-list cipher, `0x38BC` role-select cipher, and exact byte sizes of all Bishop (`0x41`, `0x37`, `0x45`, `0x3A`, `0x8C`, `0x64`, `0x34`) and GameServer (`0x42`, `0x49`, `0x44`, `0x45`, `0x46`, `0x4C`, `0x4E`, `0xA4`, `0x43`, `0x82`, `0x70`, `0x99`, `0x9B`) packets.
2. **`JXServer.ZoneEngine.Tests`**:
   - Verifies loading `Ba Lăng Huyện` (Map `53`) `.wor` and `Region_S.dat` obstacle matrices, starter character template resolution across all 10 `(ElementSeries, CharacterGender)` combinations (`newplayerini00..09.ini`), and deterministic 18-Hz tick loop execution using `FakeTimeProvider`.
3. **`JXServer.Integration.Tests`**:
   - Spins up `JXServer.Host` in-process and runs full end-to-end TCP flows:
     - Scenario 1: Fresh account $\rightarrow$ `0x41` Login $\rightarrow$ `0x37` Empty Role List $\rightarrow$ `0x45` Create Character in Ba Lăng Huyện (`Map 53`) $\rightarrow$ `0x8C` + `0x34` Handoff $\rightarrow$ `0x42` GameServer Attach $\rightarrow$ `0x49`/`0x44`/`0x45`/`0x46`/`0x43` Sync $\rightarrow$ `0x82`/`0x70`/`0x99` Heartbeat Ping $\rightarrow$ `0x9B` Clean Logout.
     - Scenario 2: Lease-Fenced Takeover (`bKickOnline = 1`) while a prior session is still in-world $\rightarrow$ immediate eviction of old session and zero stuck-account errors.

---

## 3. Live End-to-End Smoke Test with `JX-Swarm` Autonomous Drone

### Terminal 1 — Start the New `JXServer.Host`
```powershell
cd d:\Projects\jx-server
dotnet run --project src/JXServer.Host -- `
  --GameAssets:ServerRootPath="d:\Projects\jx-linux-bin\server1" `
  --GameAssets:GatewayRootPath="d:\Projects\jx-linux-bin\gateway" `
  --LegacyGateway:BishopPort=5622 `
  --LegacyGateway:GameServerPort=6666
```

### Terminal 2 — Connect `JXSwarm.Drone.Host` Against the New Server
```powershell
cd d:\Projects\jx-swarm
dotnet run --project src/JXSwarm.Drone.Host -- `
  --smoke-account "choivolam001" `
  --smoke-password "123123" `
  --JxGateway:BishopHost="127.0.0.1" `
  --JxGateway:BishopPort=5622 `
  --JxGateway:GameServerHostOverride="127.0.0.1"
```

### Expected Outcome
- `JXServer.Host` logs `Bishop` handshake (`5622`), authenticates `choivolam001`, creates a new character (or selects the existing character), issues a `LeaseEpoch` and 16-byte `GUID` handoff ticket, attaches the character on `GameServer` (`6666`) into **Ba Lăng Huyện (`Map 53`, `MpsX = 52032`, `MpsY = 101696`)**, completes `0x43` sync, and exchanges 18-Hz heartbeats continuously.
