using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldRagnarokParticleTests
{
    [Theory]
    [InlineData(0, 2, 2)]
    [InlineData(1, 4, 4)]
    public void ThrusterAllocationsUseBothOriginalMountsAndScaleOnlyRandomFlag(int speed, int count, int cursor)
    {
        NativeWorldForestState result = Step(NativeWorldForestState.Create(0, 0), speed);
        Assert.Equal(count, result.Particles.Length);
        Assert.Equal((byte)cursor, result.RandomCursor);
        Assert.All(result.Particles, particle =>
        {
            Assert.Equal((byte)8, particle.Lifetime);
            Assert.Equal((byte)1, particle.Age);
            Assert.Equal(854, particle.NativeY);
            Assert.Equal(1992, particle.GraphicsZ);
            Assert.Equal((ushort)3968, particle.Scale);
        });
        Assert.Equal(2, result.Particles.Count(particle => particle.Type == 13));
        Assert.Equal(speed == 0 ? 0 : 2, result.Particles.Count(particle => particle.Type == 12));
    }

    [Fact]
    public void ExistingThrustUsesFiveSixthsMovementAfterItsFirstCallbackAndRestoresExactly()
    {
        NativeWorldForestState first = Step(NativeWorldForestState.Create(0, 0), 1);
        NativeWorldForestState restored = JsonSerializer.Deserialize<NativeWorldForestState>(JsonSerializer.Serialize(first))!;
        NativeWorldForestState next = Step(first, 1, 5);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(Step(restored, 1, 5)));
        Assert.Equal(4, next.Particles.Length);
        Assert.All(next.Particles, particle =>
        {
            Assert.Equal(844, particle.NativeY);
            Assert.Equal(2019, particle.GraphicsZ);
            Assert.Equal((byte)2, particle.Age);
        });
        Assert.Equal(new[] { 950, 950, 1150, 1150 }, next.Particles.Select(particle => particle.X));
        Assert.Equal(first.RandomCursor, next.RandomCursor);
        Assert.Throws<NotSupportedException>(() => first.AdvanceParticlesOnly(Effects()));
    }

    [Theory]
    [InlineData(8, 1, 19, 13)]
    [InlineData(33, 1, 15, 13)]
    [InlineData(9, 2, 20, 13)]
    [InlineData(9, 1, 20, 5)]
    public void NativeSurfaceBranchesRetainTheirThresholdAndRandomOrder(byte ground, int speed, byte expectedType, int cursor)
    {
        NativeWorldForestState result = Step(NativeWorldForestState.Create(0, 0), speed, ground: ground, groundHeight: 1000);
        Assert.Equal(cursor == 13 ? 2 : 0, result.Particles.Count(particle => particle.Type == expectedType));
        Assert.Equal((byte)cursor, result.RandomCursor);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void TransferModesSkipBothThrustMounts(byte mode)
    {
        NativeWorldForestState result = Step(NativeWorldForestState.Create(0, 0), 1, mode);
        Assert.Empty(result.Particles);
        Assert.Equal((byte)0, result.RandomCursor);
    }

    private static NativeWorldForestState Step(NativeWorldForestState state, int speed, byte mode = 0, byte ground = 0, int groundHeight = 0) => state.AdvanceRagnarok(ground, new(1, 0, new(1000, 1000, 2000), 0, 0, 0), groundHeight, speed, new(60, -12, 30), mode, [new(-100, 0, -10), new(100, 0, -10)], new byte[256], Effects());

    private static NativeWorldForestEffect[] Effects() => Enumerable.Range(4, 18).Select(type => type switch
    {
        12 or 13 => new NativeWorldForestEffect((byte)type, 4096, 8, 4, 2, 128) { VelocityZ = 2 },
        15 or 20 => new NativeWorldForestEffect((byte)type, 4224, 12, 1, 6, 768) { VelocityY = -64, AccelerationY = 6 },
        19 => new NativeWorldForestEffect(19, 4224, 18, 1, 6, 768) { VelocityY = -12, AccelerationY = 2 },
        _ => new NativeWorldForestEffect((byte)type, 3968, 8, 4, 2, 255)
    }).ToArray();
}
