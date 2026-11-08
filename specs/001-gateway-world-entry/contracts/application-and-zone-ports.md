# Contract 03: Internal Control-Plane Application Services & 18-Hz Zone Ports

**Feature**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)

All internal ports follow the **ABP Framework DDD Conventions** (`I*AppService`, directional DTOs, ETOs) and **18-Hz Zone Engine Conventions** (`IZoneDispatcher`, `*ZoneCommand`), ensuring that network adapters (`JXServer.Adapter.LegacyJX1` and future `JXServer.Adapter.Modern`) never access `DbContext` or domain entities directly.

---

## 1. Control-Plane Application Service Contracts (`JXServer.Contracts`)

### 1.1 `Accounts/IAccountAppService`
- **`AuthenticateAsync(AuthenticateAccountInput input, CancellationToken ct)` $\rightarrow$ `AuthenticateAccountResultDto`**
  - **Input (`AuthenticateAccountInput`)**: `string UserName`, `string PasswordHashUpperMd5`, `uint ProtocolVersion`, `bool KickOnline`
  - **Output (`AuthenticateAccountResultDto`)**: `LoginResultCode ResultCode`, `Guid? AccountId`, `long LeaseEpoch`, `uint RemainingTimeSeconds`
  - **Behavior**: Validates protocol version (`0x41`), looks up (or auto-provisions if `AutoProvisionAccounts` option is enabled) the `Account`, verifies password hash and account status, and coordinates with `SessionLeaseManager` to issue a new `LeaseEpoch` (fencing and evicting any prior session when `KickOnline == true`).

### 1.2 `Characters/ICharacterAppService`
- **`GetCharacterRosterAsync(GetCharacterRosterInput input, CancellationToken ct)` $\rightarrow$ `IReadOnlyList<CharacterSummaryDto>`**
  - Returns up to 3 active characters (`SlotIndex`, `CharacterId`, `RoleName`, `Gender`, `Series`, `Level`) for the authenticated `AccountId`.
- **`CreateCharacterAsync(CreateCharacterDto input, CancellationToken ct)` $\rightarrow$ `CreateCharacterResultDto`**
  - **Input (`CreateCharacterDto`)**: `Guid AccountId`, `long LeaseEpoch`, `string RoleNameUnicode`, `CharacterGender Gender`, `ElementSeries Series`, `ushort StarterVillageMapId`
  - **Output (`CreateCharacterResultDto`)**: `bool Succeeded`, `string? ErrorCode`, `CharacterDto? Character`, `WorldHandoffTicketDto? HandoffTicket`
  - **Behavior**: Verifies active `LeaseEpoch`, enforces `< 3` characters per account, checks `IPlayerTemplateCatalog` name filter rules (`goddess_rolename.flt`) and starter village validity (redirecting to default active starter village `Map 53` when the requested village is valid in `nativeplacelist.ini` but not loaded in `worldset.ini` and fallback mode is enabled, or rejecting when disabled per `FR-005`), creates the `Character` initialized from `CharacterBaseTemplate` + `StarterVillageTemplate`, persists via `ICharacterRepository`, and issues a `WorldHandoffTicketDto`.
- **`DeleteCharacterAsync(DeleteCharacterDto input, CancellationToken ct)` $\rightarrow$ `DeleteCharacterResultDto`**
  - Validates account ownership and `Account.VerifyDeletionPassword` (checking secondary password if set, or primary password/authenticated session when unset per `FR-006`), permanently deletes the character via `ICharacterRepository.DeleteAsync`, and immediately frees both the character slot and global character name.

### 1.3 `SessionLeases/ISessionLeaseAppService`
- **`SelectCharacterForWorldEntryAsync(SelectCharacterInput input, CancellationToken ct)` $\rightarrow$ `WorldHandoffTicketDto`**
  - **Input (`SelectCharacterInput`)**: `Guid AccountId`, `long LeaseEpoch`, `string RoleNameUnicode`
  - **Output (`WorldHandoffTicketDto`)**: `bool Permitted`, `Guid TicketGuid`, `long LeaseEpoch`, `Guid AccountId`, `Guid CharacterId`, `string RoleNameUnicode`, `ushort TargetMapId`, `string GameServerHost`, `ushort GameServerPort`
  - **Behavior**: Verifies that `TargetMapId` is active in `worldset.ini` (or applies configured fallback starter village `Map 53` when unloaded-map fallback is enabled; otherwise returns `Permitted = false` per `FR-007`), and issues a single-use 16-byte `TicketGuid` bound to `LeaseEpoch`.
- **`RedeemWorldHandoffTicketAsync(RedeemHandoffTicketInput input, CancellationToken ct)` $\rightarrow$ `RedeemHandoffTicketResultDto`**
  - **Input (`RedeemHandoffTicketInput`)**: `Guid TicketGuid`
  - **Output (`RedeemHandoffTicketResultDto`)**: `bool Succeeded`, `long LeaseEpoch`, `CharacterDto? Character`
  - **Behavior**: Atomically verifies that `TicketGuid` exists, has not expired (30s TTL), and matches the account's current authoritative `LeaseEpoch`, transitioning the lease state to `InWorld`.
- **`CompleteLogoutOrEvictAsync(ReleaseSessionLeaseInput input, CancellationToken ct)`**
  - Saves character snapshot (`UpdateCharacterWorldSnapshotDto`) and releases the lease if `LeaseEpoch` matches the active epoch.

---

## 2. 18-Hz Zone Engine Ports (`JXServer.ZoneEngine`)

### 2.1 `WorldZones/IZoneClusterRouter`
- **`TryDispatchToZone(ushort mapId, IZoneCommand command)` $\rightarrow$ `bool`**
  - Routes a lock-free command into the target map's single-reader `Channel<IZoneCommand>`.
- **`EvictAccountFromAllZonesAsync(Guid accountId, long supersededEpoch, CancellationToken ct)`**
  - Invoked by the Control Plane whenever an account's `LeaseEpoch` advances (`bKickOnline = 1`), immediately disconnecting any lingering `PlayerZoneState` with `LeaseEpoch <= supersededEpoch` and flushing its latest coordinates/vitals.

### 2.2 Milestone 1 `IZoneCommand` Contracts (`PlayerSessions/`)
- **`AttachPlayerZoneCommand`**: Carries `CharacterDto`, `long LeaseEpoch`, and `IPlayerOutboundSink` into the target `MapZoneWorker` upon `0x42` (`c2s_logiclogin`) verification.
- **`CompletePlayerSyncZoneCommand`**: Carries `Guid CharacterId`, `long LeaseEpoch`, `uint ClientToken`, `uint ClientTick` upon `0x43` (`c2s_syncend`) receipt.
- **`ProcessPlayerPingZoneCommand`**: Carries `Guid CharacterId`, `long LeaseEpoch`, `uint EchoedServerTimestamp`, `uint ClientTimestamp` upon `0x70` (`c2s_ping`) receipt.
- **`DetachPlayerZoneCommand`**: Carries `Guid CharacterId`, `long LeaseEpoch`, `PlayerDetachReason Reason` (`CleanLogout`, `SocketClosed`, `HeartbeatTimeout`, `LeaseSuperseded`).
