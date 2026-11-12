namespace Memoria.FF8.App.Tests;

public sealed class CrossoverPosedBridgePresentationTests
{
    [Theory]
    [InlineData("Water", 1)]
    [InlineData("Water", 4)]
    [InlineData("Bio", 1)]
    [InlineData("Bio", 4)]
    [InlineData("Flare", 1)]
    [InlineData("Flare", 4)]
    public void OriginalEffectsEmitTheirNativePolygonsAgainstPosedForeignModels(string name, int target)
    {
        new CrossoverNativeForeignSpellTests().OriginalSpellExecutesAgainstActualPosedHostGeometryAndItsAvailableStageContract(name, target, true);
    }
}
