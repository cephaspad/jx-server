# Contract: Internal Spatial Grid & NPC Spawn Ports

**Feature Branch**: `005-core-spatial-aoi-and-spawns` | **Date**: 2026-11-11

---

## 1. Static NPC Template Catalog Port (`JXServer.GameAssets.NpcTemplates`)

```csharp
namespace JXServer.GameAssets.NpcTemplates;

public interface INpcTemplateCatalog
{
    int Count { get; }

    bool TryGetTemplate(int templateId, out NpcTemplate? template);

    IReadOnlyList<StaticNpcSpawnTemplate> GetAutoExecSpawnsForMap(ushort mapId);
}
```

---

## 2. World & Spatial Outbound Notification Port (`JXServer.ZoneEngine.PlayerSessions`)

```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public interface IWorldOutboundSink
{
    void SendNpcFullSync(in EntityFullSyncSnapshot snapshot);

    void SendNpcCompactSync(
        uint npcId,
        int mpsX,
        int mpsY,
        EntityDoingPosture doing,
        byte lifePer,
        byte camp,
        byte dir);

    void SendSelfPositionSync(PlayerZoneState playerState);
}
```

---

## 3. On-Demand Entity Query Zone Command (`JXServer.ZoneEngine.PlayerSessions`)

```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public sealed record RequestEntitySyncZoneCommand(
    Guid RequesterCharacterId,
    long LeaseEpoch,
    uint TargetNpcId,
    bool IsPlayerRequest) : IZoneCommand;
```
