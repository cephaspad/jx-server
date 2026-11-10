using System.Collections.Generic;
using JXServer.GameAssets.NpcTemplates;
using MemoryPack;

namespace JXServer.GameAssets.WorldMaps;

/// <summary>
/// Immutable descriptor for a world map loaded from <c>JXData Standard v1</c> (<c>data/maps/*.map.json</c> or <c>settings/maplist.ini</c>).
/// </summary>
[MemoryPackable]
public sealed partial record MapDescriptor(
    ushort MapId,
    string MapNameUnicode,
    string RawSubPath,
    int RegionBeginX,
    int RegionBeginY,
    int RegionWidth,
    int RegionHeight,
    string Code = "",
    bool AllowPkChange = true,
    bool AllowTownPortal = true)
{
    /// <summary>
    /// Gets the static NPC and monster placements loaded for this map from <c>Region_S.dat</c> (<c>NPC_SECTION</c>)
    /// and <c>script/global/autoexec.lua</c> (`FR-004`).
    /// </summary>
    public IReadOnlyList<StaticNpcSpawnTemplate> RegionNpcSpawns { get; init; } = [];
}
