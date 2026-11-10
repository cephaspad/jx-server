using System.Collections.Generic;
using JXServer.Primitives.Characters;
using JXServer.Primitives.Items;

namespace JXServer.GameAssets.Items;

/// <summary>
/// Read-only catalog of item templates, magic affixes (`m_wVersion = 4`), and ground object templates.
/// </summary>
public interface IItemTemplateCatalog
{
    int ItemCount { get; }

    int MagicAffixCount { get; }

    IReadOnlyCollection<ItemTemplate> AllItems { get; }

    IReadOnlyCollection<MagicAffixTemplate> AllMagicAffixes { get; }

    IReadOnlyCollection<GroundObjectTemplate> AllGroundObjects { get; }

    bool TryGetItemByCode(string code, out ItemTemplate? item);

    bool TryGetItem(
        ItemGenreKind genre,
        int detailType,
        int particularType,
        byte level,
        out ItemTemplate? item,
        ItemQualityKind quality = ItemQualityKind.Normal);

    IReadOnlyList<MagicAffixTemplate> GetCandidateMagicAffixes(
        bool isSuffix,
        int equipDetailType,
        int weaponParticularType,
        ElementSeries series,
        int maxLevel);

    bool TryGetMagicAffixById(int affixRowId, out MagicAffixTemplate? affix);

    bool TryGetGroundObject(int objDataId, out GroundObjectTemplate? objTemplate);
}
