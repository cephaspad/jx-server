namespace JXServer.Primitives.Items;

/// <summary>
/// Inventory container placement (<c>ITEM_POSITION</c> / <c>pos_*</c> in <c>KItemList.h</c>).
/// </summary>
public enum ItemContainerPlace : byte
{
    None = 0,
    Hand = 1,
    Equip = 2,
    EquipRoom = 3,
    RepositoryRoom = 4,
    Trade = 5,
    Immediacy = 7,
}
