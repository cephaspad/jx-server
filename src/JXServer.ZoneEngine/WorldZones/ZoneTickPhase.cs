namespace JXServer.ZoneEngine.WorldZones;

/// <summary>
/// Deterministic execution phases within a single 18-Hz frame (<c>FR-002</c>, <c>FR-005</c>).
/// Spaced by 100 so future feature modules can either attach to an existing phase or insert custom
/// intermediate phases while preserving exact legacy simulation order.
/// </summary>
public enum ZoneTickPhase : ushort
{
    StatusEffects = 100,
    MonsterAi = 200,
    Missiles = 300,
    GroundObjects = 400,
    MovementAndPosture = 500,
    SessionHeartbeats = 600,
}
