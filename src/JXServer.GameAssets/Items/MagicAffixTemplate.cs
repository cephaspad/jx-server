using JXServer.Primitives.Characters;
using MemoryPack;

namespace JXServer.GameAssets.Items;

/// <summary>
/// Immutable magic affix definition from <c>settings/item/004/magicattrib.txt</c> (`m_wVersion = 4`).
/// </summary>
[MemoryPackable]
public sealed partial record MagicAffixTemplate(
    int Id,
    string Code,
    int AttributeId,
    string AttributeCode,
    string NameTemplate,
    bool IsSuffix,
    bool IsGoldenOnly,
    ElementSeries ElementSeries,
    int Level,
    int DropWeight,
    EquipPartWeightsTemplate WeightsByEquipPart,
    SeriesWeightsTemplate WeightsBySeries,
    int Param0Min,
    int Param0Max,
    int Param1Min = 0,
    int Param1Max = 0,
    int Param2Min = 0,
    int Param2Max = 0);
