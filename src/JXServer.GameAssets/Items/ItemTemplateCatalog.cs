using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using JXServer.GameAssets.Configuration;
using JXServer.GameAssets.Logging;
using JXServer.GameAssets.Snapshots;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JXServer.GameAssets.Items;

/// <summary>
/// Singleton read-only catalog of item templates, magic affixes (`m_wVersion = 4`), and ground objects (`FR-001`, `FR-009`).
/// </summary>
public sealed class ItemTemplateCatalog : IItemTemplateCatalog
{
    private readonly FrozenDictionary<string, ItemTemplate> _itemsByCode;
    private readonly FrozenDictionary<(ItemQualityKind Quality, int RowId), ItemTemplate> _itemsByQualityAndRowId;
    private readonly FrozenDictionary<(ItemGenreKind Genre, int Detail, int Particular, byte Level, ItemQualityKind Quality), ItemTemplate> _itemsByTuple;
    private readonly FrozenDictionary<(string Pool, int RowId), MagicAffixTemplate> _affixesByPoolAndId;
    private readonly IReadOnlyList<MagicAffixTemplate> _blueAffixes;
    private readonly IReadOnlyList<MagicAffixTemplate> _allAffixes;
    private readonly FrozenDictionary<int, GroundObjectTemplate> _groundObjectsById;
    private readonly ConcurrentDictionary<(bool IsSuffix, int Detail, int Particular, ElementSeries Series, int MaxLevel), IReadOnlyList<MagicAffixTemplate>> _candidateCache = new();

    public int ItemCount => _itemsByCode.Count;

    public int MagicAffixCount => _allAffixes.Count;

    public IReadOnlyCollection<ItemTemplate> AllItems => _itemsByCode.Values;

    public IReadOnlyCollection<MagicAffixTemplate> AllMagicAffixes => _allAffixes;

    public IReadOnlyCollection<GroundObjectTemplate> AllGroundObjects => _groundObjectsById.Values;

    public ItemTemplateCatalog(IOptions<GameAssetsOptions> options, ILogger<ItemTemplateCatalog> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        IReadOnlyList<ItemTemplate> items;
        IReadOnlyList<MagicAffixTemplate> affixes;
        IReadOnlyList<GroundObjectTemplate> groundObjs;
        string sourceTier;
        bool loadedFromBundle = false;

        if (GameAssetsSnapshotLoader.TryLoadBundle(options.Value, out GameAssetsSnapshotBundle? bundle, out sourceTier) &&
            bundle is not null &&
            bundle.Items.Count > 0)
        {
            items = bundle.Items;
            affixes = bundle.MagicAffixes;
            groundObjs = bundle.GroundObjects;
            loadedFromBundle = true;
        }
        else
        {
            items = CreateDefaultItems();
            affixes = CreateDefaultMagicAffixes();
            groundObjs = CreateDefaultGroundObjects();
            sourceTier = "BuiltInDefaults";
        }

        var byCode = new Dictionary<string, ItemTemplate>(StringComparer.OrdinalIgnoreCase);
        var byQualityAndRowId = new Dictionary<(ItemQualityKind, int), ItemTemplate>();
        var byTuple = new Dictionary<(ItemGenreKind, int, int, byte, ItemQualityKind), ItemTemplate>();
        foreach (ItemTemplate item in items)
        {
            byCode[item.Code] = item;
            if (item.Quality != ItemQualityKind.Normal || !byQualityAndRowId.ContainsKey((item.Quality, item.Id)))
            {
                byQualityAndRowId[(item.Quality, item.Id)] = item;
            }

            byTuple.TryAdd((item.Genre, item.DetailType, item.ParticularType, item.Level, item.Quality), item);
        }

        if (!loadedFromBundle || items.Count < 50)
        {
            foreach (ItemTemplate fallback in CreateDefaultItems())
            {
                byCode.TryAdd(fallback.Code, fallback);
                byQualityAndRowId.TryAdd((fallback.Quality, fallback.Id), fallback);
                byTuple.TryAdd((fallback.Genre, fallback.DetailType, fallback.ParticularType, fallback.Level, fallback.Quality), fallback);
            }
        }

        var affByPoolAndId = new Dictionary<(string Pool, int RowId), MagicAffixTemplate>();
        foreach (MagicAffixTemplate ma in affixes)
        {
            string poolKey = NormalizePoolKey(ma.AffixPool);
            affByPoolAndId[(poolKey, ma.Id)] = ma;
        }

        if (!loadedFromBundle || affixes.Count == 0)
        {
            foreach (MagicAffixTemplate fallbackAff in CreateDefaultMagicAffixes())
            {
                affByPoolAndId.TryAdd((NormalizePoolKey(fallbackAff.AffixPool), fallbackAff.Id), fallbackAff);
            }
        }

        var goById = new Dictionary<int, GroundObjectTemplate>();
        foreach (GroundObjectTemplate go in groundObjs)
        {
            goById[go.Id] = go;
        }

        if (!loadedFromBundle || groundObjs.Count == 0)
        {
            foreach (GroundObjectTemplate fallbackGo in CreateDefaultGroundObjects())
            {
                goById.TryAdd(fallbackGo.Id, fallbackGo);
            }
        }

        _itemsByCode = byCode.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _itemsByQualityAndRowId = byQualityAndRowId.ToFrozenDictionary();
        _itemsByTuple = byTuple.ToFrozenDictionary();
        _affixesByPoolAndId = affByPoolAndId.ToFrozenDictionary();
        _allAffixes = _affixesByPoolAndId.Values
            .OrderBy(a => a.AffixPool, StringComparer.Ordinal)
            .ThenBy(a => a.Id)
            .ToArray();
        _blueAffixes = _allAffixes
            .Where(a => string.Equals(NormalizePoolKey(a.AffixPool), "bluerandom", StringComparison.Ordinal))
            .OrderBy(a => a.Id)
            .ToArray();
        _groundObjectsById = goById.ToFrozenDictionary();

        GameAssetsLogMessages.LogItemCatalogLoaded(logger, _itemsByCode.Count, _allAffixes.Count, sourceTier);
    }

    public bool TryGetItemByCode(string code, out ItemTemplate? item)
    {
        if (_itemsByCode.TryGetValue(code, out item))
        {
            return true;
        }

        if (string.Equals(code, "kim-sang-duoc", StringComparison.OrdinalIgnoreCase))
        {
            return _itemsByCode.TryGetValue("kim-sang-duoc-tieu", out item);
        }

        if (string.Equals(code, "ngung-than-dan", StringComparison.OrdinalIgnoreCase))
        {
            return _itemsByCode.TryGetValue("ngung-than-dan-tieu", out item);
        }

        item = null;
        return false;
    }

    public bool TryGetItemByRowId(ItemQualityKind quality, int rowId, out ItemTemplate? item) =>
        _itemsByQualityAndRowId.TryGetValue((quality, rowId), out item);

    public bool TryGetItem(
        ItemGenreKind genre,
        int detailType,
        int particularType,
        byte level,
        out ItemTemplate? item,
        ItemQualityKind quality = ItemQualityKind.Normal)
    {
        if (_itemsByTuple.TryGetValue((genre, detailType, particularType, level, quality), out item))
        {
            return true;
        }

        // Fallback to Level 1 or Level 0 of the same (Genre, DetailType, ParticularType) if exact level is not in catalog
        return _itemsByTuple.TryGetValue((genre, detailType, particularType, 1, quality), out item)
            || _itemsByTuple.TryGetValue((genre, detailType, particularType, 0, quality), out item);
    }

    public IReadOnlyList<MagicAffixTemplate> GetCandidateMagicAffixes(
        bool isSuffix,
        int equipDetailType,
        int weaponParticularType,
        ElementSeries series,
        int maxLevel)
    {
        (int normDetail, int normParticular) = NormalizeEquipBucket(equipDetailType, weaponParticularType);
        if (normDetail < 0)
        {
            return [];
        }

        int clampedLevel = Math.Clamp(maxLevel, 0, 30);
        var key = (isSuffix, normDetail, normParticular, series, clampedLevel);
        if (_candidateCache.TryGetValue(key, out IReadOnlyList<MagicAffixTemplate>? cached))
        {
            return cached;
        }

        var candidates = new List<MagicAffixTemplate>();
        foreach (MagicAffixTemplate ma in _blueAffixes)
        {
            if (ma.IsGoldenOnly || ma.IsSuffix != isSuffix || ma.Level > clampedLevel)
            {
                continue;
            }

            if (isSuffix && ma.ElementSeries != series)
            {
                continue;
            }

            int equipWeight = ma.WeightsByEquipPart.GetWeightForEquip(normDetail, normParticular);
            int seriesWeight = ma.WeightsBySeries.GetWeight(series);
            if (equipWeight > 0 && seriesWeight > 0 && ma.DropWeight > 0)
            {
                candidates.Add(ma);
            }
        }

        IReadOnlyList<MagicAffixTemplate> frozen = candidates.ToArray();
        _candidateCache.TryAdd(key, frozen);
        return frozen;
    }

    private static (int Detail, int Particular) NormalizeEquipBucket(int detailType, int particularType) =>
        detailType switch
        {
            0 => (0, particularType is >= 0 and <= 5 ? particularType : 0),
            1 => (1, particularType is >= 0 and <= 2 ? particularType : 0),
            >= 2 and <= 10 => (detailType, 0),
            _ => (-1, 0),
        };

    public bool TryGetMagicAffixById(int affixRowId, out MagicAffixTemplate? affix, string affixPool = "blueRandom") =>
        _affixesByPoolAndId.TryGetValue((NormalizePoolKey(affixPool), affixRowId), out affix);

    public bool TryGetGroundObject(int objDataId, out GroundObjectTemplate? objTemplate) =>
        _groundObjectsById.TryGetValue(objDataId, out objTemplate);

    private static string NormalizePoolKey(string? affixPool) =>
        string.IsNullOrWhiteSpace(affixPool) ? "bluerandom" : affixPool.Trim().ToLowerInvariant();

    public static IReadOnlyList<ItemTemplate> CreateDefaultItems() =>
    [
        new ItemTemplate(
            Id: 1,
            Code: "equip-melee-sword-lvl1",
            Name: "Thanh Phong Kiếm",
            Genre: ItemGenreKind.Equipment,
            Quality: ItemQualityKind.Normal,
            DetailType: 0,
            ParticularType: 0,
            Level: 1,
            ElementSeries: ElementSeries.Metal,
            GridWidthCells: 1,
            GridHeightCells: 3,
            ObjDataId: 10,
            AppearanceResId: 1,
            PriceGold: 100,
            MaxStackCount: 1,
            BaseAttributes:
            [
                new ItemBaseAttributeTemplate(1, "damage_physics_min", 6, 10),
                new ItemBaseAttributeTemplate(2, "damage_physics_max", 12, 18),
                new ItemBaseAttributeTemplate(3, "durability_max", 45, 65),
            ],
            Requirements:
            [
                new ItemRequirementTemplate(0, "strength", 20),
                new ItemRequirementTemplate(4, "level", 1),
            ]),
        new ItemTemplate(
            Id: 2,
            Code: "equip-armor-cloth-lvl1",
            Name: "Thô Bố Y",
            Genre: ItemGenreKind.Equipment,
            Quality: ItemQualityKind.Normal,
            DetailType: 2,
            ParticularType: 0,
            Level: 1,
            ElementSeries: ElementSeries.Wood,
            GridWidthCells: 2,
            GridHeightCells: 3,
            ObjDataId: 11,
            AppearanceResId: 2,
            PriceGold: 80,
            MaxStackCount: 1,
            BaseAttributes:
            [
                new ItemBaseAttributeTemplate(10, "defense_v", 15, 25),
                new ItemBaseAttributeTemplate(3, "durability_max", 50, 70),
            ],
            Requirements:
            [
                new ItemRequirementTemplate(4, "level", 1),
            ]),
        new ItemTemplate(
            Id: 3,
            Code: "equip-helm-cap-lvl1",
            Name: "Bố Quán",
            Genre: ItemGenreKind.Equipment,
            Quality: ItemQualityKind.Normal,
            DetailType: 7,
            ParticularType: 0,
            Level: 1,
            ElementSeries: ElementSeries.Water,
            GridWidthCells: 2,
            GridHeightCells: 2,
            ObjDataId: 12,
            AppearanceResId: 3,
            PriceGold: 60,
            MaxStackCount: 1,
            BaseAttributes:
            [
                new ItemBaseAttributeTemplate(10, "defense_v", 10, 18),
                new ItemBaseAttributeTemplate(3, "durability_max", 40, 60),
            ],
            Requirements:
            [
                new ItemRequirementTemplate(4, "level", 1),
            ]),
        new ItemTemplate(
            Id: 4,
            Code: "equip-ring-jade-lvl1",
            Name: "Hoàng Ngọc Giới Chỉ",
            Genre: ItemGenreKind.Equipment,
            Quality: ItemQualityKind.Normal,
            DetailType: 3,
            ParticularType: 0,
            Level: 1,
            ElementSeries: ElementSeries.Fire,
            GridWidthCells: 1,
            GridHeightCells: 1,
            ObjDataId: 13,
            AppearanceResId: 4,
            PriceGold: 120,
            MaxStackCount: 1,
            BaseAttributes:
            [
                new ItemBaseAttributeTemplate(3, "durability_max", 35, 50),
            ],
            Requirements:
            [
                new ItemRequirementTemplate(4, "level", 1),
            ]),
        new ItemTemplate(
            Id: 5,
            Code: "equip-amulet-pendant-lvl1",
            Name: "Ngân Hạng Liên",
            Genre: ItemGenreKind.Equipment,
            Quality: ItemQualityKind.Normal,
            DetailType: 4,
            ParticularType: 0,
            Level: 1,
            ElementSeries: ElementSeries.Earth,
            GridWidthCells: 2,
            GridHeightCells: 1,
            ObjDataId: 14,
            AppearanceResId: 5,
            PriceGold: 120,
            MaxStackCount: 1,
            BaseAttributes:
            [
                new ItemBaseAttributeTemplate(3, "durability_max", 35, 50),
            ],
            Requirements:
            [
                new ItemRequirementTemplate(4, "level", 1),
            ]),
        new ItemTemplate(
            Id: 6,
            Code: "equip-boot-shoes-lvl1",
            Name: "Thảo Hài",
            Genre: ItemGenreKind.Equipment,
            Quality: ItemQualityKind.Normal,
            DetailType: 5,
            ParticularType: 0,
            Level: 1,
            ElementSeries: ElementSeries.Metal,
            GridWidthCells: 2,
            GridHeightCells: 2,
            ObjDataId: 15,
            AppearanceResId: 6,
            PriceGold: 60,
            MaxStackCount: 1,
            BaseAttributes:
            [
                new ItemBaseAttributeTemplate(10, "defense_v", 8, 15),
                new ItemBaseAttributeTemplate(3, "durability_max", 40, 60),
            ],
            Requirements:
            [
                new ItemRequirementTemplate(4, "level", 1),
            ]),
        new ItemTemplate(
            Id: 100,
            Code: "kim-sang-duoc",
            Name: "Kim Sáng Dược (Tiểu)",
            Genre: ItemGenreKind.Medicine,
            Quality: ItemQualityKind.Normal,
            DetailType: 0,
            ParticularType: 0,
            Level: 1,
            ElementSeries: ElementSeries.Metal,
            GridWidthCells: 1,
            GridHeightCells: 1,
            ObjDataId: 20,
            AppearanceResId: 0,
            PriceGold: 20,
            MaxStackCount: 50,
            BaseAttributes:
            [
                new ItemBaseAttributeTemplate(100, "restore_life_v", 100, 100, DurationFrames: 90),
            ],
            Requirements: []),
        new ItemTemplate(
            Id: 101,
            Code: "ngung-than-dan",
            Name: "Ngưng Thần Đan (Tiểu)",
            Genre: ItemGenreKind.Medicine,
            Quality: ItemQualityKind.Normal,
            DetailType: 1,
            ParticularType: 0,
            Level: 1,
            ElementSeries: ElementSeries.Water,
            GridWidthCells: 1,
            GridHeightCells: 1,
            ObjDataId: 21,
            AppearanceResId: 0,
            PriceGold: 20,
            MaxStackCount: 50,
            BaseAttributes:
            [
                new ItemBaseAttributeTemplate(101, "restore_mana_v", 100, 100, DurationFrames: 90),
            ],
            Requirements: []),
        new ItemTemplate(
            Id: 200,
            Code: "tho-dia-phu",
            Name: "Thổ Địa Phù",
            Genre: ItemGenreKind.TownPortal,
            Quality: ItemQualityKind.Normal,
            DetailType: 0,
            ParticularType: 0,
            Level: 1,
            ElementSeries: ElementSeries.Earth,
            GridWidthCells: 1,
            GridHeightCells: 1,
            ObjDataId: 30,
            AppearanceResId: 0,
            PriceGold: 50,
            MaxStackCount: 50,
            BaseAttributes: [],
            Requirements: []),
    ];

    public static IReadOnlyList<MagicAffixTemplate> CreateDefaultMagicAffixes()
    {
        var allEquip = new EquipPartWeightsTemplate(100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100);
        var allSeries = new SeriesWeightsTemplate(100, 100, 100, 100, 100);
        var list = new List<MagicAffixTemplate>
        {
            // Explicit prefix affixes (Slots 0, 2, 4)
            new(1, "lifemax-v", 85, "lifemax_v", "Sinh lực tối đa +{0} điểm", IsSuffix: false, IsGoldenOnly: false, ElementSeries.Metal, 1, 100, allEquip, allSeries, 15, 60),
            new(2, "manamax-v", 89, "manamax_v", "Nội lực tối đa +{0} điểm", IsSuffix: false, IsGoldenOnly: false, ElementSeries.Water, 1, 100, allEquip, allSeries, 15, 60),
            new(3, "staminamax-v", 93, "staminamax_v", "Thể lực tối đa +{0} điểm", IsSuffix: false, IsGoldenOnly: false, ElementSeries.Wood, 1, 100, allEquip, allSeries, 15, 60),
            new(4, "addphysicsdamage-v", 121, "addphysicsdamage_v", "Sát thương vật lý +{0} điểm", IsSuffix: false, IsGoldenOnly: false, ElementSeries.Metal, 1, 100, allEquip, allSeries, 5, 25),
            new(5, "attackrating-v", 166, "attackrating_v", "Độ chính xác +{0} điểm", IsSuffix: false, IsGoldenOnly: false, ElementSeries.Fire, 1, 100, allEquip, allSeries, 20, 80),
            new(6, "defense-v", 104, "defense_v", "Né tránh +{0} điểm", IsSuffix: false, IsGoldenOnly: false, ElementSeries.Earth, 1, 100, allEquip, allSeries, 15, 60),

            // Hidden Five-Elements suffix affixes (Slots 1, 3, 5) for each series
            new(101, "metal-physics-res-p", 101, "physicsres_p", "Phòng thủ vật lý +{0}%", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Metal, 1, 100, allEquip, allSeries, 3, 15),
            new(102, "metal-strength-v", 97, "strength_v", "Sức mạnh +{0} điểm", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Metal, 1, 100, allEquip, allSeries, 3, 12),
            new(103, "metal-lifereplenish-v", 88, "lifereplenish_v", "Phục hồi sinh lực +{0}", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Metal, 1, 100, allEquip, allSeries, 2, 8),

            new(201, "wood-poison-res-p", 102, "poisonres_p", "Kháng độc +{0}%", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Wood, 1, 100, allEquip, allSeries, 3, 15),
            new(202, "wood-dexterity-v", 98, "dexterity_v", "Thân pháp +{0} điểm", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Wood, 1, 100, allEquip, allSeries, 3, 12),
            new(203, "wood-manareplenish-v", 92, "manareplenish_v", "Phục hồi nội lực +{0}", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Wood, 1, 100, allEquip, allSeries, 2, 8),

            new(301, "water-cold-res-p", 103, "coldres_p", "Kháng băng +{0}%", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Water, 1, 100, allEquip, allSeries, 3, 15),
            new(302, "water-vitality-v", 99, "vitality_v", "Sinh khí +{0} điểm", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Water, 1, 100, allEquip, allSeries, 3, 12),
            new(303, "water-fastwalkrun-p", 111, "fastwalkrun_p", "Tốc độ di chuyển +{0}%", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Water, 1, 100, allEquip, allSeries, 5, 20),

            new(401, "fire-fire-res-p", 105, "fireres_p", "Kháng hỏa +{0}%", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Fire, 1, 100, allEquip, allSeries, 3, 15),
            new(402, "fire-energy-v", 100, "energy_v", "Nội công +{0} điểm", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Fire, 1, 100, allEquip, allSeries, 3, 12),
            new(403, "fire-fasthitrecover-v", 113, "fasthitrecover_v", "Thời gian phục hồi +{0}", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Fire, 1, 100, allEquip, allSeries, 10, 30),

            new(501, "earth-light-res-p", 106, "lightingres_p", "Kháng lôi +{0}%", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Earth, 1, 100, allEquip, allSeries, 3, 15),
            new(502, "earth-lucky-v", 135, "lucky_v", "May mắn +{0}%", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Earth, 1, 100, allEquip, allSeries, 1, 8),
            new(503, "earth-allres-p", 107, "allres_p", "Kháng tất cả +{0}%", IsSuffix: true, IsGoldenOnly: false, ElementSeries.Earth, 1, 100, allEquip, allSeries, 2, 8),
        };
        return list;
    }

    public static IReadOnlyList<GroundObjectTemplate> CreateDefaultGroundObjects() =>
    [
        new GroundObjectTemplate(1, "money-pile-small", "Ngân Lượng", GroundObjectKind.Money, 1080),
        new GroundObjectTemplate(10, "dropped-sword", "Vũ Khí", GroundObjectKind.Item, 1080),
        new GroundObjectTemplate(11, "dropped-armor", "Y Phục", GroundObjectKind.Item, 1080),
        new GroundObjectTemplate(20, "dropped-potion", "Dược Phẩm", GroundObjectKind.Item, 1080),
        new GroundObjectTemplate(30, "dropped-scroll", "Thổ Địa Phù", GroundObjectKind.Item, 1080),
    ];
}
