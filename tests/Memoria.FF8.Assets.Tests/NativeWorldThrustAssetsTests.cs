namespace Memoria.FF8.Assets.Tests;

public sealed class NativeWorldThrustAssetsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(19)]
    [InlineData(20)]
    public void OriginalVehicleEffectsBindEveryFrameToTheirNativeTimAndPalette(byte type)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        var effect = assets.ForestTables.GetEffect(type);
        Assert.Equal(2, assets.ForestTables.RagnarokThrustOffsets.Count);
        for (int frame = 0; frame < effect.Frames; frame++)
        {
            var image = assets.GetForestTexture(type, frame);
            Assert.Equal(((int)effect.Width, (int)effect.Height), (image.Width, image.Height));
            Assert.Contains(image.Pixels, pixel => pixel.A != 0);
        }
    }
}
