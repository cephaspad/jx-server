# Contract: Movement Simulation & Outbound Ports

**Feature Branch**: `006-core-movement-and-obstacles` | **Date**: 2026-11-11

---

## 1. Outbound Movement Broadcast Port (`JXServer.ZoneEngine.PlayerSessions`)

```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public interface IWorldOutboundSink
{
    void SendWalkBroadcast(uint npcId, int destMpsX, int destMpsY);

    void SendRunBroadcast(uint npcId, int destMpsX, int destMpsY);

    void SendJumpBroadcast(uint npcId, int destMpsX, int destMpsY);

    void SendSitBroadcast(uint npcId);
}
```

---

## 2. Movement Phase 500 Tick Contract (`JXServer.ZoneEngine.WorldZones`)

```csharp
namespace JXServer.ZoneEngine.WorldZones;

public sealed class MovementAndPostureZoneTickSystem : IZoneTickSystem
{
    public string Name => "MovementAndPosture";

    public ZoneTickPhase Phase => ZoneTickPhase.MovementAndPosture; // 500

    public int SubOrder => 0;

    public ValueTask ExecuteAsync(ZoneExecutionContext context, CancellationToken cancellationToken);
}
```
