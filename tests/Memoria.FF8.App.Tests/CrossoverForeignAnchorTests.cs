using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverForeignAnchorTests
{
    [Fact]
    public void SemanticImpactEndpointsDoNotRequireBodyOrRootResources()
    {
        NativeBattleVector impact = new(100, -300, 500);
        Assert.Equal(impact, CrossoverNativeForeignTarget.ReadSemanticBone(240, 4096, impact, Missing));
        Assert.Equal(impact, CrossoverNativeForeignTarget.ReadSemanticBone(241, 0, impact, Missing));

        static NativeBattleVector Missing() => throw new InvalidOperationException("A point-only target has no root mesh.");
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1024, 25, -75, 125)]
    [InlineData(2048, 50, -150, 250)]
    [InlineData(3072, 75, -225, 375)]
    public void RootToImpactFractionsUseTheCommonFixedPointSegment(short fraction, short x, short y, short z)
    {
        Assert.Equal(new NativeBattleVector(x, y, z), CrossoverNativeForeignTarget.ReadSemanticBone(240, fraction, new(100, -300, 500), () => default));
    }

    [Theory]
    [InlineData(240, -1)]
    [InlineData(240, 4097)]
    [InlineData(241, 2048)]
    [InlineData(2, 0)]
    public void UnmappedSkeletonSegmentsAreExplicitCapabilities(byte selector, short fraction)
    {
        Assert.Throws<NotSupportedException>(() => CrossoverNativeForeignTarget.ReadSemanticBone(selector, fraction, default, () => default));
    }
}
