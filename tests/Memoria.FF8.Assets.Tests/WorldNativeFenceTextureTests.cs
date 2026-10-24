namespace Memoria.FF8.Assets.Tests;

public sealed class WorldNativeFenceTextureTests
{
    [Theory]
    [InlineData(93)]
    [InlineData(96)]
    public void OriginalAnimatedFencePreservesNativePageBlendBits(byte shape)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldStaticModel model = assets.ReadScriptedStaticModel(shape);
        Assert.Equal(1, model.TexturePage >> 5 & 3);
        Assert.NotEmpty(model.Faces);
        Assert.NotEmpty(model.Textures);
    }
}
