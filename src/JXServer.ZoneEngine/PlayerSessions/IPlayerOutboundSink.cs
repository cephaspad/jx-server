using System.Diagnostics.CodeAnalysis;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Composite outbound wire sink inheriting all 4 core domain sinks and providing
/// modular extensibility via <see cref="TryGetOutboundSink{TSink}"/>.
/// </summary>
public interface IPlayerOutboundSink
    : IWorldOutboundSink,
      ICombatOutboundSink,
      IInventoryOutboundSink,
      IChatOutboundSink
{
    /// <summary>
    /// Resolves a domain-specific outbound sink (<see cref="IWorldOutboundSink"/>, <see cref="ICombatOutboundSink"/>,
    /// <see cref="IInventoryOutboundSink"/>, <see cref="IChatOutboundSink"/>, or a downstream module sink).
    /// Returns <see langword="false"/> when this session/double does not subscribe to <typeparamref name="TSink"/> (<c>FR-011</c>, <c>SC-005</c>).
    /// </summary>
    bool TryGetOutboundSink<TSink>([NotNullWhen(true)] out TSink? sink)
        where TSink : class
    {
        sink = this as TSink;
        return sink is not null;
    }
}
