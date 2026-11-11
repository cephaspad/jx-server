using System;
using System.Collections.Generic;
using JXServer.Contracts.Characters;
using JXServer.GameAssets.Items;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;

namespace JXServer.ZoneEngine.Items;

/// <summary>
/// In-memory runtime representation of an item owned by a player or dropped in a zone (`FR-010`, `FR-012`).
/// </summary>
public sealed class PlayerItemSlot
{
    public uint RuntimeItemId { get; set; }

    public Guid DomainItemId { get; set; }

    public ItemTemplate Template { get; }

    public ItemGenreKind Genre { get; }

    public short DetailType { get; }

    public short ParticularType { get; }

    public byte Level { get; }

    public ElementSeries Series { get; }

    public ItemQualityKind Quality { get; }

    public uint RandomSeed { get; }

    public ushort GeneratorVersion { get; }

    public int Luck { get; }

    public ushort CurrentDurability { get; set; }

    public ushort MaxDurability { get; set; }

    public bool IsBroken { get; set; }

    public byte EnchaseLevel { get; set; }

    public ushort StackCount { get; set; }

    public ItemContainerPlace ContainerPlace { get; set; }

    public byte GridX { get; set; }

    public byte GridY { get; set; }

    public ItemBindState BindState { get; set; }

    public uint ExpireTimeUnixSeconds { get; set; }

    public string? CreatorName { get; set; }

    public ItemMagicAttributeSlot[] MagicAttributes { get; } = new ItemMagicAttributeSlot[ItemConsts.MaxMagicAffixSlots];

    /// <summary>
    /// Tracks whether the 3 hidden Five-Elements suffix slots (<c>1, 3, 5</c>) are currently activated by adjacent equipped gear.
    /// </summary>
    public bool[] ActiveHiddenSlots { get; } = new bool[3];

    public bool HasAnyMagicAffix
    {
        get
        {
            for (int i = 0; i < MagicAttributes.Length; i++)
            {
                if (MagicAttributes[i].IsPopulated)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public byte ColorId => Quality switch
    {
        ItemQualityKind.Golden or ItemQualityKind.Platina => 3,
        ItemQualityKind.Purple => 2,
        _ => HasAnyMagicAffix ? (byte)1 : (byte)0,
    };

    public PlayerItemSlot(
        uint runtimeItemId,
        Guid domainItemId,
        ItemTemplate template,
        ItemGenreKind genre,
        short detailType,
        short particularType,
        byte level,
        ElementSeries series,
        ItemQualityKind quality,
        uint randomSeed,
        ushort generatorVersion,
        int luck,
        ushort currentDurability,
        ushort maxDurability,
        ushort stackCount,
        ItemContainerPlace containerPlace,
        byte gridX,
        byte gridY,
        IReadOnlyList<ItemMagicAttributeSlot>? magicAttributes = null,
        ItemBindState bindState = ItemBindState.Unbound,
        uint expireTimeUnixSeconds = 0,
        string? creatorName = null,
        bool isBroken = false,
        byte enchaseLevel = 0)
    {
        ArgumentNullException.ThrowIfNull(template);

        RuntimeItemId = runtimeItemId;
        DomainItemId = domainItemId == Guid.Empty ? Guid.NewGuid() : domainItemId;
        Template = template;
        Genre = genre;
        DetailType = detailType;
        ParticularType = particularType;
        Level = level;
        Series = series;
        Quality = quality;
        RandomSeed = randomSeed;
        GeneratorVersion = generatorVersion == 0 ? ItemConsts.GeneratorVersion : generatorVersion;
        Luck = luck;
        CurrentDurability = currentDurability;
        MaxDurability = maxDurability;
        IsBroken = isBroken || (maxDurability > 0 && currentDurability == 0);
        EnchaseLevel = enchaseLevel;
        StackCount = Math.Max((ushort)1, stackCount);
        ContainerPlace = containerPlace;
        GridX = gridX;
        GridY = gridY;
        BindState = bindState;
        ExpireTimeUnixSeconds = expireTimeUnixSeconds;
        CreatorName = creatorName;

        for (int i = 0; i < ItemConsts.MaxMagicAffixSlots; i++)
        {
            MagicAttributes[i] = new ItemMagicAttributeSlot(i, 0, 0, 0, 0, 0);
        }

        if (magicAttributes is not null)
        {
            for (int i = 0; i < magicAttributes.Count; i++)
            {
                ItemMagicAttributeSlot slot = magicAttributes[i];
                if (slot.SlotIndex >= 0 && slot.SlotIndex < ItemConsts.MaxMagicAffixSlots)
                {
                    MagicAttributes[slot.SlotIndex] = slot;
                }
            }
        }
    }

    public static PlayerItemSlot FromGeneratedItem(
        uint runtimeItemId,
        GeneratedItemData generated,
        ItemContainerPlace containerPlace = ItemContainerPlace.Hand,
        byte gridX = 0,
        byte gridY = 0) =>
        new(
            runtimeItemId: runtimeItemId,
            domainItemId: Guid.NewGuid(),
            template: generated.Template,
            genre: generated.Genre,
            detailType: generated.DetailType,
            particularType: generated.ParticularType,
            level: generated.Level,
            series: generated.Series,
            quality: generated.Quality,
            randomSeed: generated.RandomSeed,
            generatorVersion: generated.GeneratorVersion,
            luck: generated.Luck,
            currentDurability: generated.CurrentDurability,
            maxDurability: generated.MaxDurability,
            stackCount: generated.StackCount,
            containerPlace: containerPlace,
            gridX: gridX,
            gridY: gridY,
            magicAttributes: generated.MagicAttributes);

    public CharacterItemDto ToCharacterItemDto()
    {
        var affixes = new List<CharacterItemMagicAttributeDto>(ItemConsts.MaxMagicAffixSlots);
        for (int i = 0; i < MagicAttributes.Length; i++)
        {
            ItemMagicAttributeSlot slot = MagicAttributes[i];
            if (slot.IsPopulated)
            {
                affixes.Add(new CharacterItemMagicAttributeDto(
                    slot.SlotIndex,
                    slot.AttributeId,
                    slot.Value0,
                    slot.Value1,
                    slot.Value2,
                    slot.EnchaseLevel));
            }
        }

        return new CharacterItemDto(
            Id: DomainItemId,
            Genre: Genre,
            DetailType: DetailType,
            ParticularType: ParticularType,
            Level: Level,
            Series: Series,
            Quality: Quality,
            RandomSeed: RandomSeed,
            GeneratorVersion: GeneratorVersion,
            Luck: Luck,
            CurrentDurability: CurrentDurability,
            MaxDurability: MaxDurability,
            StackCount: StackCount,
            ContainerPlace: ContainerPlace,
            GridX: GridX,
            GridY: GridY,
            MagicAttributes: affixes,
            BindState: BindState,
            ExpireTimeUnixSeconds: ExpireTimeUnixSeconds,
            CreatorName: CreatorName,
            IsBroken: IsBroken,
            EnchaseLevel: EnchaseLevel);
    }

    public void SetDurability(ushort currentDurability, ushort? maxDurability = null)
    {
        if (maxDurability.HasValue)
        {
            MaxDurability = maxDurability.Value;
        }

        CurrentDurability = currentDurability;
        if (MaxDurability > 0 && currentDurability == 0)
        {
            IsBroken = true;
        }
    }
}
