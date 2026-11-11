namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Marker interface for commands routed into a single-threaded 18-Hz <c>MapZoneWorker</c> channel.
/// </summary>
public interface IZoneCommand
{
    /// <summary>
    /// Optional character identifier associated with this command for structured fault-isolation logging (<c>FR-006</c>).
    /// Player-scoped command records override or expose <c>CharacterId</c> here; system/account commands return <see langword="null"/>.
    /// </summary>
    Guid? FaultIsolationCharacterId => null;
}
