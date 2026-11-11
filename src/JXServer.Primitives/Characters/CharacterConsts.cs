namespace JXServer.Primitives.Characters;

/// <summary>
/// Validation and buffer limits for playable characters (roles).
/// </summary>
public static class CharacterConsts
{
    public const int MaxRolesPerAccount = 3;
    public const int MinNameByteLength = 6;
    public const int MaxNameByteLength = 30;
    public const int FixedRoleNameBufferSize = 32;
    public const int FixedDeleteRoleNameBufferSize = 28;
    public const int MaxSkillSlots = 80;
    public const long ExpHighDivisor = 2_000_000_000L;
    public const byte NoFactionId = 0xFF;
    public const int DefaultSpawnMapId = 53;
    public const int DefaultRevivalPointId = 19;
    public const ushort MinTaskId = 0;
    public const ushort MaxTaskId = ushort.MaxValue;
}
