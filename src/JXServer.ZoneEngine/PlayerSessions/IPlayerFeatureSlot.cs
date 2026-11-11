using JXServer.ZoneEngine.WorldZones;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Modular feature state slot attached to a player with deterministic detachment lifecycle hooks (<c>FR-004</c>).
/// </summary>
public interface IPlayerFeatureSlot
{
    /// <summary>
    /// Invoked deterministically on the single-threaded zone loop whenever the owning player detaches from the zone
    /// for any <see cref="PlayerDetachReason"/> (<c>CleanLogout</c>, <c>SocketClosed</c>, <c>HeartbeatTimeout</c>,
    /// <c>LeaseSuperseded</c>, or <c>MapTransferred</c>) (<c>FR-004</c>).
    /// </summary>
    void OnPlayerDetached(
        PlayerZoneState player,
        ZoneExecutionContext context,
        PlayerDetachReason reason);
}
