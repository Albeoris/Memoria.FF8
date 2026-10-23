using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class DemasterCardArtworkTests
{
    [Theory]
    [InlineData(GameDataEdition.Classic)]
    [InlineData(GameDataEdition.Remastered)]
    public void AllCardTargetsAreRecognizedWithoutReadingContent(GameDataEdition edition)
    {
        ModFile[] files = Targets().Select(stem => new ModFile("Demaster/Cards", ModFormat.Demaster, stem + ".png", 1, () => throw new InvalidOperationException("Inspection must not decode."))).ToArray();
        Assert.Equal(31, files.Length);
        DemasterInspection inspection = DemasterTextureResolver.Inspect(new("test", edition, ["demaster-card-artwork"]), new(1, [], files));
        Assert.Empty(inspection.Diagnostics);
        Assert.Equal("Demaster/Cards", Assert.Single(inspection.UsablePackageIds));
        Assert.Equal(31, DemasterTextureResolver.Inspect(new("test", edition, []), new(1, [], files)).Diagnostics.Count);
    }

    [Theory]
    [InlineData("en", "_en")]
    [InlineData("jp", "")]
    public void EveryCardAndMenuAtlasUsesItsEditionLayoutAndLanguage(string language, string suffix)
    {
        List<string> reads = [];
        byte[] png = Sheet();
        ModFile[] files = Targets().Select(stem => new ModFile("Demaster/Cards", ModFormat.Demaster, stem + ".png", 1, () =>
        {
            reads.Add(stem);
            return png;
        })).ToArray();
        DemasterTextureResolver resolver = new(new(1, [], files));
        for (int atlas = 0; atlas < 10; atlas++)
        {
            TextureReplacement value = Assert.IsType<TextureReplacement>(resolver.Resolve(Context($"menu/mc{atlas:00}.tex", "atlas", 256, 256, language)));
            Assert.Contains($"textures/mc{atlas:00}.tex{(atlas == 5 ? suffix : "")}.png", value.Provenance);
            Assert.Equal(512, value.Image.Width);
        }
        for (int card = 0; card < 110; card++)
        {
            TextureReplacement value = Assert.IsType<TextureReplacement>(resolver.Resolve(Context("cards/cards", $"front/{card}", 64, 64, language)));
            int atlas = card / 8;
            Assert.Contains($"textures/cards/cards_{atlas}{(atlas == 7 ? suffix : "")}.png", value.Provenance);
            Assert.Equal(128, value.Image.Width);
            Assert.Equal(new Rgba32((byte)(card % 2 * 64), (byte)(card % 8 / 2 * 64), 7), value.Image[0, 0]);
        }
        for (int element = 0; element < 8; element++)
        {
            PixelImage value = resolver.Resolve(Context("cards/cards", $"element/{element}", 16, 16, language))!.Image;
            Assert.Equal(32, value.Width);
            Assert.Equal(new Rgba32((byte)(element % 4 * 64), (byte)((element / 4 + 1) * 16), 7), value[0, 0]);
        }
        Assert.Equal(new Rgba32(0, 192, 7), resolver.Resolve(Context("cards/cards", "back", 64, 64, language))!.Image[0, 0]);
        foreach (string part in new[] { "background", "board-background" })
        {
            PixelImage background = resolver.Resolve(Context("cards/cards", part, 384, 224, language))!.Image;
            Assert.Equal(768, background.Width);
            Assert.Equal(448, background.Height);
            Assert.Equal(new Rgba32(255, 0, 7), background[511, 0]);
            Assert.Equal(new Rgba32(0, 0, 7), background[512, 0]);
            Assert.Equal(new Rgba32(127, 223, 7), background[767, 447]);
        }
        string[] languageAlternatives = language == "en" ? ["textures/mc05.tex", "textures/cards/cards_7"] : ["textures/mc05.tex_en", "textures/cards/cards_7_en"];
        Assert.Equal(Targets().Except(languageAlternatives).Order(), reads.Distinct().Order());
    }

    [Fact]
    public void PartialBackgroundPreservesOriginalHalfAndRejectsMismatchedScale()
    {
        ModFile left = File("textures/cards/back_0.png", 512);
        DemasterTextureResolver resolver = new(new(1, [], [left]));
        PixelImage image = resolver.Resolve(Context("cards/cards", "background", 384, 224))!.Image;
        Assert.Equal(new Rgba32(1, 2, 3), image[512, 0]);
        ModFile right = File("textures/cards/back_1.png", 256);
        resolver = new(new(2, [], [left, right]));
        Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(Context("cards/cards", "background", 384, 224)));
        resolver = new(new(3, [], [File("textures/cards/cards_0.png", 300)]));
        Assert.Throws<ModTextureLoadException>(() => resolver.Resolve(Context("cards/cards", "front/0", 64, 64)));
    }

    [Fact]
    public void InstalledCardPackageDecodesAndRoutesEveryFile()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARD_MOD_DIR");
        Assert.SkipUnless(Directory.Exists(directory), "Set MEMORIA_FF8_CARD_MOD_DIR to an optional external card texture package.");
        string[] paths = Directory.GetFiles(directory!, "*", SearchOption.AllDirectories);
        ModFile[] files = paths.Where(path => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)).Select(path => new ModFile("Demaster/Cards", ModFormat.Demaster, ModCatalogSnapshot.NormalizePath(Path.GetRelativePath(directory!, path)), 1, () => System.IO.File.ReadAllBytes(path))).ToArray();
        Assert.Equal(Targets().Select(stem => stem + ".png").Order(), files.Select(file => file.RelativePath.ToLowerInvariant()).Order());
        Assert.All(files, file => Assert.Equal(2048, ModImageDecoder.Decode(file).Image.Width));
        HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
        ModFile[] tracked = files.Select(file => new ModFile(file.PackageId, file.Format, file.RelativePath, file.Revision, () =>
        {
            used.Add(file.RelativePath);
            return file.ReadBytes();
        })).ToArray();
        DemasterTextureResolver resolver = new(new(1, [], tracked));
        foreach (string language in new[] { "en", "jp" })
        {
            for (int atlas = 0; atlas < 10; atlas++)
            {
                Assert.Equal(2048, resolver.Resolve(Context($"menu/mc{atlas:00}.tex", "atlas", 256, 256, language))!.Image.Width);
            }
            for (int card = 0; card < 110; card++)
            {
                Assert.Equal(512, resolver.Resolve(Context("cards/cards", $"front/{card}", 64, 64, language))!.Image.Width);
            }
        }
        for (int element = 0; element < 8; element++)
        {
            Assert.Equal(128, resolver.Resolve(Context("cards/cards", $"element/{element}", 16, 16))!.Image.Width);
        }
        Assert.Equal(512, resolver.Resolve(Context("cards/cards", "back", 64, 64))!.Image.Width);
        Assert.Equal(3072, resolver.Resolve(Context("cards/cards", "background", 384, 224))!.Image.Width);
        Assert.Equal(1792, resolver.Resolve(Context("cards/cards", "board-background", 384, 224))!.Image.Height);
        Assert.Equal(files.Length, used.Count);
    }

    private static TextureBuildContext Context(string owner, string part, int width, int height, string language = "en")
    {
        return new(new(new ResourceKey(owner), part), new("test", GameDataEdition.Classic, ["demaster-card-artwork"], language), 1, "native-artwork", width, height, () => new PixelImage(width, height, Enumerable.Repeat(new Rgba32(1, 2, 3), width * height).ToArray()));
    }

    private static ModFile File(string path, int size)
    {
        using MagickImage image = new(MagickColors.Red, (uint)size, (uint)size);
        byte[] bytes = image.ToByteArray(MagickFormat.Png);
        return new("Demaster/Cards", ModFormat.Demaster, path, 1, () => bytes);
    }

    private static byte[] Sheet()
    {
        byte[] pixels = new byte[512 * 512 * 4];
        for (int y = 0; y < 512; y++)
        {
            for (int x = 0; x < 512; x++)
            {
                int offset = (y * 512 + x) * 4;
                pixels[offset] = (byte)(x / 2);
                pixels[offset + 1] = (byte)(y / 2);
                pixels[offset + 2] = 7;
                pixels[offset + 3] = 255;
            }
        }
        using MagickImage image = new(pixels, new PixelReadSettings(512, 512, StorageType.Char, PixelMapping.RGBA));
        return image.ToByteArray(MagickFormat.Png);
    }

    private static IEnumerable<string> Targets()
    {
        return Enumerable.Range(0, 10).Select(index => $"textures/mc{index:00}.tex")
            .Append("textures/mc05.tex_en")
            .Concat(Enumerable.Range(0, 14).Select(index => $"textures/cards/cards_{index}"))
            .Concat(new[] { "cards_7_en", "back_0", "back_1", "board_0", "board_1", "text_0" }.Select(name => "textures/cards/" + name));
    }
}
