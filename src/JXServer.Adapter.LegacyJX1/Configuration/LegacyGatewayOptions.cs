namespace JXServer.Adapter.LegacyJX1.Configuration;

/// <summary>
/// Configuration options for the Bishop Gateway (<c>5622</c>) and GameServer (<c>6666</c>) TCP listeners.
/// </summary>
public sealed class LegacyGatewayOptions
{
    public const string SectionName = "LegacyGateway";

    public string BindAddress { get; set; } = "0.0.0.0";

    public ushort BishopPort { get; set; } = 5622;

    public ushort GameServerPort { get; set; } = 6666;
}
