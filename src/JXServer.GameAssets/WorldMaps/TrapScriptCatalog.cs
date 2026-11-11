using System.Collections.Frozen;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Logging;
using JXServer.GameAssets.Snapshots;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Singleton catalog providing portal and town gate trap script bindings
/// loaded via <c>JXData Standard v1</c> Dual-Tier snapshots (<c>data/maps/*.map.json</c> or <c>artifacts/jxdata.pack</c>).
/// </summary>
public sealed class TrapScriptCatalog : ITrapScriptCatalog
{
    private readonly FrozenDictionary<uint, PortalTrapBindingTemplate> _bindings;

    public int Count => _bindings.Count;

    public TrapScriptCatalog(IOptions<GameAssetsOptions> options, ILogger<TrapScriptCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var bindings = new Dictionary<uint, PortalTrapBindingTemplate>();

        if (GameAssetsSnapshotLoader.TryLoadBundle(options.Value, out GameAssetsSnapshotBundle? bundle, out _) &&
            bundle is not null &&
            bundle.PortalTrapBindings.Count > 0)
        {
            foreach (PortalTrapBindingTemplate b in bundle.PortalTrapBindings)
            {
                bindings[b.TrapId] = b;
            }
        }

        _bindings = bindings.ToFrozenDictionary();
        GameAssetsLogMessages.LogTrapScriptsLoaded(logger, _bindings.Count, options.Value.DataDirectory);
    }

    public bool TryGetTrapBinding(uint trapId, out PortalTrapBindingTemplate? binding) =>
        _bindings.TryGetValue(trapId, out binding);
}
