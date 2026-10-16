using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeCactuarBurstAssetsTests
{
    [Fact]
    public void LateNativeBurstConsumesItsActualBytesAndTheSharedPoolFirstAdvancesItNextCallback()
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeWorldForestEffect effect = assets.ForestTables.Effects[10];
        Assert.Equal((14, 4352, 12, 1, 6, 255, 0, -16, 0, 0, 2, 0), ((int)effect.Type, (int)effect.Shrink, (int)effect.Lifetime, (int)effect.Frames, (int)effect.FrameDuration, (int)effect.HalfSize, (int)effect.VelocityX, (int)effect.VelocityY, (int)effect.VelocityZ, (int)effect.AccelerationX, (int)effect.AccelerationY, (int)effect.AccelerationZ));
        var texture = assets.GetForestTexture(14, 0);
        Assert.Equal((32, 32), (texture.Width, texture.Height));
        Assert.Contains(texture.Pixels, color => color.A != 0);
        byte[] random = new byte[256];
        NativeWorldForestState initial = NativeWorldForestState.Create(0, -7);
        NativeWorldForestState emitted = initial.EmitCactuarBurst(new(1000, 200, 3000), random, assets.ForestTables.Effects);
        NativeWorldForestParticle pending = Assert.Single(emitted.Particles);
        Assert.Equal((1000, 200, 3000, (ushort)3968, (byte)0, (byte)8, false), (pending.X, pending.NativeY, pending.GraphicsZ, pending.Scale, pending.Age, pending.Lifetime, pending.Visible));
        Assert.Equal(((short)0, (short)-16, (short)0), (pending.VelocityX, pending.VelocityY, pending.VelocityZ));
        Assert.Equal((byte)3, emitted.RandomCursor);
        NativeWorldForestState restored = JsonSerializer.Deserialize<NativeWorldForestState>(JsonSerializer.Serialize(emitted))!;
        restored.Validate();
        NativeGardenWorldObject garden = new(64, 0, default, 0, 0, 0);
        NativeWorldForestState advanced = emitted.AdvanceGarden(6, garden, 0, 0, random, assets.ForestTables.Effects);
        NativeWorldForestParticle live = Assert.Single(advanced.Particles);
        Assert.Equal((184, (short)-14, (ushort)4216, (byte)1, true), (live.NativeY, live.VelocityY, live.Scale, live.Age, live.Visible));
        Assert.Equal(JsonSerializer.Serialize(advanced), JsonSerializer.Serialize(restored.AdvanceGarden(6, garden, 0, 0, random, assets.ForestTables.Effects)));
        NativeWorldForestState full = initial with { Particles = Enumerable.Range(0, 64).Select(slot => new NativeWorldForestParticle(slot, 4, 0, 0, 0, 0, 0, 4096, 0, 8, false)).ToArray() };
        NativeWorldForestState blocked = full.EmitCactuarBurst(default, random, assets.ForestTables.Effects);
        Assert.Equal((byte)1, blocked.RandomCursor);
        Assert.Equal(full.Particles, blocked.Particles);
        NativeWorldForestState missed = initial.EmitCactuarBurst(default, Enumerable.Repeat((byte)128, 256).ToArray(), assets.ForestTables.Effects);
        Assert.Empty(missed.Particles);
        Assert.Equal((byte)1, missed.RandomCursor);
    }
}
