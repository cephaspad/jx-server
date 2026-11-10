namespace JXServer.Contracts.Characters;

/// <summary>
/// Data transfer object representing a single magic affix slot (0..5) on a character item.
/// </summary>
public sealed record class CharacterItemMagicAttributeDto(
    int SlotIndex,
    short AttributeId,
    int Value0,
    int Value1,
    int Value2,
    byte EnchaseLevel);
