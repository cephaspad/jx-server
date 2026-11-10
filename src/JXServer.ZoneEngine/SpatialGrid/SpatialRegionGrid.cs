using JXServer.ZoneEngine.NpcEntities;
using JXServer.ZoneEngine.PlayerSessions;

namespace JXServer.ZoneEngine.SpatialGrid;

/// <summary>
/// Manages the 2D region grid for a single map zone and computes <c>3x3</c> Area-of-Interest (AOI)
/// neighborhoods and entering/leaving region set differences (`FR-001`, `FR-002`).
/// </summary>
public sealed class SpatialRegionGrid
{
    private readonly Dictionary<(int RegionX, int RegionY), SpatialRegionSlot> _regions = new();

    public IEnumerable<SpatialRegionSlot> ActiveRegions => _regions.Values;

    public SpatialRegionSlot GetOrCreateRegion(int regionX, int regionY)
    {
        if (!_regions.TryGetValue((regionX, regionY), out SpatialRegionSlot? slot))
        {
            slot = new SpatialRegionSlot(regionX, regionY);
            _regions[(regionX, regionY)] = slot;
        }

        return slot;
    }

    public bool TryGetRegion(int regionX, int regionY, out SpatialRegionSlot? slot) =>
        _regions.TryGetValue((regionX, regionY), out slot);

    public void AddNpc(NpcZoneState npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        SpatialRegionSlot slot = GetOrCreateRegion(npc.RegionX, npc.RegionY);
        if (!slot.Npcs.Contains(npc))
        {
            slot.Npcs.Add(npc);
        }
    }

    public void RemoveNpc(NpcZoneState npc, int regionX, int regionY)
    {
        if (_regions.TryGetValue((regionX, regionY), out SpatialRegionSlot? slot))
        {
            slot.Npcs.Remove(npc);
        }
    }

    public void AddPlayer(PlayerZoneState player, int regionX, int regionY)
    {
        ArgumentNullException.ThrowIfNull(player);
        SpatialRegionSlot slot = GetOrCreateRegion(regionX, regionY);
        if (!slot.Players.Contains(player))
        {
            slot.Players.Add(player);
        }
    }

    public void RemovePlayer(PlayerZoneState player, int regionX, int regionY)
    {
        if (_regions.TryGetValue((regionX, regionY), out SpatialRegionSlot? slot))
        {
            slot.Players.Remove(player);
        }
    }

    /// <summary>
    /// Populates <paramref name="buffer"/> with up to 9 existing <see cref="SpatialRegionSlot"/> instances
    /// in the <c>3x3</c> neighborhood centered at <c>(centerRegionX, centerRegionY)</c>.
    /// </summary>
    public int GetSurroundingRegions(int centerRegionX, int centerRegionY, Span<SpatialRegionSlot> buffer)
    {
        int count = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (_regions.TryGetValue((centerRegionX + dx, centerRegionY + dy), out SpatialRegionSlot? slot) &&
                    count < buffer.Length)
                {
                    buffer[count++] = slot;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Computes the existing <see cref="SpatialRegionSlot"/> instances in the new <c>3x3</c> neighborhood
    /// around <c>(newRegionX, newRegionY)</c> that were NOT in the old <c>3x3</c> neighborhood around <c>(oldRegionX, oldRegionY)</c>.
    /// </summary>
    public int ComputeEnteringRegions(
        int oldRegionX,
        int oldRegionY,
        int newRegionX,
        int newRegionY,
        Span<SpatialRegionSlot> buffer)
    {
        int count = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int rx = newRegionX + dx;
                int ry = newRegionY + dy;
                if (Math.Abs(rx - oldRegionX) <= 1 && Math.Abs(ry - oldRegionY) <= 1)
                {
                    continue;
                }

                if (_regions.TryGetValue((rx, ry), out SpatialRegionSlot? slot) &&
                    count < buffer.Length)
                {
                    buffer[count++] = slot;
                }
            }
        }

        return count;
    }
}
