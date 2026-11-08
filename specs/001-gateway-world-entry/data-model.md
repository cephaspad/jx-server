# Phase 1 Data Model: Foundational Gateway Authentication, Character Lifecycle & World Entry (`001-gateway-world-entry`)

**Date**: 2026-11-08  
**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md)

All types follow the **ABP Framework DDD Naming Conventions** and the **5 Real-Time 18-Hz Game Server Conventions** defined in the [JXServer Constitution](../../.specify/memory/constitution.md) (`One-Type-Per-File`, pluralized feature folders, unit-explicit coordinate suffixes `*MpsX`/`*MpsY`/`*CellX`/`*CellY`/`*RegionX`/`*RegionY`, and strict separation between EF Core Domain Aggregates and 18-Hz `*ZoneState` structs).

---

## 1. Primitives Tier (`JXServer.Primitives`)

### 1.1 Feature Folder: `Accounts/`
- **`AccountConsts`**:
  - `MinUserNameByteLength = 1`, `MaxUserNameByteLength = 31`, `FixedAccountBufferSize = 32`
  - `PasswordMd5HexLength = 32`, `FixedPasswordBufferSize = 64`
  - `RequiredProtocolVersion = 0x00000041` (`65`)
- **`AccountStatus`** (`enum : byte`): `Active = 0`, `Frozen = 1`, `Locked = 2`
- **`LoginResultCode`** (`enum : uint`):
  - `Success = 0x00050001`
  - `Failed = 0x00050002`
  - `AccountOrPasswordError = 0x00050003`
  - `AccountAlreadyOnline = 0x00050004`
  - `Timeout = 0x00050005`
  - `InvalidProtocolVersion = 0x0005000A`
  - `AccountFrozen = 0x0005000B`

### 1.2 Feature Folder: `Characters/`
- **`CharacterConsts`**:
  - `MaxRolesPerAccount = 3`
  - `MinNameByteLength = 6`, `MaxNameByteLength = 30`, `FixedRoleNameBufferSize = 32`
  - `MaxSkillSlots = 80`
  - `ExpHighDivisor = 2_000_000_000L`
- **`CharacterGender`** (`enum : byte`): `Male = 0`, `Female = 1`
- **`ElementSeries`** (`enum : byte`): `Metal = 0`, `Wood = 1`, `Water = 2`, `Fire = 3`, `Earth = 4`
- **`EntityKind`** (`enum : byte`): `Monster = 0`, `Player = 1`, `Partner = 2`, `DialogNpc = 3`

### 1.3 Feature Folder: `SessionLeases/`
- **`SessionLeaseConsts`**:
  - `DefaultHandoffTicketTtlSeconds = 30`
  - `DefaultHeartbeatIntervalSeconds = 5`
  - `DefaultHeartbeatTimeoutSeconds = 15`
- **`SessionLeaseState`** (`enum : byte`):
  - `Offline = 0`
  - `GatewayAuthenticated = 1`
  - `HandoffIssued = 2`
  - `WorldAttaching = 3`
  - `InWorld = 4`

### 1.4 Feature Folder: `WorldZones/`
- **`WorldCoordinateConsts`**:
  - `CellWidthMps = 32`, `CellHeightMps = 32`
  - `RegionWidthCells = 16`, `RegionHeightCells = 32`
  - `RegionWidthMps = 512`, `RegionHeightMps = 1024`
  - `TicksPerSecond = 18`
- **`WorldPosition`** (`readonly record struct`):
  - Fields: `ushort MapId`, `int MpsX`, `int MpsY`
  - Computed properties: `int RegionX` (`MpsX / 512`), `int RegionY` (`MpsY / 1024`), `int PackedRegionId` (`(RegionY << 16) | RegionX`), `int CellX` (`(MpsX % 512) / 32`), `int CellY` (`(MpsY % 1024) / 32`), `ushort OffX` (`(ushort)((MpsX % 32) << 10)`), `ushort OffY` (`(ushort)((MpsY % 32) << 10)`)

### 1.5 Feature Folder: `ErrorCodes/`
- **`JXServerErrorCodes`**:
  - `AccountInvalidCredentials = "JXServer:Account:0001"`
  - `AccountFrozenOrLocked = "JXServer:Account:0002"`
  - `AccountAlreadyOnline = "JXServer:Account:0003"`
  - `CharacterLimitReached = "JXServer:Character:0001"`
  - `CharacterNameDuplicate = "JXServer:Character:0002"`
  - `CharacterNameProhibited = "JXServer:Character:0003"`
  - `CharacterInvalidStarterVillage = "JXServer:Character:0004"`
  - `CharacterNotFound = "JXServer:Character:0005"`
  - `SessionHandoffTicketInvalidOrExpired = "JXServer:Session:0001"`
  - `SessionEpochSuperseded = "JXServer:Session:0002"`

---

## 2. Static Asset Templates Tier (`JXServer.GameAssets`)

### 2.1 Feature Folder: `WorldMaps/`
- **`MapDescriptor`** (immutable record):
  - `ushort MapId`, `string MapNameUnicode`, `string RawSubPath`, `int RegionBeginX`, `int RegionBeginY`, `int RegionWidth`, `int RegionHeight`
- **`RegionObstacleGrid`** (immutable class):
  - `int RegionX`, `int RegionY`, `ReadOnlyMemory<int> CellObstacles` (`16 * 32 = 512` `int32` entries from `Region_S.dat` Section 0)
- **`IMapCatalog` / `MapCatalog`**:
  - Singleton read-only registry of loaded maps from `settings/maplist.ini`, `server1/maps/*.wor` (or `pak/maps.pak`), and `settings/nativeplacelist.ini`.

### 2.2 Feature Folder: `PlayerTemplates/`
- **`StarterSkillEntry`** (`readonly record struct`): `ushort SkillId`, `byte Level`
- **`CharacterBaseTemplate`** (immutable record loaded from `newplayerini00..09.ini` & `level_add.txt`):
  - `ElementSeries Series`, `CharacterGender Gender`, `ushort BaseStrength`, `ushort BaseDexterity`, `ushort BaseVitality`, `ushort BaseEnergy`, `ushort BaseLucky`, `ushort InitialLifeMax`, `ushort InitialManaMax`, `ushort InitialStaminaMax`, `IReadOnlyList<StarterSkillEntry> StarterSkills`
- **`StarterVillageTemplate`** (immutable record loaded from `nativeplacelist.ini`, `revivalid.ini`, `revivepos.ini`):
  - `ushort MapId`, `string VillageNameUnicode`, `int RevivalId`, `int SpawnMpsX`, `int SpawnMpsY`
- **`IPlayerTemplateCatalog` / `PlayerTemplateCatalog`**:
  - Singleton read-only registry providing starter stats, starter village spawn coordinates, and role name filter validation (`goddess_rolename.flt`).

---

## 3. Control-Plane DDD Tier (`JXServer.Domain`)

### 3.1 Aggregate Root: `Account` (`Accounts/Account.cs`)
- **Primary Key**: `Guid Id` (sequential GUID)
- **Properties** (`private set`):
  - `string UserName` (unique, normalized ASCII `1..31` chars)
  - `string PasswordHashUpperMd5` (`32`-char uppercase hex MD5)
  - `string? SecondaryPasswordHashUpperMd5` (optional `32`-char uppercase hex MD5 for character deletion `0x3A`; falls back to primary password/session when null)
  - `AccountStatus Status` (`Active`, `Frozen`, `Locked`)
  - `uint RemainingTimeSeconds`
  - `long CurrentLeaseEpoch` (monotonically incremented on every new login / session takeover)
  - `DateTimeOffset CreatedAtUtc`, `DateTimeOffset? LastLoginAtUtc`
- **Domain Methods**:
  - `VerifyPassword(string candidateMd5Upper)`
  - `VerifyDeletionPassword(string candidateMd5Upper)` (verifies against `SecondaryPasswordHashUpperMd5` if set, or `PasswordHashUpperMd5`/authenticated session when unset per `FR-006`)
  - `AdvanceLeaseEpoch()` $\rightarrow$ increments and returns `++CurrentLeaseEpoch`
  - `Freeze()` / `Activate()`
- **Domain Service**: `AccountManager` (handles credential verification and optional first-login auto-provisioning persisting username + MD5 digest per `FR-003`).
- **Repository Interface**: `IAccountRepository` (`FindByUserNameAsync`, `InsertAsync`, `UpdateAsync`).

### 3.2 Aggregate Root: `Character` (`Characters/Character.cs`)
- **Primary Key**: `Guid Id` (sequential GUID)
- **Foreign Reference**: `Guid AccountId` (indexed, max 3 active characters per `AccountId`)
- **Properties** (`private set`):
  - `byte SlotIndex` (`0..2`)
  - `string RoleName` (unique across server, Unicode UTF-8, `6..30` TCVN3 bytes)
  - `CharacterGender Gender` (`0`=Male, `1`=Female)
  - `ElementSeries Series` (`0..4`)
  - `byte Level` (`1..255`, initial `1`)
  - `long Experience` (`0`)
  - `int LeadExperience` (`0`)
  - `ushort MapId` (starter village `m_NativePlaceId`, e.g., `53`)
  - `int RevivalMapId`, `int RevivalPointId`, `int MapMpsX`, `int MapMpsY`
  - `int CurrentLife`, `ushort MaxLife`
  - `int CurrentMana`, `ushort MaxMana`
  - `ushort CurrentStamina`, `ushort MaxStamina`
  - `ushort Strength`, `ushort Dexterity`, `ushort Vitality`, `ushort Energy`, `ushort Lucky`
  - `ushort UnallocatedAttributePoints`, `ushort UnallocatedSkillPoints`
  - `int InventoryMoney`, `int RepositoryMoney`
  - `byte CurrentFactionId` (`0xFF` when none), `byte FirstFactionId` (`0xFF` when none), `ushort FactionJoinCount`
  - `IReadOnlyList<CharacterSkillValue> Skills` (mapped via EF Core 10 JSON column `ToJson()`)
  - `DateTimeOffset CreatedAtUtc`, `DateTimeOffset UpdatedAtUtc`
- **Value Object**: `CharacterSkillValue` (`ushort SkillId`, `byte BaseLevel`, `byte BonusLevel`, `ushort ExpPercent`)
- **Domain Methods**:
  - `UpdateWorldSnapshot(ushort mapId, int mpsX, int mpsY, int currentLife, int currentMana, ushort currentStamina)`
- **Domain Service**: `CharacterManager` (enforces max 3 characters per account, validates character name uniqueness and `goddess_rolename.flt` filter rules, resolves active or fallback starter village per `FR-005`, and initializes a new `Character` from `CharacterBaseTemplate` + `StarterVillageTemplate`).
- **Repository Interface**: `ICharacterRepository` (`GetListByAccountIdAsync`, `FindByNameAsync`, `CountByAccountIdAsync`, `InsertAsync`, `UpdateAsync`, `DeleteAsync` — permanently removing the character record so both `SlotIndex` and `RoleName` are immediately freed for reuse per `FR-006`).

### 3.3 Aggregate Root: `SessionLease` (`SessionLeases/SessionLease.cs`)
- **Primary Key**: `Guid AccountId` (`1:1` with `Account`)
- **Properties** (`private set`):
  - `string UserName`
  - `long LeaseEpoch`
  - `SessionLeaseState State`
  - `Guid? ActiveCharacterId`
  - `string? ActiveRoleName`
  - `Guid? HandoffTicketGuid` (16-byte one-time token sent in `0x34` and verified in `0x42`)
  - `ushort? AssignedMapId`
  - `DateTimeOffset IssuedAtUtc`
  - `DateTimeOffset? TicketExpiresAtUtc`
  - `DateTimeOffset LastHeartbeatAtUtc`
- **Domain Methods**:
  - `BeginGatewaySession(long newEpoch, DateTimeOffset nowUtc)`
  - `IssueWorldHandoffTicket(Guid characterId, string roleName, ushort mapId, Guid ticketGuid, DateTimeOffset nowUtc, TimeSpan ticketTtl)`
  - `ConsumeWorldHandoffTicket(Guid presentedTicketGuid, DateTimeOffset nowUtc)`
  - `RecordHeartbeat(DateTimeOffset nowUtc)`
  - `Release(DateTimeOffset nowUtc)`
- **Domain Service**: `SessionLeaseManager` (coordinates atomic epoch advancement, old session eviction notification, and handoff ticket verification).

---

## 4. 18-Hz Zone Simulation Tier (`JXServer.ZoneEngine`)

### 4.1 Feature Folder: `PlayerSessions/`
- **`PlayerZoneState`** (pure in-memory runtime class inside `MapZoneWorker`, completely decoupled from EF Core):
  - `uint RuntimeNpcId` (unique non-zero entity ID inside the zone, e.g., `1..N`, sent as `m_dwID` in `0x44` and `ID` in `0x4C`)
  - `Guid AccountId`, `Guid CharacterId`, `long LeaseEpoch`
  - `string RoleNameUnicode`, `CharacterGender Gender`, `ElementSeries Series`, `byte Level`
  - `ushort MapId`, `int CurrentMpsX`, `int CurrentMpsY`
  - `int CurrentLife`, `ushort MaxLife`, `int CurrentMana`, `ushort MaxMana`, `ushort CurrentStamina`, `ushort MaxStamina`
  - `ushort Strength`, `ushort Dexterity`, `ushort Vitality`, `ushort Energy`, `ushort Lucky`
  - `uint ClientToken`, `uint ClientTick` (populated when `0x43` `c2s_syncend` is received)
  - `bool IsSyncCompleted` (`false` during initial sync, `true` after `0x43` `c2s_syncend` reply)
  - `uint LastSentPingServerTimestamp`, `long LastPingReceivedTick`
  - `IPlayerOutboundSink OutboundSink` (abstraction for enqueueing outgoing `s2c_*` packets to the player's TCP pipe)
- **State Lifecycle inside `MapZoneWorker`**:
  1. `Attaching` (on `AttachPlayerZoneCommand` triggered by valid `0x42` `c2s_logiclogin`): Allocates `RuntimeNpcId`, sends `0x49` (`s2c_syncworld`), `0x44` (`s2c_synccurplayer`), `0x45` (`s2c_synccurplayerskill`), `0x46` (`s2c_synccurplayernormal`), `0x4C` (`s2c_syncnpc` for self), `0xA4` (`s2c_disabledfuncflag`), and `0x43` (`s2c_syncend` 1B).
  2. `ActiveInWorld` (on `CompletePlayerSyncZoneCommand` triggered by `0x43` `c2s_syncend` 9B): Sets `IsSyncCompleted = true`, sends initial `0x46` (`s2c_synccurplayernormal`) and `0x4E` (`s2c_syncnpcminplayer`), and enters the periodic heartbeat ping cycle (`0x82` $\rightarrow$ `0x70` $\rightarrow$ `0x99`).
  3. `Detached` (on `DetachPlayerZoneCommand` triggered by `0x9B` logout, socket close, heartbeat timeout, or lease epoch eviction): Removes `PlayerZoneState` from the zone and dispatches a snapshot save back to the Control Plane.
