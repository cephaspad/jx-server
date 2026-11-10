namespace JXServer.Primitives.Items;

/// <summary>
/// Item soulbind / lock state persisted in <c>CharacterItem</c>.
/// </summary>
public enum ItemBindState : byte
{
    Unbound = 0,
    BoundPermanent = 1,
    UnbindingPending = 2,
}
