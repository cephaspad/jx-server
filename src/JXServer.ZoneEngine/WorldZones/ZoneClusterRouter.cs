using System.Collections.Frozen;
using JXServer.Contracts.SessionLeases;
using JXServer.GameAssets.WorldMaps;
using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.PlayerSessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Singleton cluster router managing all active <see cref="MapZoneWorker"/> instances for maps loaded in <c>worldset.ini</c>.
/// </summary>
public sealed class ZoneClusterRouter : IZoneClusterRouter
{
    private readonly FrozenDictionary<ushort, MapZoneWorker> _workersByMapId;

    public IReadOnlyCollection<MapZoneWorker> Workers => _workersByMapId.Values;

    public ZoneClusterRouter(
        IMapCatalog mapCatalog,
        ISessionLeaseAppService sessionLeaseAppService,
        IOptions<ZoneEngineOptions> options,
        TimeProvider timeProvider,
        ILogger<ZoneClusterRouter> logger)
    {
        ArgumentNullException.ThrowIfNull(mapCatalog);
        ArgumentNullException.ThrowIfNull(sessionLeaseAppService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        ZoneEngineOptions cfg = options.Value;
        var dict = new Dictionary<ushort, MapZoneWorker>();
        foreach (MapDescriptor desc in mapCatalog.ActiveMaps)
        {
            dict[desc.MapId] = new MapZoneWorker(desc, sessionLeaseAppService, cfg, timeProvider, logger);
        }

        _workersByMapId = dict.ToFrozenDictionary();
    }

    public bool TryGetWorker(ushort mapId, out MapZoneWorker? worker) =>
        _workersByMapId.TryGetValue(mapId, out worker);

    public bool TryDispatchToZone(ushort mapId, IZoneCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _workersByMapId.TryGetValue(mapId, out MapZoneWorker? worker) &&
               worker.TryEnqueueCommand(command);
    }

    public async Task EvictAccountFromAllZonesAsync(
        Guid accountId,
        long supersededEpoch,
        Guid? activeCharacterId = null,
        ushort? activeMapId = null,
        CancellationToken cancellationToken = default)
    {
        if (activeCharacterId is null || activeCharacterId.Value == Guid.Empty)
        {
            return;
        }

        if (activeMapId.HasValue && _workersByMapId.TryGetValue(activeMapId.Value, out MapZoneWorker? targetWorker))
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cmd = new DetachPlayerZoneCommand(
                activeCharacterId.Value,
                supersededEpoch,
                PlayerDetachReason.LeaseSuperseded,
                tcs);

            if (targetWorker.TryEnqueueCommand(cmd))
            {
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        foreach (MapZoneWorker worker in _workersByMapId.Values)
        {
            worker.TryEnqueueCommand(new DetachPlayerZoneCommand(
                activeCharacterId.Value,
                supersededEpoch,
                PlayerDetachReason.LeaseSuperseded));
        }
    }
}
