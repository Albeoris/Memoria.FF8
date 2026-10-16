using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeGardenWakeAssetsTests
{
    [Fact]
    public void OriginalTypeTenKeepsWakeGeometryAndRandomPoolHistoryAcrossFootRestoration()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeWorldForestEffect effect = assets.ForestTables.Effects[6];
        Assert.Equal((10, 4224, 16, 1, 2, 640), ((int)effect.Type, (int)effect.Shrink, (int)effect.Lifetime, (int)effect.Frames, (int)effect.FrameDuration, (int)effect.HalfSize));
        Assert.Equal((8, -8, 8, 0, 1, 0), ((int)effect.VelocityX, (int)effect.VelocityY, (int)effect.VelocityZ, (int)effect.AccelerationX, (int)effect.AccelerationY, (int)effect.AccelerationZ));
        var texture = assets.GetForestTexture(10, 0);
        Assert.Equal((32, 32), (texture.Width, texture.Height));
        Assert.Contains(texture.Pixels, color => color.A != 0);
        byte[] random = new byte[256];
        NativeGardenWorldObject garden = new(64, 0, new(1000, 200, 2000), 0, 0, 0);
        NativeWorldForestState first = NativeWorldForestState.Create(0, -5).AdvanceGarden(33, garden, 0, 0, random, assets.ForestTables.Effects);
        NativeWorldForestParticle particle = Assert.Single(first.Particles);
        Assert.Equal((992, -8, 1992, (short)-2048, (ushort)4092, (byte)1, (byte)12), (particle.X, particle.NativeY, particle.GraphicsZ, particle.Yaw, particle.Scale, particle.Age, particle.Lifetime));
        Assert.Equal(((short)-8, (short)-7, (short)-8), (particle.VelocityX, particle.VelocityY, particle.VelocityZ));
        Assert.Equal((byte)5, first.RandomCursor);
        Assert.Equal(-5, first.CameraCooldown);
        NativeWorldForestState restored = JsonSerializer.Deserialize<NativeWorldForestState>(JsonSerializer.Serialize(first))!;
        restored.Validate();
        Assert.Throws<InvalidDataException>(() => (restored with { Particles = [particle with { Motion = null }] }).Validate());
        NativeWorldForestState foot = first.Advance(6, 1000, 0, -2000, 0, 0, random, assets.ForestTables.Effects);
        Assert.Equal(JsonSerializer.Serialize(foot), JsonSerializer.Serialize(restored.Advance(6, 1000, 0, -2000, 0, 0, random, assets.ForestTables.Effects)));
        Assert.Equal((984, -15, 1984, (byte)2), (foot.Particles[0].X, foot.Particles[0].NativeY, foot.Particles[0].GraphicsZ, foot.Particles[0].Age));
        Assert.Equal((byte)5, foot.RandomCursor);
        NativeWorldForestState full = NativeWorldForestState.Create(0, 0) with { Particles = Enumerable.Range(0, 64).Select(slot => new NativeWorldForestParticle(slot, 4, 0, 0, 0, 0, 0, 4096, 0, 8, true)).ToArray() };
        NativeWorldForestState failedAllocation = full.AdvanceGarden(33, garden, 0, 0, random, assets.ForestTables.Effects);
        Assert.Equal((byte)3, failedAllocation.RandomCursor);
        Assert.Equal(64, failedAllocation.Particles.Length);
        Assert.DoesNotContain(failedAllocation.Particles, item => item.Type == 10);
    }
}
