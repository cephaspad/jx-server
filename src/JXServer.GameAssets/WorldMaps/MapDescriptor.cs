namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Immutable descriptor for a world map loaded from <c>settings/maplist.ini</c> and <c>maps/*.wor</c>.
/// </summary>
public sealed record MapDescriptor(
    ushort MapId,
    string MapNameUnicode,
    string RawSubPath,
    int RegionBeginX,
    int RegionBeginY,
    int RegionWidth,
    int RegionHeight);
