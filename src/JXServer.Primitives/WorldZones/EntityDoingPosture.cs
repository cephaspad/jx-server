namespace JXServer.Primitives.WorldZones;

/// <summary>
/// Canonical action/posture states (<c>NPCCMD</c> / <c>m_Doing</c>) for players and NPCs on the wire and in 18-Hz zone simulation.
/// </summary>
public enum EntityDoingPosture : byte
{
    Stand = 1,
    Walk = 2,
    Run = 3,
    Sit = 4,
    Jump = 5,
    Attack = 6,
    Skill = 7,
    Hurt = 8,
    Death = 11,
    Revive = 12,
}
