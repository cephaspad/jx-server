using JXServer.ZoneEngine.Chats;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Routes lock-free <see cref="IZoneCommand"/> instances to sharded 18-Hz <c>MapZoneWorker</c> loops,
/// coordinates same-server cross-map player transfers (`FR-009`), routes global/whisper/faction chat (`FR-011`),
/// and handles cross-zone account eviction (`FR-008`).
/// </summary>
public interface IZoneClusterRouter
{
    /// <summary>
    /// Determines whether a <see cref="MapZoneWorker"/> shard is active for <paramref name="mapId"/>.
    /// </summary>
    bool IsMapLoaded(ushort mapId);

    /// <summary>
    /// Enqueues <paramref name="command"/> into the target map's single-reader channel.
    /// </summary>
    bool TryDispatchToZone(ushort mapId, IZoneCommand command);

    /// <summary>
    /// Transfers a live <see cref="PlayerZoneState"/> from its source map worker to the destination map worker
    /// for <paramref name="targetMapId"/> (`FR-009`).
    /// </summary>
    bool TryTransferPlayerAcrossMaps(
        PlayerZoneState player,
        ushort targetMapId,
        int arrivalMpsX,
        int arrivalMpsY,
        byte arrivalFightMode);

    /// <summary>
    /// Broadcasts a cluster-wide chat or system message (`World`, `PrivateWhisper`, `Team`, `Faction`, `System`)
    /// across all active <see cref="MapZoneWorker"/> shards (`FR-011`).
    /// </summary>
    void RouteClusterChat(DeliverClusterChatZoneCommand chatCommand);

    /// <summary>
    /// Immediately evicts any active player belonging to <paramref name="accountId"/> with <c>LeaseEpoch &lt;= supersededEpoch</c>
    /// and awaits snapshot persistence.
    /// </summary>
    Task EvictAccountFromAllZonesAsync(
        Guid accountId,
        long supersededEpoch,
        Guid? activeCharacterId = null,
        ushort? activeMapId = null,
        CancellationToken cancellationToken = default);
}
