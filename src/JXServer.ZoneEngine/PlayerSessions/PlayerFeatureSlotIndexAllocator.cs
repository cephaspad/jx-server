namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Thread-safe monotonic index allocator for typed player feature slot keys.
/// </summary>
internal static class PlayerFeatureSlotIndexAllocator
{
    private static int _nextIndex;

    public static int NextIndex() => Interlocked.Increment(ref _nextIndex) - 1;

    public static int CurrentCount => Volatile.Read(ref _nextIndex);
}
