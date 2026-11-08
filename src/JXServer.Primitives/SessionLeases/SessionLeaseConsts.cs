namespace JXServer.Primitives.SessionLeases;

/// <summary>
/// Timing and TTL defaults for session leases and heartbeat checks.
/// </summary>
public static class SessionLeaseConsts
{
    public const int DefaultHandoffTicketTtlSeconds = 30;
    public const int DefaultHeartbeatIntervalSeconds = 5;
    public const int DefaultHeartbeatTimeoutSeconds = 15;
}
