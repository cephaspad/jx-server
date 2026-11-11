# Quickstart Guide: Core Zone Modular Spine, World Entry & Simulation Harness

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)  

---

## 1. Prerequisites

- **.NET 10 SDK** installed (`dotnet --version` outputs `10.0.x`)
- Working repository at `d:\Projects\jx-server`

---

## 2. Running Simulation Harness & World Entry Tests

Execute the automated test suites verifying the modular zone spine, sub-5ms simulation harness, and world entry:

```powershell
# From repository root (d:\Projects\jx-server)
# 1. Deterministic Simulation Harness Tests (Steps 1,000 frames under 5ms)
dotnet test tests/JXServer.ZoneEngine.Tests/JXServer.ZoneEngine.Tests.csproj --filter FullyQualifiedName~SimulationHarnessTests

# 2. Modular Spine & Fault Isolation Tests
dotnet test tests/JXServer.ZoneEngine.Tests/JXServer.ZoneEngine.Tests.csproj --filter FullyQualifiedName~ModularZoneDeconstructionTests

# 3. GameServer TCP World Entry Loopback Integration Tests
dotnet test tests/JXServer.Integration.Tests/JXServer.Integration.Tests.csproj --filter FullyQualifiedName~GatewayWorldEntryIntegrationTests

# 4. Character & Item Foundational Persistence Integration Tests
dotnet test tests/JXServer.Integration.Tests/JXServer.Integration.Tests.csproj --filter FullyQualifiedName~CharacterItemPersistenceIntegrationTests
```

Expected output:
```text
Passed!  - Failed: 0, Passed: X, Skipped: 0 - JXServer.ZoneEngine.Tests.dll
Passed!  - Failed: 0, Passed: 2, Skipped: 0 - JXServer.Integration.Tests.dll (GatewayWorldEntryIntegrationTests)
Passed!  - Failed: 0, Passed: 4, Skipped: 0 - JXServer.Integration.Tests.dll (CharacterItemPersistenceIntegrationTests)
```

---

## 3. Working with `ZoneSimulationHarness`

```csharp
using JXServer.ZoneEngine.Tests.SimulationHarnesses;

// Instantiate harness for Map 53 with deterministic random seed
await using var harness = await ZoneSimulationHarness.CreateAsync(mapId: 53, randomSeed: 1337);

// Spawn a simulated player into the harness
var player = harness.SpawnPlayer(characterId: Guid.NewGuid(), roleName: "Hiệp Khách", mpsX: 51200, mpsY: 102400);

// Advance 1,000 virtual frames (~55.5 seconds of game time) in < 5ms
var stopwatch = Stopwatch.StartNew();
await harness.StepFramesAsync(1000);
stopwatch.Stop();

Console.WriteLine($"Stepped 1,000 frames in {stopwatch.ElapsedMilliseconds} ms!");

// Inspect outbound notifications recorded for the player
var events = harness.EventRecorder.GetEventsForPlayer(player.CharacterId);
Console.WriteLine($"Recorded {events.Count} outbound events.");
```

---

## 4. Registering a Modular `IZoneModule`

```csharp
using JXServer.ZoneEngine.WorldZones;

public sealed class SampleGameplayModule : IZoneModule
{
    public void Register(IZoneExtensionBuilder builder)
    {
        // 1. Register a typed zero-boxing command handler
        builder.RegisterCommandHandler<SampleCommand, SampleCommandHandler>();

        // 2. Register an ordered per-frame tick subsystem
        builder.RegisterTickSystem<SampleTickSystem>(
            ZoneTickPhase.StatusEffects,
            subOrder: 10,
            systemName: "SampleSubsystem");

        // 3. Register an isolated per-player feature state slot
        builder.RegisterFeatureSlot(SampleFeatureSlotKeys.SlotKey);
    }
}
```
