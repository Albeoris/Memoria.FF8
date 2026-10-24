using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldContactSparkTests
{
    [Theory]
    [InlineData(-1, 2, true)]
    [InlineData(2, 2, false)]
    [InlineData(1, 2, true)]
    [InlineData(2, -1, false)]
    [InlineData(-1, 4, false)]
    public void ContactTransitionsCompareRetainedDenseIndex(int previous, int current, bool emits)
    {
        NativeWorldContactSpark candidate = new(new(1000, 200, 3000), 100, 0);
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess() with { PreviousContactC76784 = previous };
        NativeWorldContactSparkResult result = NativeWorldContactSparkEmitter.Select(process, current, 4, candidate);
        Assert.Equal(emits ? candidate : null, result.Spark);
        Assert.Equal(current, result.Process.PreviousContactC76784);
        Assert.Equal(-1, NativeWorldProcess.FromColdProcess().PreviousContactC76784);
        NativeWorldProcess restored = JsonSerializer.Deserialize<NativeWorldProcess>(JsonSerializer.Serialize(result.Process))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(result.Process), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void UnknownPrecedingContactFailsOnlyAtTheActualSparkBoundaryAndNoHitConverges()
    {
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess() with { PreviousContactC76784 = null };
        NativeWorldContactSpark candidate = new(new(0, 0, 0), 0, 1);
        Assert.Throws<NotSupportedException>(() => NativeWorldContactSparkEmitter.Select(process, 1, 4, candidate));
        Assert.Null(NativeWorldContactSparkEmitter.Select(process, 1, 4, null).Spark);
        Assert.Equal(-1, NativeWorldContactSparkEmitter.Select(process, -1, 4, null).Process.PreviousContactC76784);
    }

    [Theory]
    [InlineData(100, 0, 1024)]
    [InlineData(0, 100, 0)]
    [InlineData(-100, 0, -1024)]
    public void SparkUsesBodyHeightOffsetFixedLifetimeAndNoRandomAllocationDraws(short dx, short dz, short yaw)
    {
        NativeWorldForestEffect[] effects = Enumerable.Range(4, 18).Select(type => new NativeWorldForestEffect((byte)type, 5120, 12, 4, 3, 64)).ToArray();
        NativeWorldForestState result = NativeWorldForestState.Create(0, 0).Advance(8, 1000, 200, -3000, 0, 0, new byte[256], effects, new(new(1000, 200, 3000), dx, dz));
        NativeWorldForestParticle particle = Assert.Single(result.Particles);
        Assert.Equal((21, 1000, 0, 3000, yaw, 5120, 1, 12), ((int)particle.Type, particle.X, particle.NativeY, particle.GraphicsZ, particle.Yaw, (int)particle.Scale, (int)particle.Age, (int)particle.Lifetime));
        Assert.Equal((byte)0, result.RandomCursor);
        result.Validate();
    }
}
