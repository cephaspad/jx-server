using System.Collections.Frozen;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Immutable frozen command router, ordered tick-system factory list, and validated feature slot registry (<c>FR-001</c>–<c>FR-004</c>).
/// </summary>
public sealed class ZoneExtensionRegistry
{
    private readonly FrozenDictionary<Type, ZoneCommandRoute> _commandRoutes;
    private readonly IReadOnlyList<Func<ZoneExecutionContext, IZoneTickSystem>> _tickSystemFactories;
    private readonly FrozenDictionary<string, object> _featureSlotsByName;
    private readonly FrozenDictionary<Type, object> _featureSlotsByType;

    public static ZoneExtensionRegistry Empty { get; } = new ZoneExtensionBuilder().Build();

    public int RegisteredCommandCount => _commandRoutes.Count;

    public int RegisteredTickSystemFactoryCount => _tickSystemFactories.Count;

    public int RegisteredFeatureSlotCount => _featureSlotsByName.Count;

    internal ZoneExtensionRegistry(
        FrozenDictionary<Type, ZoneCommandRoute> commandRoutes,
        IReadOnlyList<Func<ZoneExecutionContext, IZoneTickSystem>> tickSystemFactories,
        FrozenDictionary<string, object> featureSlotsByName,
        FrozenDictionary<Type, object> featureSlotsByType)
    {
        _commandRoutes = commandRoutes;
        _tickSystemFactories = tickSystemFactories;
        _featureSlotsByName = featureSlotsByName;
        _featureSlotsByType = featureSlotsByType;
    }

    public bool HasCommandHandler(Type commandType) => _commandRoutes.ContainsKey(commandType);

    public bool TryGetCommandRoute(Type commandType, out ZoneCommandRoute route) =>
        _commandRoutes.TryGetValue(commandType, out route);

    public IReadOnlyList<IZoneTickSystem> CreateTickSystems(ZoneExecutionContext context)
    {
        var systems = new List<IZoneTickSystem>(_tickSystemFactories.Count);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var factory in _tickSystemFactories)
        {
            IZoneTickSystem system = factory(context);
            if (!seenNames.Add(system.Name))
            {
                throw new InvalidOperationException($"Duplicate tick system name '{system.Name}' instantiated on Map {context.MapId}.");
            }
            systems.Add(system);
        }

        systems.Sort((a, b) =>
        {
            int phaseComparison = a.Phase.CompareTo(b.Phase);
            if (phaseComparison != 0) return phaseComparison;
            return a.SubOrder.CompareTo(b.SubOrder);
        });

        return systems;
    }

    public static ZoneExtensionRegistry CreateDefault(IEnumerable<IZoneModule>? additionalModules = null)
    {
        var modules = new List<IZoneModule>
        {
            new CoreZoneModule(),
            new Chats.ChatZoneModule()
        };
        if (additionalModules is not null)
        {
            modules.AddRange(additionalModules);
        }
        return Create(modules);
    }

    public static ZoneExtensionRegistry Create(IEnumerable<IZoneModule> modules, bool includeCoreModule = false)
    {
        var builder = new ZoneExtensionBuilder();
        var moduleList = modules as IReadOnlyCollection<IZoneModule> ?? modules.ToList();
        if (includeCoreModule && !moduleList.Any(static m => m is CoreZoneModule))
        {
            new CoreZoneModule().Configure(builder);
        }
        foreach (IZoneModule module in moduleList)
        {
            module.Configure(builder);
        }

        return builder.Build();
    }
}
