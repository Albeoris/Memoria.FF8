using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class WorldNativeTextureCopyTests
{
    [Theory]
    [InlineData(0, 832, 840)]
    [InlineData(3, 832, 840)]
    [InlineData(4, 840, 848)]
    [InlineData(12, 856, 832)]
    [InlineData(16, 832, 840)]
    public void NativeCopiesUseAdjacentFourTickFramesWithWrap(uint counter, int firstX, int nextX)
    {
        IReadOnlyList<NativeWorldTextureCopy> copies = WorldAssets.ReadNativeWorldTextureCopies(4, counter);
        Assert.Equal(2, copies.Count);
        Assert.Equal(new NativeWorldTextureCopy(firstX, 160, 856, 96, 8, 32), copies[0]);
        Assert.Equal(new NativeWorldTextureCopy(nextX, 160, 864, 96, 8, 32), copies[1]);
        Assert.Empty(WorldAssets.ReadNativeWorldTextureCopies(3, counter));
    }

    [Theory]
    [InlineData(93)]
    [InlineData(96)]
    public void OriginalStaticGeometryReceivesNativeAnimatedTextureCopies(byte shape)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        WorldStaticModel model = assets.ReadScriptedStaticModel(shape);
        Assert.Same(model.Textures, assets.ReadAnimatedStaticTextures(model, 3, 0));
        Assert.Throws<NotSupportedException>(() => assets.ReadAnimatedStaticTextures(model, null, 0));
        Assert.Throws<NotSupportedException>(() => assets.ReadAnimatedStaticTextures(model, 4, null));
        IReadOnlyList<PixelImage> first = assets.ReadAnimatedStaticTextures(model, 4, 0);
        IReadOnlyList<PixelImage> second = assets.ReadAnimatedStaticTextures(model, 4, 4);
        PixelImage source = assets.ReadNativeSectionTexture(38, 35);
        Assert.Equal(model.Textures.Count, first.Count);
        Assert.Equal(new System.Numerics.Vector2(64, 32), ResourceTextureList.LogicalSize(first, 0));
        for (int row = 0; row < 32; row++)
        {
            for (int column = 0; column < 64; column++)
            {
                Assert.Equal(source[column, row], first[0][column, row]);
                Assert.Equal(source[column + 32, row], second[0][column, row]);
            }
        }
        Assert.Same(first, assets.ReadAnimatedStaticTextures(model, 4, 16));
    }

    [Fact]
    public void NativeCopySourcesResolveBeforePackingAndRemainLiveAcrossPublication()
    {
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        {
            using WorldAssets assets = new(TestResources.GameDirectory, resources);
            WorldStaticModel model = assets.ReadScriptedStaticModel(93);
            IReadOnlyList<PixelImage> textures = assets.ReadAnimatedStaticTextures(model, 4, 0);
            resources.Prepare(new CopyResolver(20)).Commit();
            Assert.Equal(128, textures[0].Width);
            Assert.Equal(64, textures[0].Height);
            Assert.Equal(new System.Numerics.Vector2(64, 32), ResourceTextureList.LogicalSize(textures, 0));
            Assert.Equal(new Rgba32(20, 0, 0), textures[0][0, 0]);
            Assert.Equal(new Rgba32(70, 0, 0), textures[0][64, 0]);
            IReadOnlyList<PixelImage> next = assets.ReadAnimatedStaticTextures(model, 4, 4);
            Assert.Equal(new Rgba32(70, 0, 0), next[0][0, 0]);
            Assert.Equal(new Rgba32(120, 0, 0), next[0][64, 0]);
            resources.Prepare(new CopyResolver(30)).Commit();
            Assert.Equal(new Rgba32(30, 0, 0), textures[0][0, 0]);
            Assert.Equal(new Rgba32(80, 0, 0), next[0][0, 0]);
        }
        Assert.Equal(0, resources.ResidentTextureCount);
    }

    private sealed class CopyResolver(byte baseRed) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            if (context.NativeAlias is not { Subresource: "section/38/texture/35" } alias)
            {
                return null;
            }
            Assert.Equal((32, 32, 128, 32), (alias.Width, alias.Height, alias.CanvasWidth, alias.CanvasHeight));
            byte red = checked((byte)(baseRed + 50 * (alias.X / 32)));
            PixelImage image = new(64, 64, Enumerable.Repeat(new Rgba32(red, 0, 0), 64 * 64).ToArray());
            return new(image, "replacement", 32, 32);
        }
    }
}
