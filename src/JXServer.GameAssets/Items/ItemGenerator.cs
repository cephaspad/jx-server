using System;
using System.Collections.Generic;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;

namespace JXServer.GameAssets.Items;

/// <summary>
/// Deterministic C# port of <c>KItemGenerator::Gen_Equipment</c>, <c>Gen_Medicine</c>, <c>Gen_TownPortal</c>,
/// and <c>Gen_MagicAttrib</c> (<c>m_wVersion = 4</c>) (`FR-010`).
/// Rolls White equipment, consumables, and 6-affix Blue equipment (up to 3 explicit slots <c>0, 2, 4</c>
/// and 3 Five-Elements hidden slots <c>1, 3, 5</c>) weighted by <c>WeightsByEquipPart</c>, <c>WeightsBySeries</c>,
/// item <c>Level</c>, <c>RandomSeed</c>, and killer <c>Luck</c> without duplicate <c>AttributeId</c>s.
/// </summary>
public sealed class ItemGenerator
{
    private readonly IItemTemplateCatalog _itemCatalog;

    public ItemGenerator(IItemTemplateCatalog itemCatalog)
    {
        ArgumentNullException.ThrowIfNull(itemCatalog);
        _itemCatalog = itemCatalog;
    }

    public GeneratedItemData GenerateFromItemCode(
        string itemCode,
        ElementSeries? seriesOverride = null,
        byte? levelOverride = null,
        int luck = 0,
        uint? randomSeedOverride = null,
        ushort stackCount = 1,
        bool rollBlueAffixes = false)
    {
        if (!_itemCatalog.TryGetItemByCode(itemCode, out ItemTemplate? tpl) || tpl is null)
        {
            return GenerateMedicine(0, 1, 1, stackCount);
        }

        byte level = levelOverride ?? Math.Max((byte)1, tpl.Level);
        ElementSeries series = seriesOverride ?? tpl.ElementSeries;
        uint seed = randomSeedOverride ?? NextDeterministicSeed(tpl.Id, level, (int)series, luck);

        if (tpl.Genre == ItemGenreKind.Equipment)
        {
            int[]? magicLevels = rollBlueAffixes ? [10, 10, 10, 10, 10, 10] : null;
            return GenerateEquipment(
                tpl.DetailType,
                tpl.ParticularType,
                series,
                level,
                magicLevels,
                luck,
                seed,
                tpl);
        }

        if (tpl.Genre == ItemGenreKind.TownPortal)
        {
            return GenerateTownPortal(tpl.ParticularType, stackCount, tpl);
        }

        return GenerateMedicine(tpl.DetailType, tpl.ParticularType, level, stackCount, tpl);
    }

    public GeneratedItemData GenerateEquipment(
        int detailType,
        int particularType,
        ElementSeries series,
        byte level,
        ReadOnlySpan<int> magicLevelThresholds,
        int luck = 0,
        uint? randomSeedOverride = null,
        ItemTemplate? resolvedTemplate = null)
    {
        byte clampedLevel = (byte)Math.Clamp((int)level, 1, 10);
        ItemTemplate tpl = resolvedTemplate ?? ResolveEquipmentTemplate(detailType, particularType, clampedLevel, series);
        uint seed = randomSeedOverride ?? NextDeterministicSeed(tpl.Id, clampedLevel, (int)series, luck);

        ushort maxDurability = ComputeBaseDurability(tpl, seed);
        IReadOnlyList<ItemMagicAttributeSlot> affixes = RollBlueMagicAffixes(
            detailType,
            particularType,
            series,
            clampedLevel,
            magicLevelThresholds,
            luck,
            seed);

        return new GeneratedItemData(
            Template: tpl,
            Genre: ItemGenreKind.Equipment,
            DetailType: (short)detailType,
            ParticularType: (short)particularType,
            Level: clampedLevel,
            Series: series,
            Quality: tpl.Quality,
            RandomSeed: seed,
            GeneratorVersion: ItemConsts.GeneratorVersion,
            Luck: luck,
            CurrentDurability: maxDurability,
            MaxDurability: maxDurability,
            StackCount: 1,
            MagicAttributes: affixes);
    }

    public GeneratedItemData GenerateMedicine(
        int detailType,
        int particularType,
        byte level = 1,
        ushort stackCount = 1,
        ItemTemplate? resolvedTemplate = null)
    {
        byte clampedLevel = (byte)Math.Clamp((int)level, 1, 10);
        if (resolvedTemplate is null &&
            !_itemCatalog.TryGetItem(ItemGenreKind.Medicine, detailType, particularType, clampedLevel, out resolvedTemplate))
        {
            _itemCatalog.TryGetItemByCode("kim-sang-duoc", out resolvedTemplate);
        }

        ItemTemplate tpl = resolvedTemplate ?? CreateFallbackMedicineTemplate(detailType, particularType, clampedLevel);
        ushort clampedStack = (ushort)Math.Clamp((int)stackCount, 1, Math.Max(1, tpl.MaxStackCount));

        return new GeneratedItemData(
            Template: tpl,
            Genre: ItemGenreKind.Medicine,
            DetailType: (short)detailType,
            ParticularType: (short)particularType,
            Level: clampedLevel,
            Series: ElementSeries.Metal,
            Quality: ItemQualityKind.Normal,
            RandomSeed: NextDeterministicSeed(tpl.Id, clampedLevel, detailType, particularType),
            GeneratorVersion: ItemConsts.GeneratorVersion,
            Luck: 0,
            CurrentDurability: 1,
            MaxDurability: 1,
            StackCount: clampedStack,
            MagicAttributes: CreateEmptyMagicSlots());
    }

    public GeneratedItemData GenerateTownPortal(
        int particularType = 1,
        ushort stackCount = 1,
        ItemTemplate? resolvedTemplate = null)
    {
        if (resolvedTemplate is null &&
            !_itemCatalog.TryGetItem(ItemGenreKind.TownPortal, 0, particularType, 1, out resolvedTemplate))
        {
            _itemCatalog.TryGetItemByCode("tho-dia-phu", out resolvedTemplate);
        }

        ItemTemplate tpl = resolvedTemplate ?? new ItemTemplate(
            Id: 30001,
            Code: "tho-dia-phu",
            Name: "Thổ Địa Phù",
            Genre: ItemGenreKind.TownPortal,
            Quality: ItemQualityKind.Normal,
            DetailType: 0,
            ParticularType: particularType,
            Level: 1,
            ElementSeries: ElementSeries.Metal,
            GridWidthCells: 1,
            GridHeightCells: 1,
            ObjDataId: 3,
            AppearanceResId: 1,
            PriceGold: 100,
            MaxStackCount: 1,
            BaseAttributes: [],
            Requirements: []);

        return new GeneratedItemData(
            Template: tpl,
            Genre: ItemGenreKind.TownPortal,
            DetailType: 0,
            ParticularType: (short)particularType,
            Level: 1,
            Series: ElementSeries.Metal,
            Quality: ItemQualityKind.Normal,
            RandomSeed: NextDeterministicSeed(tpl.Id, 1, 0, particularType),
            GeneratorVersion: ItemConsts.GeneratorVersion,
            Luck: 0,
            CurrentDurability: 1,
            MaxDurability: 1,
            StackCount: (ushort)Math.Max((ushort)1, stackCount),
            MagicAttributes: CreateEmptyMagicSlots());
    }

    /// <summary>
    /// Rolls up to 6 magic affix slots (<c>0..5</c>) matching <c>KItemGenerator::Gen_MagicAttrib</c>:
    /// even indices (<c>0, 2, 4</c>) are explicit prefixes (<c>IsSuffix == false</c>);
    /// odd indices (<c>1, 3, 5</c>) are hidden Five-Elements suffixes (<c>IsSuffix == true</c>, matching item <c>series</c>).
    /// Never emits duplicate <c>AttributeId</c>s across the 6 slots.
    /// </summary>
    public IReadOnlyList<ItemMagicAttributeSlot> RollBlueMagicAffixes(
        int detailType,
        int particularType,
        ElementSeries series,
        byte itemLevel,
        ReadOnlySpan<int> magicLevelThresholds,
        int luck,
        uint randomSeed)
    {
        var slots = new ItemMagicAttributeSlot[ItemConsts.MaxMagicAffixSlots];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new ItemMagicAttributeSlot(i, 0, 0, 0, 0, 0);
        }

        if (magicLevelThresholds.IsEmpty)
        {
            return slots;
        }

        var rng = new Random((int)(randomSeed ^ (uint)(luck * 2654435761L)));
        var usedAttributeIds = new HashSet<int>();
        int luckBonusLevel = Math.Clamp(luck / 15, 0, 5);

        for (int slotIndex = 0; slotIndex < ItemConsts.MaxMagicAffixSlots && slotIndex < magicLevelThresholds.Length; slotIndex++)
        {
            int threshold = magicLevelThresholds[slotIndex];
            if (threshold <= 0)
            {
                continue;
            }

            bool isSuffix = (slotIndex & 1) == 1;
            int maxAffixLevel = Math.Clamp(Math.Max((int)itemLevel, threshold) + luckBonusLevel, 1, 10);

            IReadOnlyList<MagicAffixTemplate> candidates = _itemCatalog.GetCandidateMagicAffixes(
                isSuffix,
                detailType,
                particularType,
                series,
                maxAffixLevel);

            if (candidates.Count == 0)
            {
                continue;
            }

            long totalWeight = 0;
            for (int cIdx = 0; cIdx < candidates.Count; cIdx++)
            {
                MagicAffixTemplate cand = candidates[cIdx];
                if (usedAttributeIds.Contains(cand.AttributeId))
                {
                    continue;
                }

                long weight = CalculateAffixWeight(cand, detailType, particularType, series, maxAffixLevel, luck);
                totalWeight += weight;
            }

            if (totalWeight <= 0)
            {
                continue;
            }

            long roll = rng.NextInt64(totalWeight);
            long accum = 0;
            MagicAffixTemplate? chosen = null;

            for (int cIdx = 0; cIdx < candidates.Count; cIdx++)
            {
                MagicAffixTemplate cand = candidates[cIdx];
                if (usedAttributeIds.Contains(cand.AttributeId))
                {
                    continue;
                }

                accum += CalculateAffixWeight(cand, detailType, particularType, series, maxAffixLevel, luck);
                if (roll < accum)
                {
                    chosen = cand;
                    break;
                }
            }

            if (chosen is null)
            {
                continue;
            }

            usedAttributeIds.Add(chosen.AttributeId);
            int val0 = RollRangeWithLuck(rng, chosen.Param0Min, chosen.Param0Max, luck);
            int val1 = RollRangeWithLuck(rng, chosen.Param1Min, chosen.Param1Max, luck);
            int val2 = RollRangeWithLuck(rng, chosen.Param2Min, chosen.Param2Max, luck);
            byte enchaseLvl = (byte)Math.Clamp(chosen.Level, 1, 10);

            slots[slotIndex] = new ItemMagicAttributeSlot(
                SlotIndex: slotIndex,
                AttributeId: (short)chosen.AttributeId,
                Value0: val0,
                Value1: val1,
                Value2: val2,
                EnchaseLevel: enchaseLvl);
        }

        return slots;
    }

    private static long CalculateAffixWeight(
        MagicAffixTemplate cand,
        int detailType,
        int particularType,
        ElementSeries series,
        int maxAffixLevel,
        int luck)
    {
        int equipWeight = Math.Max(1, cand.WeightsByEquipPart.GetWeightForEquip(detailType, particularType));
        int seriesWeight = Math.Max(1, cand.WeightsBySeries.GetWeight(series));
        int baseDrop = Math.Max(1, cand.DropWeight);
        int levelBoost = luck > 0 && cand.Level >= Math.Max(1, maxAffixLevel - 2)
            ? 100 + Math.Min(300, luck * 5)
            : 100;

        return ((long)baseDrop * equipWeight * seriesWeight * levelBoost) / 100L;
    }

    private static int RollRangeWithLuck(Random rng, int min, int max, int luck)
    {
        if (min == max)
        {
            return min;
        }

        int lo = Math.Min(min, max);
        int hi = Math.Max(min, max);
        int roll = rng.Next(lo, hi + 1);
        if (luck > 0)
        {
            int secondRoll = rng.Next(lo, hi + 1);
            if (secondRoll > roll && rng.Next(100) < Math.Min(80, luck))
            {
                roll = secondRoll;
            }
        }

        return roll;
    }

    private ItemTemplate ResolveEquipmentTemplate(
        int detailType,
        int particularType,
        byte level,
        ElementSeries series)
    {
        if (_itemCatalog.TryGetItem(ItemGenreKind.Equipment, detailType, particularType, level, out ItemTemplate? tpl) &&
            tpl is not null)
        {
            return tpl;
        }

        return new ItemTemplate(
            Id: 10000 + (detailType * 100) + (particularType * 10) + level,
            Code: $"equip-{detailType}-{particularType}-lv{level}",
            Name: $"Trang Bị Cấp {level}",
            Genre: ItemGenreKind.Equipment,
            Quality: ItemQualityKind.Normal,
            DetailType: detailType,
            ParticularType: particularType,
            Level: level,
            ElementSeries: series,
            GridWidthCells: detailType is 0 or 2 ? (byte)2 : (byte)1,
            GridHeightCells: detailType == 0 ? (byte)3 : (detailType == 2 ? (byte)3 : (byte)1),
            ObjDataId: 4,
            AppearanceResId: 1,
            PriceGold: level * 200,
            MaxStackCount: 1,
            BaseAttributes:
            [
                new ItemBaseAttributeTemplate(18, "defense_v", level * 8, level * 12),
            ],
            Requirements:
            [
                new ItemRequirementTemplate(36, "level", Math.Max(1, (level - 1) * 10)),
            ]);
    }

    private static ItemTemplate CreateFallbackMedicineTemplate(int detailType, int particularType, byte level) =>
        new(
            Id: 20000 + (detailType * 100) + particularType,
            Code: particularType == 2 ? "ngung-than-dan" : "kim-sang-duoc",
            Name: particularType == 2 ? "Ngưng Thần Đan" : "Kim Sáng Dược",
            Genre: ItemGenreKind.Medicine,
            Quality: ItemQualityKind.Normal,
            DetailType: detailType,
            ParticularType: particularType,
            Level: level,
            ElementSeries: ElementSeries.Metal,
            GridWidthCells: 1,
            GridHeightCells: 1,
            ObjDataId: 2,
            AppearanceResId: 1,
            PriceGold: 50,
            MaxStackCount: 50,
            BaseAttributes:
            [
                particularType == 2
                    ? new ItemBaseAttributeTemplate(2, "mana_v", 150, 150, 90)
                    : new ItemBaseAttributeTemplate(1, "life_v", 150, 150, 90),
            ],
            Requirements: []);

    private static ushort ComputeBaseDurability(ItemTemplate tpl, uint seed)
    {
        foreach (ItemBaseAttributeTemplate baseAttr in tpl.BaseAttributes)
        {
            if (string.Equals(baseAttr.AttributeCode, "durability_v", StringComparison.OrdinalIgnoreCase) &&
                baseAttr.MaxValue > 0)
            {
                int lo = Math.Max(1, Math.Min(baseAttr.MinValue, baseAttr.MaxValue));
                int hi = Math.Max(lo, baseAttr.MaxValue);
                int val = lo + (int)(seed % (uint)(hi - lo + 1));
                return (ushort)Math.Clamp(val, 1, ushort.MaxValue);
            }
        }

        return (ushort)(40 + (tpl.Level * 5));
    }

    private static ItemMagicAttributeSlot[] CreateEmptyMagicSlots()
    {
        var slots = new ItemMagicAttributeSlot[ItemConsts.MaxMagicAffixSlots];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new ItemMagicAttributeSlot(i, 0, 0, 0, 0, 0);
        }

        return slots;
    }

    private static uint NextDeterministicSeed(int a, int b, int c, int d)
    {
        uint hash = 2166136261u;
        hash = (hash ^ (uint)a) * 16777619u;
        hash = (hash ^ (uint)b) * 16777619u;
        hash = (hash ^ (uint)c) * 16777619u;
        hash = (hash ^ (uint)d) * 16777619u;
        hash ^= (uint)Random.Shared.Next(1, int.MaxValue);
        return hash == 0 ? 1u : hash;
    }
}
