using JXServer.Primitives.SessionLeases;

namespace JXServer.Application.Configuration;

/// <summary>
/// Configuration options for Control-Plane application services (account auto-provisioning, lease TTLs, and advertised GameServer endpoint).
/// </summary>
public sealed class ApplicationOptions
{
    /// <summary>
    /// Configuration section name (<c>Application</c>).
    /// </summary>
    public const string SectionName = "Application";

    /// <summary>
    /// Gets or sets a value indicating whether new accounts are automatically created on first login (`FR-003`).
    /// </summary>
    public bool AutoProvisionAccounts { get; set; } = true;

    /// <summary>
    /// Gets or sets the advertised GameServer IPv4 address sent in <c>s2c_notifyplayerlogin</c> (<c>0x34</c>).
    /// </summary>
    public string AdvertisedGameServerHost { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the advertised GameServer TCP port sent in <c>s2c_notifyplayerlogin</c> (<c>0x34</c>).
    /// </summary>
    public ushort AdvertisedGameServerPort { get; set; } = 6666;

    /// <summary>
    /// Gets or sets the single-use handoff ticket expiration time in seconds (`30` seconds default).
    /// </summary>
    public int HandoffTicketTtlSeconds { get; set; } = SessionLeaseConsts.DefaultHandoffTicketTtlSeconds;

    /// <summary>
    /// Gets or sets the session heartbeat timeout in seconds (`15` seconds default).
    /// </summary>
    public int HeartbeatTimeoutSeconds { get; set; } = SessionLeaseConsts.DefaultHeartbeatTimeoutSeconds;
}
