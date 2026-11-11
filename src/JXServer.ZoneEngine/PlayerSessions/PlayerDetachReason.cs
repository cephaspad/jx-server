namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Reason why a player session was detached from a <c>MapZoneWorker</c>.
/// </summary>
public enum PlayerDetachReason : byte
{
    CleanLogout = 0,
    SocketClosed = 1,
    HeartbeatTimeout = 2,
    LeaseSuperseded = 3,
    MapTransferred = 4,
}
