namespace JXServer.Primitives.Combat;

/// <summary>
/// Projectile movement archetype (<c>MISSLE_MOVEKIND</c> in <c>KMissle.cpp</c> / <c>skill-catalog.schema.json</c>).
/// </summary>
public enum MissileMoveKind : byte
{
    Stand = 0,
    Stationary = 1,
    Line64 = 2,
    Random = 3,
    Circle = 4,
    Spread = 5,
    Spiral = 6,
    Helix = 7,
    Follow = 8,
    Motion = 9,
    Parabola = 10,
    SingleLine = 11,
    RollBack = 12,
    Toss = 13,
}
