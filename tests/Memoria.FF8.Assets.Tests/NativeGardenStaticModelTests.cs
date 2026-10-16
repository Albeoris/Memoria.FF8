namespace Memoria.FF8.Assets.Tests;

public sealed class NativeGardenStaticModelTests
{
    [Theory]
    [InlineData(64, 143, 149)]
    [InlineData(65, 16, 16)]
    public void OriginalGardenModelDecodesItsEightBitTexturePageAndPalette(int shape, int vertices, int faces)
    {
        using WorldAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        WorldStaticModel model = assets.ReadScriptedStaticModel((byte)shape);
        Assert.Equal((ushort)0x8D, model.TexturePage);
        Assert.Equal(vertices, model.NativeVertices.Count);
        Assert.Equal(faces, model.Faces.Count);
        var texture = Assert.Single(model.Textures);
        Assert.Equal((256, 64), (texture.Width, texture.Height));
        Assert.Contains(texture.Pixels, color => color.A != 0);
        Assert.All(model.Faces, face => Assert.Equal(0, face.Palette));
        Assert.Same(model, assets.ReadScriptedStaticModel((byte)shape));
    }
}
