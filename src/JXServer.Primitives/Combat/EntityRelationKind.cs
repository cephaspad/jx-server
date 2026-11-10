using System;

namespace JXServer.Primitives.Combat;

/// <summary>
/// Entity relationship flags (<c>NPC_RELATION</c> in <c>KNpcSet.h</c>).
/// </summary>
[Flags]
public enum EntityRelationKind : byte
{
    None = 1,
    Friend = 2,
    Enemy = 4,
    Self = 8,
    All = 15,
}
