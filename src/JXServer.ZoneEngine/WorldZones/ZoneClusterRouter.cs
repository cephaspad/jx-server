using System.Collections.Frozen;
using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.GameAssets.Items;
using JXServer.GameAssets.LootTables;
using JXServer.GameAssets.NpcTemplates;
using JXServer.GameAssets.PlayerTemplates;
using JXServer.GameAssets.Skills;
using JXServer.GameAssets.WorldMaps;
using JXServer.ZoneEngine.Chats;
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
        ILogger<ZoneClusterRouter> logger,
        INpcTemplateCatalog? npcTemplateCatalog = null,
        ITrapScriptCatalog? trapScriptCatalog = null,
        ICharacterAppService? characterAppService = null,
        ISkillTemplateCatalog? skillTemplateCatalog = null,
        IMissileTemplateCatalog? missileTemplateCatalog = null,
        IPlayerTemplateCatalog? playerTemplateCatalog = null,
        IItemTemplateCatalog? itemTemplateCatalog = null,
        ILootTableCatalog? lootTableCatalog = null,
        ItemGenerator? itemGenerator = null)
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
            dict[desc.MapId] = new MapZoneWorker(
                desc,
                sessionLeaseAppService,
                cfg,
                timeProvider,
                logger,
                mapCatalog,
                npcTemplateCatalog,
                trapScriptCatalog,
                characterAppService,
                this,
                skillTemplateCatalog,
                missileTemplateCatalog,
                playerTemplateCatalog,
                itemTemplateCatalog,
                lootTableCatalog,
                itemGenerator);
        }

        _workersByMapId = dict.ToFrozenDictionary();
    }

    public bool IsMapLoaded(ushort mapId) => _workersByMapId.ContainsKey(mapId);

    public bool TryGetWorker(ushort mapId, out MapZoneWorker? worker) =>
        _workersByMapId.TryGetValue(mapId, out worker);

    public bool TryDispatchToZone(ushort mapId, IZoneCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _workersByMapId.TryGetValue(mapId, out MapZoneWorker? worker) &&
               worker.TryEnqueueCommand(command);
    }

    public bool TryTransferPlayerAcrossMaps(
        PlayerZoneState player,
        ushort targetMapId,
        int arrivalMpsX,
        int arrivalMpsY,
        byte arrivalFightMode)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!_workersByMapId.TryGetValue(targetMapId, out MapZoneWorker? targetWorker))
        {
            return false;
        }

        return targetWorker.TryEnqueueCommand(new AttachTransferredPlayerZoneCommand(
            player,
            arrivalMpsX,
            arrivalMpsY,
            arrivalFightMode));
    }

    public void RouteClusterChat(DeliverClusterChatZoneCommand chatCommand)
    {
        ArgumentNullException.ThrowIfNull(chatCommand);
        foreach (MapZoneWorker worker in _workersByMapId.Values)
        {
            if (worker.ActivePlayerCount > 0)
            {
                worker.TryEnqueueCommand(chatCommand);
            }
        }
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
