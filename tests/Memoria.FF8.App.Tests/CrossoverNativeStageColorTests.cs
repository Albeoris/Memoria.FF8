using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverNativeStageColorTests
{
    private static readonly NativeBattleSummonColors Colors = new(0, 0, 1f / 4096, 4503601774854144d);

    [Theory]
    [InlineData(0xA0102030u, (short)0, 0x102080u, 0xA0102030u)]
    [InlineData(0xA0102030u, (short)2048, 0x102080u, 0xA0482020u)]
    [InlineData(0x13010203u, (short)4096, 0x102080u, 0x13802010u)]
    [InlineData(0x55112233u, (short)8192, 0xFFFFFFu, 0x55FFFFFFu)]
    [InlineData(0x55112233u, (short)-4096, 0xFFFFFFu, 0x55000000u)]
    public void NativeColorInterpolationReachesTheCorrectHostChannelsAndKeepsAlpha(uint source, short intensity, uint target, uint expected)
    {
        Assert.Equal(expected, new CrossoverNativeStageColor(Colors, intensity, target).Apply(source));
    }

    [Theory]
    [InlineData((short)-4096)]
    [InlineData((short)0)]
    [InlineData((short)1024)]
    [InlineData((short)4096)]
    [InlineData((short)8192)]
    public void ExistingBlackFadeRetainsItsOriginalRounding(short intensity)
    {
        const uint source = 0x7F3A9CDE;
        Assert.Equal(Colors.Interpolate(source, intensity, 0), new CrossoverNativeStageColor(Colors, intensity).Apply(source));
    }
}
