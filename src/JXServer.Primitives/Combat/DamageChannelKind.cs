namespace JXServer.Primitives.Combat;

/// <summary>
/// Six combat damage channels (<c>DAMAGE_TYPE</c> in <c>KNpc.cpp</c>).
/// </summary>
public enum DamageChannelKind : byte
{
    Physics = 0,
    Cold = 1,
    Fire = 2,
    Light = 3,
    Poison = 4,
    Magic = 5,
}
