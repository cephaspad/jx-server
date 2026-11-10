using JXServer.Primitives.Characters;
using JXServer.Primitives.Combat;
using Xunit;

namespace JXServer.Protocol.Tests;

public sealed class SixtyFourDirectionAndFiveElementsMathTests
{
    [Fact]
    public void TrigTable_Has64Entries_AndCardinalsMatchTenBitFixedPoint()
    {
        Assert.Equal(64, SixtyFourDirectionTrigTable.SinTable.Length);
        Assert.Equal(64, SixtyFourDirectionTrigTable.CosTable.Length);

        // Dir 0: South (dx = 0, dy = +1024)
        Assert.Equal(1024, SixtyFourDirectionTrigTable.DirSin(0));
        Assert.Equal(0, SixtyFourDirectionTrigTable.DirCos(0));

        // Dir 16: West (dx = -1024, dy = 0)
        Assert.Equal(0, SixtyFourDirectionTrigTable.DirSin(16));
        Assert.Equal(-1024, SixtyFourDirectionTrigTable.DirCos(16));

        // Dir 32: North (dx = 0, dy = -1024)
        Assert.Equal(-1024, SixtyFourDirectionTrigTable.DirSin(32));
        Assert.Equal(0, SixtyFourDirectionTrigTable.DirCos(32));

        // Dir 48: East (dx = +1024, dy = 0)
        Assert.Equal(0, SixtyFourDirectionTrigTable.DirSin(48));
        Assert.Equal(1024, SixtyFourDirectionTrigTable.DirCos(48));
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(-100, 0, 16)]
    [InlineData(0, -100, 32)]
    [InlineData(100, 0, 48)]
    public void GetDirectionIndex_ResolvesExpectedCardinalDirections(int dx, int dy, int expectedDir)
    {
        int actual = SixtyFourDirectionTrigTable.GetDirectionIndex(0, 0, dx, dy);
        Assert.Equal(expectedDir, actual);
    }

    [Theory]
    [InlineData(ElementSeries.Metal, ElementSeries.Wood, true, false)]
    [InlineData(ElementSeries.Wood, ElementSeries.Earth, true, false)]
    [InlineData(ElementSeries.Earth, ElementSeries.Water, true, false)]
    [InlineData(ElementSeries.Water, ElementSeries.Fire, true, false)]
    [InlineData(ElementSeries.Fire, ElementSeries.Metal, true, false)]
    [InlineData(ElementSeries.Metal, ElementSeries.Water, false, true)]
    [InlineData(ElementSeries.Water, ElementSeries.Wood, false, true)]
    [InlineData(ElementSeries.Wood, ElementSeries.Fire, false, true)]
    [InlineData(ElementSeries.Fire, ElementSeries.Earth, false, true)]
    [InlineData(ElementSeries.Earth, ElementSeries.Metal, false, true)]
    public void FiveElementsSeriesMatrix_ValidatesConquerAndAccrueCycles(
        ElementSeries source,
        ElementSeries target,
        bool expectedConquers,
        bool expectedAccrues)
    {
        Assert.Equal(expectedConquers, FiveElementsSeriesMatrix.Conquers(source, target));
        Assert.Equal(expectedAccrues, FiveElementsSeriesMatrix.Generates(source, target));

        if (expectedConquers)
        {
            Assert.Equal(target, FiveElementsSeriesMatrix.GetConqueredSeries(source));
            Assert.Equal(120, FiveElementsSeriesMatrix.GetSeriesDamageMultiplierPercent(source, target));
        }

        if (expectedAccrues)
        {
            Assert.Equal(target, FiveElementsSeriesMatrix.GetGeneratedSeries(source));
        }
    }

    [Fact]
    public void FiveElementsSeriesMatrix_ReturnsReducedMultiplierWhenTargetConquersAttacker()
    {
        Assert.Equal(80, FiveElementsSeriesMatrix.GetSeriesDamageMultiplierPercent(ElementSeries.Wood, ElementSeries.Metal));
        Assert.Equal(100, FiveElementsSeriesMatrix.GetSeriesDamageMultiplierPercent(ElementSeries.Metal, ElementSeries.Metal));
    }
}
