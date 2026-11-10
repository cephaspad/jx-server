using JXServer.Primitives.Characters;
using MemoryPack;

namespace JXServer.GameAssets.Items;

/// <summary>
/// Five-Elements series weights for a magic affix.
/// </summary>
[MemoryPackable]
public sealed partial record SeriesWeightsTemplate(
    int Metal = 0,
    int Wood = 0,
    int Water = 0,
    int Fire = 0,
    int Earth = 0)
{
    public int GetWeight(ElementSeries series) =>
        series switch
        {
            ElementSeries.Metal => Metal,
            ElementSeries.Wood => Wood,
            ElementSeries.Water => Water,
            ElementSeries.Fire => Fire,
            ElementSeries.Earth => Earth,
            _ => 0,
        };
}
