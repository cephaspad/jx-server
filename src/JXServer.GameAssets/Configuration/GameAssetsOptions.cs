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
    /// Gets or sets the canonical UTF-8 JSON game data directory (<c>JXData Standard v1</c> Tier 1).
    /// </summary>
    public string DataDirectory { get; set; } = @"d:\Projects\jx-server\data";

    /// <summary>
    /// Gets or sets the compiled <c>MemoryPack</c> snapshot directory (<c>JXData Standard v1</c> Tier 2).
    /// </summary>
    public string ArtifactsDirectory { get; set; } = @"d:\Projects\jx-server\artifacts";

    /// <summary>
    /// Gets or sets a value indicating whether to prefer loading <c>MemoryPack</c> binary snapshots from <see cref="ArtifactsDirectory"/> when present.
    /// </summary>
    public bool PreferBinarySnapshots { get; set; } = true;

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
    public ushort[] EagerObstacleLoadMapIds { get; set; } = [53, 54, 70, 74, 199];

    /// <summary>
    /// Gets or sets an optional list of map IDs to exclude from the active <c>worldset.ini</c> map set.
    /// </summary>
    public ushort[] ExcludedWorldSetMapIds { get; set; } = [];
}
