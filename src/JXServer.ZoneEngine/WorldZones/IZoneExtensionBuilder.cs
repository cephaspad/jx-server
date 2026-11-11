using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Fluent registration contract for command handlers, tick systems, and feature slot keys (<c>FR-001</c>–<c>FR-004</c>).
/// </summary>
public interface IZoneExtensionBuilder
{
    /// <summary>
    /// Registers a singleton/stateless command handler instance for <typeparamref name="TCommand"/>.
    /// Throws <see cref="InvalidOperationException"/> if <typeparamref name="TCommand"/> already has a registered handler (<c>FR-003</c>).
    /// </summary>
    IZoneExtensionBuilder AddCommandHandler<TCommand>(IZoneCommandHandler<TCommand> handler)
        where TCommand : class, IZoneCommand;

    /// <summary>
    /// Registers a command handler resolved per-zone (or singleton via factory) for <typeparamref name="TCommand"/>.
    /// Throws <see cref="InvalidOperationException"/> if <typeparamref name="TCommand"/> already has a registered handler (<c>FR-003</c>).
    /// </summary>
    IZoneExtensionBuilder AddCommandHandler<TCommand>(Func<ZoneExecutionContext, IZoneCommandHandler<TCommand>> handlerFactory)
        where TCommand : class, IZoneCommand;

    /// <summary>
    /// Registers a stateless <see cref="IZoneTickSystem"/> shared across map shards.
    /// Throws <see cref="InvalidOperationException"/> if another tick system with the same <see cref="IZoneTickSystem.Name"/> is already registered (<c>FR-003</c>).
    /// </summary>
    IZoneExtensionBuilder AddTickSystem(IZoneTickSystem tickSystem);

    /// <summary>
    /// Registers a per-zone <see cref="IZoneTickSystem"/> factory invoked once per <see cref="MapZoneWorker"/> construction.
    /// </summary>
    IZoneExtensionBuilder AddTickSystem(Func<ZoneExecutionContext, IZoneTickSystem> tickSystemFactory);

    /// <summary>
    /// Registers a <see cref="PlayerFeatureSlotKey{TSlot}"/> owned by a gameplay module (<c>FR-003</c>, <c>FR-004</c>).
    /// Throws <see cref="InvalidOperationException"/> if a slot with the same <see cref="PlayerFeatureSlotKey{TSlot}.Name"/> or <typeparamref name="TSlot"/> type is already registered.
    /// </summary>
    IZoneExtensionBuilder RegisterFeatureSlot<TSlot>(PlayerFeatureSlotKey<TSlot> key)
        where TSlot : class, IPlayerFeatureSlot;
}
