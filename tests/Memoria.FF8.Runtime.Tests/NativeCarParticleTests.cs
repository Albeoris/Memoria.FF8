using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeCarParticleTests
{
    private static readonly NativeWorldForestEffect[] Effects =
    [
        new(2, 4224, 10, 0, 255, 1) { VelocityY = -12, VelocityZ = -4, AccelerationY = 2 },
        new(3, 4351, 6, 0, 255, 1) { VelocityY = -12, VelocityZ = -8, AccelerationY = 2 },
        new(14, 4352, 12, 1, 6, 255) { VelocityX = -16, AccelerationY = 2 }
    ];

    [Theory]
    [InlineData(8, 14, 2, 9)]
    [InlineData(0, 3, 2, 9)]
    [InlineData(10, 2, 8, 32)]
    [InlineData(34, 2, 8, 32)]
    [InlineData(27, 3, 0, 0)]
    public void GroundBranchPreservesNativeEmissionAndRandomOrder(byte ground, byte type, int count, byte cursor)
    {
        NativeWorldForestState result = NativeWorldForestState.Create(0, 0).AdvanceCar(ground, 35, new(100, -200, 300), 0, 1000, 0, 64, new byte[256], Effects);
        Assert.Equal(count, result.Particles.Length);
        Assert.Equal(cursor, result.RandomCursor);
        Assert.All(result.Particles, particle =>
        {
            Assert.Equal(type, particle.Type);
            Assert.Equal((byte)1, particle.Age);
            Assert.True(particle.Visible);
        });
        result.Validate();
    }

    [Fact]
    public void StoppedCarStillConsumesOrdinaryThresholdButNoSpecialGroundBurst()
    {
        NativeWorldForestState state = NativeWorldForestState.Create(0, 0);
        Assert.Equal((byte)1, state.AdvanceCar(0, 35, new(), 0, 0, 0, 0, new byte[256], Effects).RandomCursor);
        Assert.Equal((byte)0, state.AdvanceCar(10, 35, new(), 0, 0, 0, 0, new byte[256], Effects).RandomCursor);
        Assert.Equal((byte)0, state.AdvanceCar(0, 39, new(), 0, 0, 0, 64, new byte[256], Effects).RandomCursor);
    }

    [Fact]
    public void SuppressedEmissionStillAdvancesRetainedParticlesAndRestoresExactly()
    {
        NativeWorldForestState state = NativeWorldForestState.Create(0, 0) with
        {
            Particles = [new(0, 2, 100, 200, 300, 0, 0, 4096, 1, 10, true) { Motion = new(0, 1, -12, -4) }]
        };
        NativeWorldForestState next = state.AdvanceCar(28, 35, new(), 0, 0, 0, 0, new byte[256], Effects);
        NativeWorldForestParticle particle = Assert.Single(next.Particles);
        Assert.Equal((101, 188, 296, (short)-10, (byte)2), (particle.X, particle.NativeY, particle.GraphicsZ, particle.VelocityY, particle.Age));
        NativeWorldForestState restored = JsonSerializer.Deserialize<NativeWorldForestState>(JsonSerializer.Serialize(state))!;
        Assert.True(NativeWorldForestState.ContentEquals(next, restored.AdvanceCar(28, 35, new(), 0, 0, 0, 0, new byte[256], Effects)));
    }
}
