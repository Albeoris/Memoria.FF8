using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStageColorTests
{
    [Theory]
    [InlineData((short)0, 0u)]
    [InlineData((short)2048, 0x101060u)]
    [InlineData((short)4096, 0x123456u)]
    [InlineData((short)-256, 0x101080u)]
    public void UniformTransitionPreservesSignedIntensityAndNativeTargetColor(short depth, uint color)
    {
        var original = NativeBattleStageState.FromColdProcess().Groups;
        var groups = original.Select(group => group with { ColorDepth = depth, ColorFlags = color }).ToImmutableArray();
        Assert.Equal(new NativeBattleStageColor(depth, color), NativeBattleStageColor.ReadUniform(groups));
        Assert.All(original, group => Assert.Equal(0u, group.ColorFlags));
    }

    [Fact]
    public void DifferentGroupIntensityOrColorRequiresAnExplicitHostMapping()
    {
        var groups = NativeBattleStageState.FromColdProcess().Groups;
        Assert.Throws<NotSupportedException>(() => NativeBattleStageColor.ReadUniform(groups.SetItem(1, groups[1] with { ColorDepth = 1 })));
        Assert.Throws<NotSupportedException>(() => NativeBattleStageColor.ReadUniform(groups.SetItem(1, groups[1] with { ColorFlags = 1 })));
    }

    [Fact]
    public void RenderingFlagsAreNotSilentlyTreatedAsRgb()
    {
        var groups = NativeBattleStageState.FromColdProcess().Groups.Select(group => group with { ColorFlags = 0x02010203u }).ToImmutableArray();
        Assert.Throws<NotSupportedException>(() => NativeBattleStageColor.ReadUniform(groups));
    }
}
