namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldWaterStepTests
{
    [Theory]
    [InlineData(0, 112, 2, 128)]
    [InlineData(2, 272, 3, -128)]
    [InlineData(0, 272, 3, 128)]
    public void ThresholdEdgesPublishLastNewSideWithoutRandomDraws(byte previous, int phase, byte expectedEdges, short expectedYaw)
    {
        NativeWorldWaterStepResult result = Step(previous, phase);
        Assert.Equal(expectedEdges, result.Edges);
        NativeWorldForestParticle particle = Assert.Single(result.Forest.Particles);
        Assert.Equal((16, 32, 0, 4096, expectedYaw), ((int)particle.Type, (int)particle.Lifetime, (int)particle.Age, (int)particle.Scale, particle.Yaw));
        Assert.Equal((byte)0, result.Forest.RandomCursor);
        result.Forest.Validate();
        Assert.Empty(Step(result.Edges, phase).Forest.Particles);
    }

    [Fact]
    public void UnknownEdgesConvergeOnlyWhenBothNativeFlagsAreCleared()
    {
        Assert.Equal((byte)0, Step(null, 111).Edges);
        Assert.Throws<NotSupportedException>(() => Step(null, 112));
        Assert.Equal((byte)0, Step(3, 0).Edges);
        Assert.Equal((byte)3, Step(null, 272, ground: 8).Edges);
    }

    [Fact]
    public void DryGroundAndStandingPreserveTheNativeEdgeTiming()
    {
        Assert.Equal((byte)2, Step(0, 112, ground: 8).Edges);
        NativeWorldWaterStepResult standing = NativeWorldForestState.Create(0, 0).EmitFootWaterStep(9, 0, 200, null, new(0, 0, 0), 0, 0, 0, Effects());
        Assert.Null(standing.Edges);
        Assert.Empty(standing.Forest.Particles);
    }

    private static NativeWorldWaterStepResult Step(byte? previous, int phase, byte ground = 9) => NativeWorldForestState.Create(0, 0).EmitFootWaterStep(ground, 1, phase, previous, new(10, 20, 30), 0, 0, 0, Effects());
    private static NativeWorldForestEffect[] Effects() => [new(16, 0, 32, 4, 8, 32) { DrawMode = 3 }];
}
