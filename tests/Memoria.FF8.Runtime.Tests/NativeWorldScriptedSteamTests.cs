using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldScriptedSteamTests
{
    [Theory]
    [InlineData(65)]
    [InlineData(66)]
    public void CarriageEmitsTwoSharedPoolParticlesAndRestoresTheirMotion(byte entity)
    {
        byte[] random = Enumerable.Repeat((byte)1, 256).ToArray();
        NativeWorldForestState state = NativeWorldForestState.Create(0, 0).AdvanceScriptedSteam(entity, new(100, -200, 300), new(0, 0, 0), random, Effects());
        Assert.Equal((byte)6, state.RandomCursor);
        Assert.Equal(new short[] { 4, -4 }, state.Particles.Select(particle => particle.Yaw));
        Assert.All(state.Particles, particle =>
        {
            Assert.Equal((0, 100, -200, 300, 1, 13), ((int)particle.Type, particle.X, particle.NativeY, particle.GraphicsZ, (int)particle.Age, (int)particle.Lifetime));
            Assert.NotNull(particle.Motion);
        });
        NativeWorldForestState restored = JsonSerializer.Deserialize<NativeWorldForestState>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(state.AdvanceScriptedSteam(entity, new(110, -200, 300), new(0, 0, 0), random, Effects())), JsonSerializer.Serialize(restored.AdvanceScriptedSteam(entity, new(110, -200, 300), new(0, 0, 0), random, Effects())));
    }

    [Fact]
    public void FullPoolConsumesOnlyBothOrientationBytesAndOrdinaryTrainCannotEmitSteam()
    {
        NativeWorldForestState full = NativeWorldForestState.Create(0, 0) with { Particles = Enumerable.Range(0, 64).Select(slot => new NativeWorldForestParticle(slot, 4, 0, 0, 0, 0, 0, 4096, 0, 8, true)).ToArray() };
        NativeWorldForestState result = full.AdvanceScriptedSteam(65, default, new(0, 0, 0), new byte[256], Effects());
        Assert.Equal((byte)2, result.RandomCursor);
        Assert.DoesNotContain(result.Particles, particle => particle.Type == 0);
        Assert.Throws<InvalidDataException>(() => full.AdvanceScriptedSteam(16, default, new(0, 0, 0), new byte[256], Effects()));
    }

    private static NativeWorldForestEffect[] Effects() => [new(0, 4096, 16, 1, 1, 32), new(4, 4096, 8, 1, 1, 32)];
}
