# Contract: Internal C# 14 Asset Catalogs, Zone Cluster Router & Outbound Sink Ports

**Feature Branch**: `002-spatial-aoi-movement-chat`  
**Date**: 2026-11-09  
**Spec**: [../spec.md](../spec.md) | **Data Model**: [../data-model.md](../data-model.md)

This contract specifies the internal C# 14 interfaces connecting `JXServer.GameAssets`, `JXServer.Contracts`, `JXServer.ZoneEngine`, and `JXServer.Adapter.LegacyJX1`.

---

## 1. Static NPC & Trap Script Catalogs (`JXServer.GameAssets`)

```csharp
namespace JXServer.GameAssets.NpcTemplates;

public interface INpcTemplateCatalog
{
    int Count { get; }

    bool TryGetTemplate(int templateId, out NpcTemplate? template);

    IReadOnlyList<StaticNpcSpawnTemplate> GetAutoExecSpawnsForMap(ushort mapId);
}
```

```csharp
namespace JXServer.GameAssets.WorldMaps;

public interface ITrapScriptCatalog
{
    int Count { get; }

    bool TryGetTrapBinding(uint trapId, out PortalTrapBindingTemplate? binding);
}
```

---

## 2. Control-Plane Position Persistence Port (`JXServer.Contracts`)

```csharp
namespace JXServer.Contracts.Characters;

public sealed record UpdateCharacterWorldSnapshotDto(
    Guid CharacterId,
    long ExpectedLeaseEpoch,
    ushort MapId,
    int MapMpsX,
    int MapMpsY,
    int CurrentLife,
    int CurrentMana,
    ushort CurrentStamina,
    byte FightMode = 0);

public interface ICharacterAppService
{
    Task<IReadOnlyList<CharacterSummaryDto>> GetCharacterRosterAsync(
        GetCharacterRosterInput input,
        CancellationToken cancellationToken = default);

    Task<CreateCharacterResultDto> CreateCharacterAsync(
        CreateCharacterDto input,
        CancellationToken cancellationToken = default);

    Task<DeleteCharacterResultDto> DeleteCharacterAsync(
        DeleteCharacterDto input,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateCharacterWorldSnapshotAsync(
        UpdateCharacterWorldSnapshotDto input,
        CancellationToken cancellationToken = default);
}
```

---

## 3. Sharded Zone Cluster Router Extensions (`JXServer.ZoneEngine`)

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
    /// on the same server, or notifies the session if the target map is hosted externally.
    /// </summary>
    bool TryTransferPlayerAcrossMaps(
        PlayerZoneState player,
        ushort targetMapId,
        int targetMpsX,
        int targetMpsY,
        byte targetFightMode);

    /// <summary>
    /// Broadcasts a cluster-wide chat or system message (World, Whisper, Team, Faction, System)
    /// across all active MapZoneWorker shards.
    /// </summary>
    void RouteClusterChat(DeliverClusterChatZoneCommand chatCommand);
}
```

---

## 4. Transport-Agnostic Player Outbound Sink (`JXServer.ZoneEngine`)

```csharp
namespace JXServer.ZoneEngine.PlayerSessions;

public interface IPlayerOutboundSink
{
    void SendInitialWorldEntrySync(
        PlayerZoneState player,
        string mapName,
        IReadOnlyList<(ushort SkillId, byte SkillLevel)> skills);

    void SendNpcFullSync(in EntityFullSyncSnapshot snapshot);

    void SendNpcCompactSync(
        uint npcId,
        int mpsX,
        int mpsY,
        EntityDoingPosture doing,
        byte lifePer,
        byte camp,
        byte dir);

    void SendSelfPositionSync(
        uint playerNpcId,
        int mpsX,
        int mpsY,
        EntityDoingPosture doing,
        byte dir);

    void SendWalkBroadcast(uint npcId, int destMpsX, int destMpsY);

    void SendRunBroadcast(uint npcId, int destMpsX, int destMpsY);

    void SendJumpBroadcast(uint npcId, int destMpsX, int destMpsY);

    void SendSitBroadcast(uint npcId);

    void SendNormalVitalSync(
        uint playerNpcId,
        short curLife,
        short curMana,
        short curStamina,
        byte dir);

    void SendWorldMapTransitionSync(
        ushort mapId,
        string mapName,
        int regionX,
        int regionY,
        uint playerNpcId,
        int mpsX,
        int mpsY,
        EntityDoingPosture doing,
        byte dir);

    void SendNotifyServerExchange(
        uint ipAddressLittleEndian,
        ushort port,
        Guid handoffTicketId);

    void SendPlayerTalkBroadcast(uint talkerNpcId, string messageUtf8);

    void SendChatMessage(uint senderNpcId, string senderNameUtf8, string messageUtf8);

    void SendSystemMessage(string messageUtf8);

    void SendPingRequest(uint serverTimestamp);

    void SendPingReply(uint clientTimestamp);

    void Disconnect(PlayerDetachReason reason);
}
```
