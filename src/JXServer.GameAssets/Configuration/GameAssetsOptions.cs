namespace JXServer.GameAssets.Configuration;

/// <summary>
/// Configuration options for loading static map and player template assets from <c>server1</c> and <c>gateway</c>.
/// </summary>
public sealed class GameAssetsOptions
{
    /// <summary>
    /// Configuration section name (<c>GameAssets</c>).
    /// </summary>
    public const string SectionName = "GameAssets";

    /// <summary>
    /// Gets or sets the path to the <c>server1</c> root directory (e.g., <c>d:\Projects\jx-linux-bin\server1</c>).
    /// </summary>
    public string ServerRootPath { get; set; } = @"d:\Projects\jx-linux-bin\server1";

    /// <summary>
    /// Gets or sets the path to the <c>gateway</c> root directory (e.g., <c>d:\Projects\jx-linux-bin\gateway</c>).
    /// </summary>
    public string GatewayRootPath { get; set; } = @"d:\Projects\jx-linux-bin\gateway";

    /// <summary>
    /// Gets or sets the default fallback starter village map ID (<c>53</c> = Ba Lăng Huyện).
    /// </summary>
    public ushort DefaultFallbackStarterVillageMapId { get; set; } = 53;

    /// <summary>
    /// Gets or sets a value indicating whether an unloaded starter village or character map should redirect
    /// to <see cref="DefaultFallbackStarterVillageMapId"/> (`FR-005`, `FR-007`).
    /// </summary>
    public bool FallbackUnloadedStarterVillage { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional subset of map IDs whose <c>Region_S.dat</c> obstacle grids are eagerly preloaded on startup.
    /// If empty, starter maps (<c>53</c>) are eagerly preloaded and remaining maps in <c>worldset.ini</c> load on demand.
    /// </summary>
    public ushort[] EagerObstacleLoadMapIds { get; set; } = [53];

    /// <summary>
    /// Gets or sets an optional list of map IDs to exclude from the active <c>worldset.ini</c> map set.
    /// </summary>
    public ushort[] ExcludedWorldSetMapIds { get; set; } = [];
}
