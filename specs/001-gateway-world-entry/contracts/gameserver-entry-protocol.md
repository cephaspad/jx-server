# Contract 02: GameServer World Entry & Heartbeat Wire Protocol (`TCP Port 6666`)

**Feature**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)  
**Ground-Truth Evidence**: `jx-swarm/docs/01-session-lifecycle/gameserver-session-and-sync.md`, `jx_linux_y` (`0x0804daa0..0x081f5c60`)

---

## 1. World Attach & Initial Synchronization Sequence

Upon TCP connection to port `6666`, the server sends the 44-byte `AccountBeginS2CPacket` (`0x20`) handshake frame. The client and server then execute the following exact packet sequence:

1. **Client $\rightarrow$ Server**: **`LogicLoginC2SPacket` (`0x42`, Fixed `17B`)**
   - `+0x00`: `0x42`
   - `+0x01` (`byte[16]`): `Guid` (the exact 16-byte ticket issued by Bishop in `0x34`).
   - Validation: Server verifies `Guid` against `ISessionLeaseAppService`, checks ticket expiration and `LeaseEpoch`, attaches the character to the target `MapZoneWorker`, and streams the initial state packets (`2a..2g`).
2. **Server $\rightarrow$ Client (Initial World & Character Burst)**:
   - **2a. `SyncWorldS2CPacket` (`0x49`, Fixed `34B`)**:
     - `+0x00`: `0x49`, `+0x01` (`int32`): `SubWorldId` (e.g., `53`), `+0x05` (`int32`): `MapCopyIndex = 0`, `+0x09` (`int32`): `PackedRegionId` (`(RegionY << 16) | RegionX`), `+0x0D` (`byte`): `Weather = 0`, `+0x0E` (`uint32`): `Frame` (current 18-Hz tick counter), `+0x12` (`byte[16]`): `MapName` (TCVN3).
   - **2b. `SyncCurPlayerS2CPacket` (`0x44`, Fixed `68B`)**:
     - Full character base stats (`RuntimeNpcId`, `Level`, `MaxLife`, `MaxStamina`, `Gender`, `Kind = 1`, `Series`, `MaxMana`, `HeadImage`, `AttributePoint`, `Energy`, `Lucky`, `SkillPoint`, `Strength`, `Dexterity`, `Vitality`, `ExpRemainder`, `LeadExp`, `TeamState`, `CurFaction`, `FactionAddTimes`, `ExpHigh`, `FirstFaction`, `WorldStat`, `SectStat`, `RepositoryMoney`, `InventoryMoney`, `RankFFId`).
   - **2c. `SyncCurPlayerSkillS2CPacket` (`0x45`, Variable `483B`)**:
     - `+0x00`: `0x45`, `+0x01` (`uint16`): `Length = 482`, `+0x03` (`80 * 6B`): 80 skill slots (`uint16 SkillId`, `byte Level`, `byte AddLevel`, `uint16 ExpPercent`).
   - **2d. `SyncCurPlayerNormalS2CPacket` (`0x46`, Fixed `14B`)**:
     - `+0x00`: `0x46`, `+0x01` (`uint16`): `CurrentStamina`, `+0x03` (`int32`): `CurrentMana`, `+0x07` (`int32`): `CurrentLife`, `+0x0B` (`uint16`): `Angry = 0`, `+0x0D` (`byte`): `TeamData = 0`.
   - **2e. `SyncNpcFullS2CPacket` (`0x4C`, Variable `34 + NameLen`)**:
     - Self NPC entity appearance and world coordinates (`MapMpsY`, `MapMpsX`, `Doing = 1` stand, `Kind = 1` player, `ID = RuntimeNpcId`, `m_szName` TCVN3).
   - **2f. `DisabledFuncFlagS2CPacket` (`0xA4`, Fixed `5B`)**:
     - `+0x00`: `0xA4`, `+0x01` (`uint32`): `0`.
   - **2g. `SyncEndS2CPacket` (`0x43`, Fixed `1B`)**:
     - `+0x00`: `0x43` (signals completion of initial world burst).
3. **Client $\rightarrow$ Server**: **`SyncEndC2SPacket` (`0x43`, Fixed `9B`)**
   - `+0x00`: `0x43`, `+0x01` (`uint32`): `ClientToken`, `+0x05` (`uint32`): `ClientTick`.
4. **Server $\rightarrow$ Client (Active In-World Confirmation)**:
   - Upon receiving `SyncEndC2SPacket` (`0x43`), the zone marks `PlayerZoneState.IsSyncCompleted = true` and transmits `SyncCurPlayerNormalS2CPacket` (`0x46`, `14B`) and `SyncNpcMinPlayerS2CPacket` (`0x4E`, `13B` with `OffY`, `OffX`, `MapMpsY`, `MapMpsX`).

---

## 2. Continuous Heartbeat & Clean Logout Contracts

| Packet Contract Name | Opcode | Direction | Payload Size | Wire Structure & Validation Rules |
| :--- | :--- | :--- | :--- | :--- |
| **`PingS2CPacket`** | `0x82` | `S2C` | Fixed `5B` | `+0x00`: `0x82`, `+0x01` (`uint32`): `ServerTimestamp` (emitted periodically every `5` seconds). |
| **`PingReplyC2SPacket`** | `0x70` | `C2S` | Fixed `9B` | `+0x00`: `0x70`, `+0x01` (`uint32`): `EchoedServerTimestamp`, `+0x05` (`uint32`): `ClientTimestamp`. Server validates exact 9-byte length and updates session heartbeat timestamp. |
| **`PingReplyS2CPacket`** | `0x99` | `S2C` | Fixed `5B` | `+0x00`: `0x99`, `+0x01` (`uint32`): `EchoedClientTimestamp` (from `+0x05` of `0x70`). |
| **`CloseConnectionC2SPacket`** | `0x9B` | `C2S` | Fixed `1B` | `+0x00`: `0x9B`. Cleanly detaches the player from `MapZoneWorker`, persists character state, and immediately releases `SessionLease`. |
