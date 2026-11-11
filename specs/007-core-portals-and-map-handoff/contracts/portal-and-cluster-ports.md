# Contract: Internal Cluster Router & Portal Catalog Ports

**Feature Branch**: `007-core-portals-and-map-handoff` | **Date**: 2026-11-11

---

## 1. Trap Script Catalog Port (`JXServer.GameAssets.WorldMaps`)

```csharp
namespace JXServer.GameAssets.WorldMaps;

public interface ITrapScriptCatalog
{
    int Count { get; }

    bool TryGetTrapBinding(uint trapId, out PortalTrapBindingTemplate? binding);
}
```

---

## 2. Sharded Cluster Router Port (`JXServer.ZoneEngine.WorldZones`)

```csharp
namespace JXServer.ZoneEngine.WorldZones;

public interface IZoneClusterRouter
{
    bool IsMapLoaded(ushort mapId);

    bool TryRouteCommand(ushort mapId, IZoneCommand command);

    bool TryGetWorker(ushort mapId, out MapZoneWorker? worker);

    IReadOnlyCollection<MapZoneWorker> GetActiveWorkers();

    /// <summary>
    /// Transfers a live PlayerZoneState from its current map worker to a destination map worker
    /// on the same server, or dispatches a cross-node transfer packet if hosted externally.
    /// </summary>
    bool TryTransferPlayerAcrossMaps(
        PlayerZoneState player,
        ushort targetMapId,
        int targetMpsX,
        int targetMpsY,
        byte targetFightMode);
}
```

---

## 3. World Outbound Transition Port (`JXServer.ZoneEngine.PlayerSessions`)

```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public interface IWorldOutboundSink
{
    void SendWorldMapTransitionSync(
        PlayerZoneState playerState,
        string mapNameUnicode,
        uint currentFrameTick);

    void SendNotifyServerExchange(
        Guid handoffTicketGuid,
        uint ipAddressLittleEndian,
        ushort port);
}
```
