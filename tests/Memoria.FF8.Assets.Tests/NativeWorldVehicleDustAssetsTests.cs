using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldVehicleDustAssetsTests
{
    [Fact]
    public void OriginalDustRecordUsesItsPublishedSpriteAndSharedPool()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldForestEffect effect = assets.ForestTables.Effects[15];
        Assert.Equal((19, 4224, 18, 1, 6, 768), ((int)effect.Type, (int)effect.Shrink, (int)effect.Lifetime, (int)effect.Frames, (int)effect.FrameDuration, (int)effect.HalfSize));
        Assert.Equal((856, 128, 928, 224, 32, 32, 0, 3), ((int)effect.TextureX, (int)effect.TextureY, (int)effect.PaletteX, (int)effect.PaletteY, (int)effect.Width, (int)effect.Height, (int)effect.DrawMode, (int)effect.BlendMode));
        var image = assets.GetForestTexture(19, 0);
        Assert.Equal((32, 32), (image.Width, image.Height));
        Assert.Contains(image.Pixels, pixel => pixel.A != 0);
        NativeWorldForestState state = NativeWorldForestState.Create(0, 0).AdvanceGarden(8, new(64, 0, new(1000, 0, 2000), 0, 0, 0), 0, 1, new byte[256], assets.ForestTables.Effects);
        Assert.Equal(2, state.Particles.Length);
        Assert.All(state.Particles, particle => Assert.Equal((byte)19, particle.Type));
    }
}
