using System.Collections.Frozen;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Concrete builder collecting and validating command handlers, tick systems, and feature slots (<c>FR-003</c>).
/// </summary>
internal sealed class ZoneExtensionBuilder : IZoneExtensionBuilder
{
    private readonly Dictionary<Type, ZoneCommandRoute> _commandRoutes = [];
    private readonly List<Func<ZoneExecutionContext, IZoneTickSystem>> _tickSystemFactories = [];
    private readonly HashSet<string> _statelessTickSystemNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> _featureSlotsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Type, object> _featureSlotsByType = [];

    public IZoneExtensionBuilder AddCommandHandler<TCommand>(IZoneCommandHandler<TCommand> handler)
        where TCommand : class, IZoneCommand
    {
        ArgumentNullException.ThrowIfNull(handler);
        Type commandType = typeof(TCommand);
        if (_commandRoutes.ContainsKey(commandType))
        {
            throw new InvalidOperationException($"Command handler for '{commandType.Name}' is already registered.");
        }

        _commandRoutes[commandType] = new ZoneCommandRoute(
            (command, context, ct) => handler.HandleAsync((TCommand)command, context, ct));
        return this;
    }

    public IZoneExtensionBuilder AddCommandHandler<TCommand>(Func<ZoneExecutionContext, IZoneCommandHandler<TCommand>> handlerFactory)
        where TCommand : class, IZoneCommand
    {
        ArgumentNullException.ThrowIfNull(handlerFactory);
        Type commandType = typeof(TCommand);
        if (_commandRoutes.ContainsKey(commandType))
        {
            throw new InvalidOperationException($"Command handler for '{commandType.Name}' is already registered.");
        }

        _commandRoutes[commandType] = new ZoneCommandRoute(
            (command, context, ct) => handlerFactory(context).HandleAsync((TCommand)command, context, ct));
        return this;
    }

    public IZoneExtensionBuilder AddTickSystem(IZoneTickSystem tickSystem)
    {
        ArgumentNullException.ThrowIfNull(tickSystem);
        if (!_statelessTickSystemNames.Add(tickSystem.Name))
        {
            throw new InvalidOperationException($"Tick system '{tickSystem.Name}' is already registered.");
        }

        _tickSystemFactories.Add(_ => tickSystem);
        return this;
    }

    public IZoneExtensionBuilder AddTickSystem(Func<ZoneExecutionContext, IZoneTickSystem> tickSystemFactory)
    {
        ArgumentNullException.ThrowIfNull(tickSystemFactory);
        _tickSystemFactories.Add(tickSystemFactory);
        return this;
    }

    public IZoneExtensionBuilder RegisterFeatureSlot<TSlot>(PlayerFeatureSlotKey<TSlot> key)
        where TSlot : class, IPlayerFeatureSlot
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_featureSlotsByName.ContainsKey(key.Name))
        {
            throw new InvalidOperationException($"Feature slot with name '{key.Name}' is already registered.");
        }

        Type slotType = typeof(TSlot);
        if (_featureSlotsByType.ContainsKey(slotType))
        {
            throw new InvalidOperationException($"Feature slot of type '{slotType.Name}' is already registered.");
        }

        _featureSlotsByName[key.Name] = key;
        _featureSlotsByType[slotType] = key;
        return this;
    }

    public ZoneExtensionRegistry Build() =>
        new(
            _commandRoutes.ToFrozenDictionary(),
            _tickSystemFactories.ToArray(),
            _featureSlotsByName.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
            _featureSlotsByType.ToFrozenDictionary());
}
