using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldWaterSplashTests
{
    [Theory]
    [InlineData(141, true)]
    [InlineData(140, false)]
    [InlineData(147, true)]
    [InlineData(148, false)]
    [InlineData(301, true)]
    [InlineData(307, true)]
    [InlineData(308, false)]
    public void NativePhaseWindowsPreserveCumulativeAnglesAndAllocationRandomOrder(int phase, bool emitted)
    {
        NativeWorldForestState state = NativeWorldForestState.Create(0, 0).EmitWaterSplash(30, 1, phase, 8, new(10, 20, 30), Random(), Effects());
        Assert.Equal(emitted ? 8 : 0, state.Particles.Length);
        Assert.Equal(emitted ? (byte)32 : (byte)0, state.RandomCursor);
        if (emitted)
        {
            Assert.Equal(((short)-1016, (short)-504, (ushort)3971, (byte)10), (state.Particles[0].Yaw, state.Particles[0].Roll, state.Particles[0].Scale, state.Particles[0].Lifetime));
            Assert.Equal(((short)-1488, (short)-992, (ushort)3975, (byte)6), (state.Particles[1].Yaw, state.Particles[1].Roll, state.Particles[1].Scale, state.Particles[1].Lifetime));
            Assert.All(state.Particles, particle => Assert.Equal((10, 20, 30, (byte)0), (particle.X, particle.NativeY, particle.GraphicsZ, particle.Age)));
            state.Validate();
            NativeWorldForestState copy = JsonSerializer.Deserialize<NativeWorldForestState>(JsonSerializer.Serialize(state))!;
            Assert.True(NativeWorldForestState.ContentEquals(state.AdvanceParticlesOnly(Effects()), copy.AdvanceParticlesOnly(Effects())));
        }
    }

    [Fact]
    public void FullPoolConsumesOnlyTheTwoOrientationDrawsPerAttempt()
    {
        NativeWorldForestParticle[] pool = Enumerable.Range(0, 64).Select(index => new NativeWorldForestParticle(index, 16, 0, 0, 0, 0, 0, 4096, 0, 32, true) { Motion = new(0, 0, 0, 0) }).ToArray();
        NativeWorldForestState state = (NativeWorldForestState.Create(0, 0) with { Particles = pool }).EmitWaterSplash(11, 1, 304, 8, new(10, 20, 30), Random(), Effects());
        Assert.Equal((byte)16, state.RandomCursor);
        Assert.Equal(pool, state.Particles);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(18)]
    public void ChocoboAndCompanionFootprintsUseTheirOwnTypeWithoutRandomness(byte type)
    {
        NativeWorldWaterStepResult result = NativeWorldForestState.Create(0, 0).EmitWaterStep(type, 17, 1, 112, 0, new(10, 20, 30), 0, 100, 0, Effects());
        NativeWorldForestParticle particle = Assert.Single(result.Forest.Particles);
        Assert.Equal(type, particle.Type);
        Assert.Equal((short)228, particle.Yaw);
        Assert.Equal((byte)2, result.Edges);
        Assert.Equal((byte)0, result.Forest.RandomCursor);
        result.Forest.Validate();
    }

    private static byte[] Random() => Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
    private static NativeWorldForestEffect[] Effects() => [new(2, 4096, 10, 1, 1, 32), new(16, 4096, 32, 4, 8, 32) { DrawMode = 3 }, new(17, 4096, 32, 4, 8, 64) { DrawMode = 3 }, new(18, 4096, 32, 4, 8, 32) { DrawMode = 3 }];
}
