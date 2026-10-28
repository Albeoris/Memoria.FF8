using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterGfAtlasTests
{
    [Fact]
    public void WholeTextureFormatDoesNotApplyInactiveOddComponentImages()
    {
        TextureBuildContext context = Context(new("mag777", 0, 2, 256, 256, 0, 0, 256, 256));
        TextureReplacement value = Assert.IsType<TextureReplacement>(Resolver(Image(0, 512, 512, MagickColors.Red), Image(1, 4, 4, MagickColors.Black)).Resolve(context));
        Assert.Equal((512, 512), (value.Image.Width, value.Image.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), value.Image[3, 259]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), value.Image[4, 259]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), value.Image[0, 260]);
    }

    [Fact]
    public void SourceCropUsesWholePageCoordinates()
    {
        TextureBuildContext context = Context(new("mag777", 0, 1, 256, 256, 128, 0, 128, 256));
        TextureReplacement value = Assert.IsType<TextureReplacement>(Resolver(Image(0, 512, 512, MagickColors.Red)).Resolve(context));
        Assert.Equal((256, 512), (value.Image.Width, value.Image.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), value.Image[255, 255]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), value.Image[255, 256]);
    }

    [Fact]
    public void SourcePublishedWideCanvasKeepsItsAspectAndSecondHalfPlacement()
    {
        TextureBuildContext context = Context(new("mag777", 2, 2, 512, 256, 256, 128, 256, 128));
        TextureReplacement value = Assert.IsType<TextureReplacement>(Resolver(Image(2, 1024, 512, MagickColors.Red), Image(3, 8, 8, MagickColors.Black)).Resolve(context));
        Assert.Equal((512, 256), (value.Image.Width, value.Image.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), value.Image[0, 0]);
    }

    [Fact]
    public void IncompatibleWholeImageFailsWithItsFileOwnership()
    {
        TextureBuildContext context = Context(new("mag777", 0, 2, 256, 256, 0, 0, 256, 256));
        ModTextureLoadException exception = Assert.Throws<ModTextureLoadException>(() => Resolver(Image(0, 512, 256, MagickColors.Red)).Resolve(context));
        Assert.Contains("mag777_0.png", exception.RelativePath);
    }

    [Fact]
    public void TranslatedWindowClampsToTheNativeAtlasEdgeWithoutTransparentPaddingOrRepeat()
    {
        TextureSourceAtlasAddress address = new("mag777", 0, 1, 384, 256, 0, 0, 256, 256) { SampleXOffset = 256, HorizontalAddressMode = TextureSourceAtlasAddressMode.Clamp };
        using MagickImage image = new(MagickColors.Red, 768, 512);
        using MagickImage edge = new(MagickColors.Blue, 2, 512);
        image.Composite(edge, 766, 0, CompositeOperator.Copy);
        byte[] bytes = image.ToByteArray(MagickFormat.Png);
        ModFile file = new("Demaster/Fixture", ModFormat.Demaster, "textures/battle.fs/hd_new/mag777_0.png", 1, () => bytes);
        TextureReplacement value = Assert.IsType<TextureReplacement>(Resolver(file).Resolve(Context(address)));
        Assert.Equal((512, 512), (value.Image.Width, value.Image.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), value.Image[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), value.Image[254, 0]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), value.Image[511, 511]);
    }

    private static TextureBuildContext Context(TextureSourceAtlasAddress address) => new(new(new("battle/native-page"), "page"), new("profile", GameDataEdition.Remastered, ["native-battle-gf-atlas"]), 1, "native-battle-gf-atlas", address.Width, address.Height, () => new(1, 1)) { NativeAtlas = address };

    private static DemasterTextureResolver Resolver(params ModFile[] files) => new(new(1, [], files));

    private static ModFile Image(int index, int width, int height, IMagickColor<byte> color)
    {
        using MagickImage image = new(color, (uint)width, (uint)height);
        byte[] bytes = image.ToByteArray(MagickFormat.Png);
        return new("Demaster/Fixture", ModFormat.Demaster, $"textures/battle.fs/hd_new/mag777_{index}.png", 1, () => bytes);
    }
}
