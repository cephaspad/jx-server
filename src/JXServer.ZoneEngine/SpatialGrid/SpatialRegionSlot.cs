using JXServer.ZoneEngine.Items;
using JXServer.ZoneEngine.NpcEntities;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.SpatialGrid;

/// <summary>
/// Represents a single <c>512 * 1024</c> Mps (<c>16 * 32</c> cell) spatial region slot inside a <c>MapZoneWorker</c>,
/// holding the active players, static/dynamic NPCs, and ground objects residing in <c>(RegionX, RegionY)</c> (`FR-001`, `FR-011`).
/// </summary>
public sealed class SpatialRegionSlot
{
    public int RegionX { get; }

    public int RegionY { get; }

    public List<PlayerZoneState> Players { get; } = [];

    public List<NpcZoneState> Npcs { get; } = [];

    public List<GroundObjectSlot> GroundObjects { get; } = [];

    public SpatialRegionSlot(int regionX, int regionY)
    {
        RegionX = regionX;
        RegionY = regionY;
    }
}
