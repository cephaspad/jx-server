namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Routes lock-free <see cref="IZoneCommand"/> instances to sharded 18-Hz <c>MapZoneWorker</c> loops
/// and coordinates cross-zone account eviction.
/// </summary>
public interface IZoneClusterRouter
{
    /// <summary>
    /// Enqueues <paramref name="command"/> into the target map's single-reader channel.
    /// </summary>
    bool TryDispatchToZone(ushort mapId, IZoneCommand command);

    /// <summary>
    /// Immediately evicts any active player belonging to <paramref name="accountId"/> with <c>LeaseEpoch &lt;= supersededEpoch</c>
    /// and awaits snapshot persistence (`FR-008`).
    /// </summary>
    Task EvictAccountFromAllZonesAsync(
        Guid accountId,
        long supersededEpoch,
        Guid? activeCharacterId = null,
        ushort? activeMapId = null,
        CancellationToken cancellationToken = default);
}
