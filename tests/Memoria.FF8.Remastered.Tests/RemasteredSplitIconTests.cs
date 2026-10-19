using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Remastered.Tests;

public sealed class RemasteredSplitIconTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void SplitPagesPreserveQuadrantsSeamsAndAlphaAndTakePrecedenceOverWholeAtlas(int scale)
    {
        using SyntheticResources files = new();
        List<(string Name, byte[] Data)> entries = SplitPages("en", 0, scale, 1);
        entries.Add(("textures/icon.tex/en/0.png", Png(256, 256, (_, _) => new Rgba32(255, 0, 255))));
        using RemasteredGameDataSource source = OpenSource(files, entries);

        TextureReplacement replacement = Assert.IsType<TextureReplacement>(source.Resolve(Context(source, 0)));

        Assert.Equal((256, 256), (replacement.LogicalWidth, replacement.LogicalHeight));
        Assert.Equal((256 * scale, 256 * scale), (replacement.Image.Width, replacement.Image.Height));
        AssertAtlas(replacement.Image, scale, 1);
        Assert.Same(replacement.Image, source.Resolve(Context(source, 0))!.Image);
    }

    [Fact]
    public void PaletteAndLanguageSelectionKeepSeparateAtlasesAndJapaneseUsesWholePageFallback()
    {
        using SyntheticResources files = new();
        List<(string Name, byte[] Data)> entries = SplitPages("en", 0, 1, 2);
        entries.AddRange(SplitPages("en", 7, 2, 9));
        entries.Add(("textures/icon.tex/jp/0.png", Png(512, 512, (_, _) => new Rgba32(31, 63, 95, 128))));
        entries.Add(("textures/icon.tex/jp/7.png", Png(256, 256, (_, _) => new Rgba32(17, 29, 41))));
        using RemasteredGameDataSource english = OpenSource(files, entries);
        using RemasteredGameDataSource japanese = new(Path.GetDirectoryName(files.Write([]))!, "ja");

        PixelImage first = Assert.IsType<TextureReplacement>(english.Resolve(Context(english, 0))).Image;
        PixelImage seventh = Assert.IsType<TextureReplacement>(english.Resolve(Context(english, 7))).Image;

        AssertAtlas(first, 1, 2);
        AssertAtlas(seventh, 2, 9);
        Assert.NotSame(first, seventh);
        Assert.Same(first, english.Resolve(Context(english, 0))!.Image);
        Assert.Same(seventh, english.Resolve(Context(english, 7))!.Image);
        TextureReplacement japaneseFirst = Assert.IsType<TextureReplacement>(japanese.Resolve(Context(japanese, 0)));
        TextureReplacement japaneseSeventh = Assert.IsType<TextureReplacement>(japanese.Resolve(Context(japanese, 7)));
        Assert.Equal((512, 512), (japaneseFirst.Image.Width, japaneseFirst.Image.Height));
        Assert.Equal(Enumerable.Repeat(new Rgba32(31, 63, 95, 128), 512 * 512).ToArray(), japaneseFirst.Image.Pixels);
        Assert.Equal(Enumerable.Repeat(new Rgba32(17, 29, 41), 256 * 256).ToArray(), japaneseSeventh.Image.Pixels);
        Assert.Null(japanese.Resolve(Context(japanese, 1)));
    }

    [Fact]
    public void MissingSelectedSplitFamilyUsesWholeAtlasAndAbsentSelectedPaletteReturnsNull()
    {
        using SyntheticResources files = new();
        List<(string Name, byte[] Data)> entries = SplitPages("en", 1, 1, 4);
        entries.Add(("textures/icon.tex/en/0.png", Png(768, 768, (_, _) => new Rgba32(19, 37, 53))));
        using RemasteredGameDataSource source = OpenSource(files, entries);

        TextureReplacement replacement = Assert.IsType<TextureReplacement>(source.Resolve(Context(source, 0)));

        Assert.Equal((768, 768), (replacement.Image.Width, replacement.Image.Height));
        Assert.Equal((256, 256), (replacement.LogicalWidth, replacement.LogicalHeight));
        Assert.Equal(new Rgba32(19, 37, 53), replacement.Image[767, 767]);
        Assert.Null(source.Resolve(Context(source, 2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MissingSelectedPageRejectsInsteadOfFallingBackOrUsingAnotherPalette(int missingPage)
    {
        using SyntheticResources files = new();
        List<(string Name, byte[] Data)> entries = SplitPages("en", 0, 1, 1);
        entries.RemoveAt(missingPage);
        entries.AddRange(SplitPages("en", 1, 1, 2));
        entries.Add(("textures/icon.tex/en/0.png", Png(256, 256, (_, _) => new Rgba32(255, 0, 255))));
        using RemasteredGameDataSource source = OpenSource(files, entries);

        Assert.Throws<InvalidDataException>(() => source.Resolve(Context(source, 0)));
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(129, 129)]
    [InlineData(128, 256)]
    [InlineData(256, 128)]
    [InlineData(256, 256)]
    public void InvalidPageGeometryOrDifferentIntegerScaleRejectsDespiteWholeAtlasFallback(int width, int height)
    {
        using SyntheticResources files = new();
        List<(string Name, byte[] Data)> entries = SplitPages("en", 0, 1, 1);
        entries[2] = (entries[2].Name, Png(width, height, (_, _) => new Rgba32(0, 255, 0)));
        entries.Add(("textures/icon.tex/en/0.png", Png(256, 256, (_, _) => new Rgba32(255, 0, 255))));
        using RemasteredGameDataSource source = OpenSource(files, entries);

        Assert.Throws<InvalidDataException>(() => source.Resolve(Context(source, 0)));
    }

    private static RemasteredGameDataSource OpenSource(SyntheticResources files, List<(string Name, byte[] Data)> entries)
    {
        string directory = Path.GetDirectoryName(files.Write([]))!;
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz(entries.ToArray()));
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        return new RemasteredGameDataSource(directory);
    }

    private static TextureBuildContext Context(RemasteredGameDataSource source, int palette)
    {
        return new TextureBuildContext(new TextureKey(new ResourceKey("menu/icon.tex"), "atlas", palette), source.Descriptor, 1, "native-artwork", 256, 256, () => throw new InvalidOperationException("Icon replacement must not decode the original atlas."));
    }

    private static List<(string Name, byte[] Data)> SplitPages(string language, int palette, int scale, int seed)
    {
        List<(string Name, byte[] Data)> entries = [];
        for (int page = 0; page < 4; page++)
        {
            int currentPage = page;
            entries.Add(($"textures/hires/iconfl{page:D2}.tex/{language}/{palette}.png", Png(128 * scale, 128 * scale, (x, y) => PagePixel(currentPage, x, y, seed))));
        }
        return entries;
    }

    private static void AssertAtlas(PixelImage atlas, int scale, int seed)
    {
        int pageSize = 128 * scale;
        Rgba32[] expected = new Rgba32[256 * scale * 256 * scale];
        for (int y = 0; y < 256 * scale; y++)
        {
            for (int x = 0; x < 256 * scale; x++)
            {
                int page = (y >= pageSize ? 2 : 0) + (x >= pageSize ? 1 : 0);
                expected[y * atlas.Width + x] = PagePixel(page, x % pageSize, y % pageSize, seed);
            }
        }
        Assert.Equal(expected, atlas.Pixels);
    }

    private static Rgba32 PagePixel(int page, int x, int y, int seed)
    {
        byte alpha = (x + 2 * y + page) % 5 == 0 ? (byte)0 : (x + y) % 3 == 0 ? (byte)128 : (byte)255;
        if (alpha == 0)
        {
            return default;
        }
        return new Rgba32((byte)(seed + 41 * page), (byte)(13 * x + 7), (byte)(7 * y + 11), alpha);
    }

    private static byte[] Png(int width, int height, Func<int, int, Rgba32> pixel)
    {
        byte[] rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rgba32 color = pixel(x, y);
                int offset = (y * width + x) * 4;
                rgba[offset] = color.R;
                rgba[offset + 1] = color.G;
                rgba[offset + 2] = color.B;
                rgba[offset + 3] = color.A;
            }
        }
        using MagickImage image = new(rgba, new MagickReadSettings { Width = (uint)width, Height = (uint)height, Depth = 8, Format = MagickFormat.Rgba });
        return image.ToByteArray(MagickFormat.Png);
    }
}
