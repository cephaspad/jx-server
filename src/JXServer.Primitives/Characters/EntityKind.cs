namespace JXServer.Primitives.Characters;

/// <summary>
/// Runtime entity classification (<c>m_btKind</c>).
/// </summary>
public enum EntityKind : byte
{
    Monster = 0,
    Player = 1,
    Partner = 2,
    DialogNpc = 3
}
