using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Remastered.Tests;

public sealed class RemasteredInstalledSplitIconTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("it")]
    public void InstalledSplitPagesPreserveEveryPixelInAllNativePalettes(string language)
    {
        string directory = InstalledDirectory();
        using RemasteredGameDataSource source = new(directory, language);
        using ZzzArchive archive = new(Path.Combine(directory, "main.zzz"));
        for (int palette = 0; palette < 16; palette++)
        {
            PixelImage atlas = Resolve(source, palette);
            Assert.Equal((2048, 2048), (atlas.Width, atlas.Height));
            for (int page = 0; page < 4; page++)
            {
                PixelImage expected = ReadImage(archive, $"textures/hires/iconfl{page:D2}.tex/{language}/{palette}.png");
                Assert.Equal((1024, 1024), (expected.Width, expected.Height));
                for (int row = 0; row < expected.Height; row++)
                {
                    ReadOnlySpan<Rgba32> actual = atlas.Pixels.AsSpan((page / 2 * 1024 + row) * atlas.Width + page % 2 * 1024, 1024);
                    Assert.True(actual.SequenceEqual(expected.Pixels.AsSpan(row * 1024, 1024)), $"Palette {palette}, page {page}, row {row} changed RGBA pixels.");
                }
            }
        }
    }

    [Fact]
    public void InstalledJapaneseIconsUseTheirUnsplitLocalizedAtlases()
    {
        string directory = InstalledDirectory();
        using RemasteredGameDataSource source = new(directory, "ja");
        using ZzzArchive archive = new(Path.Combine(directory, "main.zzz"));
        for (int palette = 0; palette < 16; palette++)
        {
            PixelImage expected = ReadImage(archive, $"textures/icon.tex/jp/{palette}.png");
            PixelImage actual = Resolve(source, palette);
            Assert.Equal((expected.Width, expected.Height), (actual.Width, actual.Height));
            Assert.True(actual.Pixels.AsSpan().SequenceEqual(expected.Pixels));
        }
    }

    [Fact]
    public void InstalledPointerAndCompositeIconsUseSplitArtwork()
    {
        string directory = InstalledDirectory();
        NativeMenuArtwork artwork = NativeMenuArtwork.Load(directory);
        PixelImage pointer = NativeMenuPointer.Read(directory).Image;
        Assert.True(pointer.Pixels.AsSpan().SequenceEqual(artwork.GetIcon(0).Pixels));
        Assert.Contains(pointer.Pixels, pixel => pixel.A == 255);
        Assert.Equal((896, 128), (artwork.GetIcon(68).Width, artwork.GetIcon(68).Height));
        Assert.Equal((512, 64), (artwork.GetIcon(90).Width, artwork.GetIcon(90).Height));
        for (int id = 0; id < artwork.IconCount; id++)
        {
            if (id is >= 128 and < 140)
            {
                continue;
            }
            Assert.NotEmpty(artwork.GetIcon(id).Pixels);
        }
    }

    private static string InstalledDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        return directory!;
    }

    private static PixelImage Resolve(RemasteredGameDataSource source, int palette)
    {
        TextureBuildContext context = new(new TextureKey(new ResourceKey("menu/icon.tex"), "atlas", palette), source.Descriptor, 1, "native-artwork", 256, 256, () => new PixelImage(256, 256));
        return Assert.IsType<TextureReplacement>(source.Resolve(context)).Image;
    }

    private static PixelImage ReadImage(ZzzArchive archive, string path)
    {
        using Stream stream = archive.Open(path);
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        return TextureImageDecoder.Decode(bytes.ToArray(), path).Image;
    }
}
