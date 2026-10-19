namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicRibbonBasisTests
{
    [Theory]
    [InlineData(0, 4090, 20, 4090)]
    [InlineData(4090, 0, 20, 0)]
    [InlineData(0, 2048, 100, 3996)]
    [InlineData(4090, 10, 5, 4095)]
    [InlineData(500, 500, 10, 500)]
    [InlineData(0, 100, -10, 4086)]
    public void TurnUsesNativeWrapDirectionOvershootAndSignedStep(int current, int target, int step, int expected)
    {
        Assert.Equal(expected, NativeBattleCinematicRibbonBasis.Turn(current, target, step));
    }

    [Fact]
    public void BasisPublishesRowVectorsAndLeavesMatrixPaddingTranslationUntouched()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        NativeBattleCinematicRibbonBasisResult result = NativeBattleCinematicRibbonBasis.Prepare(frame, new(0, 0, 0), new(0, 0, 4096));
        Assert.Equal(new short[] { -4096, 0, 0, 0, 4096, 0, 0, 0, -4096 }, result.Matrix.Rotation);
        Assert.False(result.Flip);
        Assert.Equal(0xA5A5, result.Frame.ReadTemporaryWord(0xB2));
        Assert.Equal(0xA5A5A5A5u, result.Frame.ReadTemporaryDword(0xB4));
        Assert.Equal(4096u, result.Frame.ReadTemporaryDword(0xFC));
        Assert.Equal(unchecked((uint)-4096), result.Frame.ReadTemporaryDword(0xF8));
    }

    [Theory]
    [InlineData(3, 4, 0, 5)]
    [InlineData(65535, 65535, 65535, -2147483648)]
    public void DistanceUsesWrappingSignedDwordSumBeforeTruncatedSqrt(int x, int y, int z, int expected)
    {
        Assert.Equal(expected, NativeBattleCinematicRibbonBasis.Distance(x, y, z));
    }
}
