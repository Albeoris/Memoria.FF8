using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterFieldPartialPlaneTests
{
    [Theory]
    [InlineData(383)]
    [InlineData(192)]
    [InlineData(4)]
    public void APartialWidthUsesItsCompleteSiblingScaleAndLeavesUnuploadedColumnsTransparent(int width)
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(0, 384, 384), Image(1, width, 384)).Resolve(Context(1)));

        Assert.Equal((384, 384), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Color(width - 1, 383), replacement.Image[width - 1, 383]);
        Assert.Equal(default, replacement.Image[width, 383]);
        Assert.Null(replacement.Variants);
    }

    [Theory]
    [InlineData(385)]
    [InlineData(700)]
    [InlineData(767)]
    public void AnActuallyUploadedSecondPlaneKeepsItsPartialPixelsAndTransparentTail(int width)
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(0, 384, 384), Image(1, width, 288)).Resolve(Context(1)));
        PixelImage closed = Assert.IsAssignableFrom<IReadOnlyDictionary<string, PixelImage>>(replacement.Variants)["eyes-closed"];

        Assert.Equal((384, 384), (closed.Width, closed.Height));
        Assert.Equal(Color(383, 287), replacement.Image[383, 287]);
        Assert.Equal(Color(width - 1, 287), closed[width - 385, 287]);
        Assert.Equal(default, closed[width - 384, 287]);
        Assert.Equal(default, closed[0, 288]);
    }

    [Fact]
    public void NativeCropCoordinatesRemainAnchoredWhenOnlyPartOfTheRectangleWasUploaded()
    {
        TextureReplacement replacement = Assert.IsType<TextureReplacement>(Resolver(Image(0, 384, 384), Image(1, 192, 144)).Resolve(Context(1, 32, 16, 64, 64)));

        Assert.Equal((192, 192), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal(Color(96, 48), replacement.Image[0, 0]);
        Assert.Equal(Color(191, 143), replacement.Image[95, 95]);
        Assert.Equal(default, replacement.Image[96, 95]);
        Assert.Equal(default, replacement.Image[95, 96]);
    }

    [Fact]
    public void APartialRectangleRequiresAnUnambiguousCompleteSourceAnchor()
    {
        Assert.Throws<ModTextureLoadException>(() => Resolver(Image(1, 383, 288)).Resolve(Context(1)));
        Assert.Throws<ModTextureLoadException>(() => Resolver(Image(0, 384, 384), Image(2, 512, 512), Image(1, 383, 288)).Resolve(Context(1)));
    }

    [Theory]
    [InlineData(769, 288)]
    [InlineData(383, 385)]
    public void PartialRectanglesCannotEscapeTwoNativePlanesOrTheirRowHeight(int width, int height)
    {
        ModTextureLoadException failure = Assert.Throws<ModTextureLoadException>(() => Resolver(Image(0, 384, 384), Image(1, width, height)).Resolve(Context(1)));
        Assert.Equal(Path(1), failure.RelativePath);
    }

    private static DemasterTextureResolver Resolver(params ModFile[] files) => new(new(1, [], files));
    private static string Path(int row) => $"textures/field.fs/field_hd_new/p199_{row}.png";
    private static Rgba32 Color(int x, int y) => new((byte)(x % 251), (byte)(y % 251), 70, 255);

    private static TextureBuildContext Context(int row, int x = 0, int y = 0, int width = 128, int height = 128)
    {
        return new(new(new("field/test/chara.one"), "texture/0"), new("fixture", GameDataEdition.Classic, ["native-field-model-sheet"]), 1, "classic-field-model", width, height, () => throw new InvalidOperationException("Original pixels stay lazy."))
        {
            NativeSheet = new(new("field/model/second_chr/p199"), $"row/{row}", 0, x, y, width, height, 128, 128)
        };
    }

    private static ModFile Image(int row, int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rgba32 color = Color(x, y);
                int offset = 4 * (y * width + x);
                pixels[offset] = color.R;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.B;
                pixels[offset + 3] = color.A;
            }
        }
        using MagickImage image = new(pixels, new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.RGBA));
        byte[] encoded = image.ToByteArray(MagickFormat.Png);
        return new("Demaster/Fixture", ModFormat.Demaster, Path(row), 1, () => encoded);
    }
}
