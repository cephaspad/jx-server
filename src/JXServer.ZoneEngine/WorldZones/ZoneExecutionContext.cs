using System.Diagnostics.CodeAnalysis;
using JXServer.Contracts.Characters;
using JXServer.Contracts.SessionLeases;
using JXServer.GameAssets.WorldMaps;
using JXServer.Primitives.WorldZones;
using JXServer.ZoneEngine.Combat;
using JXServer.ZoneEngine.Configuration;
using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.NpcEntities;
using JXServer.ZoneEngine.PlayerSessions;
using JXServer.ZoneEngine.SpatialGrid;
using Microsoft.Extensions.Logging;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Single-threaded per-zone state and helper façade passed into <see cref="IZoneCommandHandler{TCommand}"/>,
/// <see cref="IZoneTickSystem"/>, and <see cref="IPlayerFeatureSlot"/> callbacks (<c>FR-001</c>–<c>FR-004</c>).
/// Allocated once per <see cref="MapZoneWorker"/> shard; never shared across threads.
/// </summary>
public sealed class ZoneExecutionContext
{
    private readonly MapZoneWorker _worker;
    private readonly Dictionary<Type, object> _zoneFeatures = [];

    internal MapZoneWorker Worker => _worker;

    public MapDescriptor MapDescriptor => _worker.MapDescriptor;

    public ushort MapId => MapDescriptor.MapId;

    public string MapNameUnicode => MapDescriptor.MapNameUnicode;

    /// <summary>Current 1-based 18-Hz frame counter for this map zone.</summary>
    public long CurrentFrame
    {
        get => _worker.CurrentFrameTick;
        internal set => _worker.CurrentFrameTick = value;
    }

    public ZoneEngineOptions Options => _worker.Options;

    public TimeProvider TimeProvider => _worker.TimeProvider;

    public ILogger Logger => _worker.Logger;

    public Random Random => _worker.Random;

    public ISessionLeaseAppService SessionLeaseAppService => _worker.SessionLeaseAppService;

    public ZoneAssetServices Assets => _worker.Assets;

    public SpatialRegionGrid RegionGrid => _worker.RegionGrid;

    public MissileSimulationTickSystem MissileSystem => _worker.MissileSystem;

    public MonsterAiTickSystem MonsterAiSystem => _worker.MonsterAiSystem;

    public GroundObjectTickSystem GroundObjectSystem => _worker.GroundObjectSystem;

    public IReadOnlyDictionary<Guid, PlayerZoneState> PlayersByCharacterId => _worker.PlayersByCharacterId;

    public IReadOnlyDictionary<uint, PlayerZoneState> PlayersByNpcId => _worker.PlayersByNpcId;

    public IReadOnlyDictionary<uint, NpcZoneState> StaticNpcs => _worker.StaticNpcs;

    public ZoneExecutionContext(MapZoneWorker worker)
    {
        ArgumentNullException.ThrowIfNull(worker);
        _worker = worker;
    }

    public bool TryGetPlayer(Guid characterId, [NotNullWhen(true)] out PlayerZoneState? player) =>
        _worker.TryGetPlayer(characterId, out player);

    public bool TryGetPlayerByNpcId(uint runtimeNpcId, [NotNullWhen(true)] out PlayerZoneState? player) =>
        _worker.TryGetPlayerByNpcId(runtimeNpcId, out player);

    public bool TryGetStaticNpc(uint npcId, [NotNullWhen(true)] out NpcZoneState? npc) =>
        _worker.TryGetStaticNpc(npcId, out npc);

    /// <summary>
    /// Populates <paramref name="destination"/> (capacity &gt;= 9) with active <c>3x3</c> AOI regions around
    /// <c>(regionX, regionY)</c> with zero heap allocation (<c>SC-003</c>).
    /// </summary>
    public int CollectAoiRegions(int regionX, int regionY, Span<SpatialRegionSlot> destination) =>
        RegionGrid.GetSurroundingRegions(regionX, regionY, destination);

    /// <summary>
    /// Lazily creates or retrieves per-zone module state without modifying <see cref="MapZoneWorker"/> (<c>SC-001</c>).
    /// </summary>
    public TZoneFeature GetOrCreateZoneFeature<TZoneFeature>(Func<ZoneExecutionContext, TZoneFeature> factory)
        where TZoneFeature : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (_zoneFeatures.TryGetValue(typeof(TZoneFeature), out object? existing))
        {
            return (TZoneFeature)existing;
        }

        TZoneFeature created = factory(this);
        _zoneFeatures[typeof(TZoneFeature)] = created;
        return created;
    }

    public UpdateCharacterWorldSnapshotDto BuildCharacterWorldSnapshot(PlayerZoneState player) =>
        _worker.BuildCharacterWorldSnapshot(player);

    public ValueTask DetachPlayerAsync(
        Guid characterId,
        PlayerDetachReason reason,
        bool persistSnapshot = true,
        bool sendDisconnect = false,
        CancellationToken cancellationToken = default) =>
        _worker.DetachPlayerAsync(characterId, reason, persistSnapshot, sendDisconnect, cancellationToken);
}
