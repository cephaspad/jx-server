namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Strongly-typed array-indexed key for <see cref="IPlayerFeatureSlot"/> attachments on <see cref="PlayerZoneState"/> (<c>FR-004</c>).
/// </summary>
public sealed class PlayerFeatureSlotKey<TSlot>
    where TSlot : class, IPlayerFeatureSlot
{
    public string Name { get; }

    internal int Index { get; }

    public PlayerFeatureSlotKey(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Index = PlayerFeatureSlotIndexAllocator.NextIndex();
    }
}
