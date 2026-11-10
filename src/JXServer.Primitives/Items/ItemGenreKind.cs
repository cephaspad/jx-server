namespace JXServer.Primitives.Items;

/// <summary>
/// Item genre classification (<c>item_genre</c> in <c>KItem.h</c> / <c>item-catalog.schema.json</c>).
/// </summary>
public enum ItemGenreKind : byte
{
    Equipment = 0,
    Medicine = 1,
    QuestKey = 2,
    TownPortal = 3,
    Material = 4,
    MagicScript = 6,
    PlatinaEquipment = 7,
}
