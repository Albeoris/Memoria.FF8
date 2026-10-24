using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldVehicleDustTests
{
    [Fact]
    public void GardenDustUsesPairedOriginalAnglesAllocationDrawsAndSharedAdvance()
    {
        NativeWorldForestState state = NativeWorldForestState.Create(0, 0).AdvanceGarden(8, Body(0), 0, 1, new byte[256], Effects());
        Assert.Equal((byte)9, state.RandomCursor);
        Assert.Equal(2, state.Particles.Length);
        Assert.Equal(new short[] { -224, -1824 }, state.Particles.Select(particle => particle.Yaw));
        Assert.Equal(new[] { 992, 1008 }, state.Particles.Select(particle => particle.X));
        Assert.Equal(new short[] { -8, 8 }, state.Particles.Select(particle => particle.VelocityX));
        Assert.All(state.Particles, particle =>
        {
            Assert.Equal((19, -8, 1997, -512, 4092, 1, 14), ((int)particle.Type, particle.NativeY, particle.GraphicsZ, (int)particle.Roll, (int)particle.Scale, (int)particle.Age, (int)particle.Lifetime));
            Assert.Equal(((short)-6, (short)-3), (particle.VelocityY, particle.VelocityZ));
        });
        NativeWorldForestState restored = JsonSerializer.Deserialize<NativeWorldForestState>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(state.AdvanceParticlesOnly(Effects())), JsonSerializer.Serialize(restored.AdvanceParticlesOnly(Effects())));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(600, 1000, 0)]
    [InlineData(-600, 1000, 0)]
    [InlineData(599, 1000, 2)]
    public void DustThresholdUsesSignedHeightAndExclusiveSixHundredBoundary(int groundHeight, int speed, int expected)
    {
        NativeWorldForestState state = NativeWorldForestState.Create(0, 0).AdvanceGarden(8, Body(0), groundHeight, speed, new byte[256], Effects());
        Assert.Equal(expected, state.Particles.Length);
        Assert.Equal(expected == 0 ? (byte)1 : (byte)9, state.RandomCursor);
    }

    [Fact]
    public void FullPoolStillDrawsBothEmitterOrientationsButSkipsAllocationRandomDraws()
    {
        NativeWorldForestState full = NativeWorldForestState.Create(0, 0) with { Particles = Enumerable.Range(0, 64).Select(slot => new NativeWorldForestParticle(slot, 4, 0, 0, 0, 0, 0, 4096, 0, 8, true)).ToArray() };
        NativeWorldForestState result = full.AdvanceGarden(8, Body(0), 0, 1, new byte[256], Effects());
        Assert.Equal((byte)5, result.RandomCursor);
        Assert.DoesNotContain(result.Particles, particle => particle.Type == 19);
    }

    private static NativeGardenWorldObject Body(int y) => new(64, 0, new(1000, y, 2000), 0, 0, 0);

    private static NativeWorldForestEffect[] Effects() => Enumerable.Range(4, 18).Select(type => type == 19
        ? new NativeWorldForestEffect(19, 4224, 18, 1, 6, 768) { VelocityY = -12, AccelerationY = 2 }
        : new NativeWorldForestEffect((byte)type, 3968, 8, 4, 2, 255)).ToArray();
}
