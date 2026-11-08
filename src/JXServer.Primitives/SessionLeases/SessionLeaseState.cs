namespace JXServer.Primitives.SessionLeases;

/// <summary>
/// Authoritative lifecycle state of an account's session lease.
/// </summary>
public enum SessionLeaseState : byte
{
    Offline = 0,
    GatewayAuthenticated = 1,
    HandoffIssued = 2,
    WorldAttaching = 3,
    InWorld = 4
}
