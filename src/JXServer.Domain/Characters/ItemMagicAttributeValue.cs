namespace JXServer.Domain.Characters;

/// <summary>
/// Represents a single magic affix slot (0..5) on a persisted character item.
/// </summary>
public sealed record class ItemMagicAttributeValue(
    int SlotIndex,
    short AttributeId,
    int Value0,
    int Value1,
    int Value2,
    byte EnchaseLevel);
