using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;

namespace JXServer.Domain.Characters;

/// <summary>
/// Persisted item owned by a character across inventory, equipment, or repository containers.
/// </summary>
public sealed class CharacterItem
{
    private readonly List<ItemMagicAttributeValue> _magicAttributes = [];

    private CharacterItem()
    {
        CreatorName = string.Empty;
    }

    public CharacterItem(
        Guid id,
        Guid characterId,
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
        ItemBindState bindState = ItemBindState.Unbound,
        uint expireTimeUnixSeconds = 0,
        string? creatorName = null,
        IEnumerable<ItemMagicAttributeValue>? magicAttributes = null)
    {
        Id = id;
        CharacterId = characterId;
        Genre = genre;
        DetailType = detailType;
        ParticularType = particularType;
        Level = level;
        Series = series;
        Quality = quality;
        RandomSeed = randomSeed;
        GeneratorVersion = generatorVersion;
        Luck = luck;
        CurrentDurability = currentDurability;
        MaxDurability = maxDurability;
        StackCount = stackCount;
        ContainerPlace = containerPlace;
        GridX = gridX;
        GridY = gridY;
        BindState = bindState;
        ExpireTimeUnixSeconds = expireTimeUnixSeconds;
        CreatorName = creatorName ?? string.Empty;

        if (magicAttributes is not null)
        {
            _magicAttributes.AddRange(magicAttributes);
        }
    }

    public Guid Id { get; private set; }

    public Guid CharacterId { get; private set; }

    public ItemGenreKind Genre { get; private set; }

    public short DetailType { get; private set; }

    public short ParticularType { get; private set; }

    public byte Level { get; private set; }

    public ElementSeries Series { get; private set; }

    public ItemQualityKind Quality { get; private set; }

    public uint RandomSeed { get; private set; }

    public ushort GeneratorVersion { get; private set; }

    public int Luck { get; private set; }

    public ushort CurrentDurability { get; private set; }

    public ushort MaxDurability { get; private set; }

    public ushort StackCount { get; private set; }

    public ItemContainerPlace ContainerPlace { get; private set; }

    public byte GridX { get; private set; }

    public byte GridY { get; private set; }

    public ItemBindState BindState { get; private set; }

    public uint ExpireTimeUnixSeconds { get; private set; }

    public string CreatorName { get; private set; }

    public IReadOnlyList<ItemMagicAttributeValue> MagicAttributes => _magicAttributes;
}
