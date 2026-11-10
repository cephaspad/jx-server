namespace JXServer.Primitives.Items;

/// <summary>
/// Represents a single rolled magic attribute slot (<c>0..5</c>) on a generated or active item (`FR-010`).
/// Even slots (<c>0, 2, 4</c>) are explicit always-active affixes; odd slots (<c>1, 3, 5</c>) are Five-Elements hidden affixes.
/// </summary>
public readonly record struct ItemMagicAttributeSlot(
    int SlotIndex,
    short AttributeId,
    int Value0,
    int Value1 = 0,
    int Value2 = 0,
    byte EnchaseLevel = 0)
{
    public bool IsPopulated => AttributeId > 0;
}
