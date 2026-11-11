# Quickstart Guide: Core Authentication, Character Roster & Session Leases

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  

---

## 1. Prerequisites

- **.NET 10 SDK** installed (`dotnet --version` outputs `10.0.x`)
- Working repository at `d:\Projects\jx-server`

---

## 2. Configuration (`appsettings.json`)

Configure Bishop Gateway and auto-provisioning settings in `src/JXServer.Host/appsettings.json`:

```json
{
  "LegacyGateway": {
    "Port": 5622,
    "AutoProvisionAccounts": true,
    "FallbackUnloadedStarterVillage": true,
    "DefaultFallbackStarterVillageMapId": 53
  },
  "SessionLeases": {
    "HandoffTicketTtlSeconds": 30,
    "HeartbeatIntervalSeconds": 5,
    "HeartbeatTimeoutSeconds": 15
  }
}
```

---

## 3. Running Automated Tests

Run the integration test suites verifying authentication, character CRUD, and session lease fencing:

```powershell
# From repository root (d:\Projects\jx-server)
dotnet test tests/JXServer.Integration.Tests/JXServer.Integration.Tests.csproj --filter FullyQualifiedName~CharacterLifecycleIntegrationTests
dotnet test tests/JXServer.Integration.Tests/JXServer.Integration.Tests.csproj --filter FullyQualifiedName~SessionLeaseEpochFencingAdversarialTests
dotnet test tests/JXServer.Integration.Tests/JXServer.Integration.Tests.csproj --filter FullyQualifiedName~GatewayWorldEntryIntegrationTests
```

Expected output:
```text
Passed!  - Failed: 0, Passed: 2, Skipped: 0 - JXServer.Integration.Tests.dll (CharacterLifecycleIntegrationTests)
Passed!  - Failed: 0, Passed: 4, Skipped: 0 - JXServer.Integration.Tests.dll (SessionLeaseEpochFencingAdversarialTests)
Passed!  - Failed: 0, Passed: 2, Skipped: 0 - JXServer.Integration.Tests.dll (GatewayWorldEntryIntegrationTests)
```

---

## 4. Programmatic Workflow Example

```csharp
// 1. Authenticate Account
var accountAppService = serviceProvider.GetRequiredService<IAccountAppService>();
var authResult = await accountAppService.AuthenticateAsync(new AuthenticateAccountInput
{
    UserName = "testaccount",
    PasswordMd5Hex = "E10ADC3949BA59ABBE56E057F20F883E",
    ProtocolVersion = 0x00000041,
    KickOnlineFlag = 1 // Takeover lingering session
});

// 2. Query Character Roster
var characterAppService = serviceProvider.GetRequiredService<ICharacterAppService>();
var roster = await characterAppService.GetCharacterRosterAsync(new GetCharacterRosterInput
{
    AccountId = authResult.AccountId
});

// 3. Select Character to Obtain World Handoff Ticket
var sessionLeaseAppService = serviceProvider.GetRequiredService<ISessionLeaseAppService>();
var ticketDto = await sessionLeaseAppService.SelectCharacterAsync(new SelectCharacterInput
{
    AccountId = authResult.AccountId,
    CharacterId = roster[0].CharacterId
});

Console.WriteLine($"Ticket: {ticketDto.TicketGuid}, Target Map: {ticketDto.TargetMapId}");
```
