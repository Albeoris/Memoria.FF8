namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActionSquaredMagnitudeTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(3, 4, 0, 25)]
    [InlineData(-3, 0, -4, 25)]
    [InlineData(1, 2, 3, 14)]
    [InlineData(4096, 1, 0, 16777216)]
    [InlineData(0, 1, 4096, 16777216)]
    [InlineData(4097, 0, 0, 16785408)]
    [InlineData(32768, 32768, 32768, -1073741824)]
    public void NativeReturnedSquarePreservesFloatStoreAndLowDwordConversion(int x, int y, int z, int expected)
    {
        Assert.Equal(expected, NativeBattleActionGeometryRules.SquaredMagnitude56BC50(x, y, z));
    }
}
