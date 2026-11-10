namespace JXServer.Primitives.Combat;

/// <summary>
/// Base character attributes allocated via <c>0x57</c> (<c>c2s_playeraddbaseattribute</c>).
/// </summary>
public enum BaseAttributeKind : byte
{
    Strength = 0,
    Dexterity = 1,
    Vitality = 2,
    Energy = 3,
}
