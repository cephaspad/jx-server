namespace JXServer.Primitives.Combat;

/// <summary>
/// Deterministic 18-Hz monster AI state machine states.
/// </summary>
public enum MonsterAiStateKind : byte
{
    IdlePatrol = 0,
    Pursuit = 1,
    Attack = 2,
    LeashReturn = 3,
    DeadRespawnWait = 4,
}
