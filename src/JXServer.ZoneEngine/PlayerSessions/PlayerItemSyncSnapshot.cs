using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using JXServer.ZoneEngine.Items;

namespace JXServer.ZoneEngine.PlayerSessions;

/// <summary>
/// Read-only snapshot of a player inventory item used for <c>0x5F</c> (<c>s2c_syncitem</c>, <c>80</c> bytes) outbound synchronization (`FR-012`).
/// </summary>
public readonly record struct PlayerItemSyncSnapshot(
    uint RuntimeItemId,
    ItemGenreKind Genre,
    ItemQualityKind Quality,
    short DetailType,
    ushort ParticularType,
    ElementSeries Series,
    byte Level,
    byte Luck,
    short MagicLevel0,
    short MagicLevel1,
    short MagicLevel2,
    short MagicLevel3,
    short MagicLevel4,
    short MagicLevel5,
    ushort Version,
    uint RandomSeed,
    ushort StackCount,
    short MaxDurability,
    short CurrentDurability,
    ItemContainerPlace ContainerPlace,
    byte GridY,
    byte GridX,
    int WidthCells,
    int HeightCells)
{
    public static PlayerItemSyncSnapshot CreateFromSlot(PlayerItemSlot slot) =>
        new(
            RuntimeItemId: slot.RuntimeItemId,
            Genre: slot.Genre,
            Quality: slot.Quality,
            DetailType: slot.DetailType,
            ParticularType: (ushort)slot.ParticularType,
            Series: slot.Series,
            Level: slot.Level,
            Luck: (byte)Math.Clamp(slot.Luck, 0, byte.MaxValue),
            MagicLevel0: slot.MagicAttributes[0].EnchaseLevel > 0 ? slot.MagicAttributes[0].EnchaseLevel : slot.MagicAttributes[0].AttributeId,
            MagicLevel1: slot.MagicAttributes[1].EnchaseLevel > 0 ? slot.MagicAttributes[1].EnchaseLevel : slot.MagicAttributes[1].AttributeId,
            MagicLevel2: slot.MagicAttributes[2].EnchaseLevel > 0 ? slot.MagicAttributes[2].EnchaseLevel : slot.MagicAttributes[2].AttributeId,
            MagicLevel3: slot.MagicAttributes[3].EnchaseLevel > 0 ? slot.MagicAttributes[3].EnchaseLevel : slot.MagicAttributes[3].AttributeId,
            MagicLevel4: slot.MagicAttributes[4].EnchaseLevel > 0 ? slot.MagicAttributes[4].EnchaseLevel : slot.MagicAttributes[4].AttributeId,
            MagicLevel5: slot.MagicAttributes[5].EnchaseLevel > 0 ? slot.MagicAttributes[5].EnchaseLevel : slot.MagicAttributes[5].AttributeId,
            Version: slot.GeneratorVersion,
            RandomSeed: slot.RandomSeed,
            StackCount: slot.StackCount,
            MaxDurability: (short)Math.Clamp((int)slot.MaxDurability, 0, short.MaxValue),
            CurrentDurability: (short)Math.Clamp((int)slot.CurrentDurability, 0, short.MaxValue),
            ContainerPlace: slot.ContainerPlace,
            GridY: slot.GridY,
            GridX: slot.GridX,
            WidthCells: slot.Template.GridWidthCells,
            HeightCells: slot.Template.GridHeightCells);
}
