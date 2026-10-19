using Albeoris.Games.FF8.Imaging;
using ImageMagick;
using ImageMagick.Drawing;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Remastered.Tests;

public sealed class RemasteredArtworkTextureTests
{
    [Fact]
    public void AtlasResolutionPreservesPixelsAndLanguageSelection()
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        byte[] red = Png(1024, MagickColors.Red);
        byte[] blue = Png(1024, MagickColors.Blue);
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz(("textures/hires/face_b00.tex.png", red), ("textures/mc05.tex.png", red), ("textures/mc05.tex_en.png", blue), ("textures/mag00.tex.png", red)));
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        using RemasteredGameDataSource english = new(directory);
        using RemasteredGameDataSource japanese = new(directory, "ja");
        TextureReplacement portrait = Assert.IsType<TextureReplacement>(english.Resolve(Context(english, "menu/face_b00.tex")));
        Assert.Equal(1024, portrait.Image.Width);
        Assert.Equal(256, portrait.LogicalWidth);
        Assert.Equal(new Rgba32(255, 0, 0), portrait.Image[1023, 1023]);
        Assert.Equal(new Rgba32(0, 0, 255), english.Resolve(Context(english, "menu/mc05.tex"))!.Image[0, 0]);
        Assert.Equal(new Rgba32(255, 0, 0), japanese.Resolve(Context(japanese, "menu/mc05.tex"))!.Image[0, 0]);
        Assert.Equal(1024, english.Resolve(Context(english, "menu/mag00.tex"))!.Image.Height);
        Assert.Null(english.Resolve(Context(english, "menu/mag08.tex")));
    }

    [Fact]
    public void TripleTriadPagesUseNativeTwoByFourCellsAndLocalizedPageSeven()
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        using MagickImage cells = new(MagickColors.Transparent, 768, 768);
        cells.Draw(new Drawables().FillColor(MagickColors.Red).Rectangle(192, 192, 383, 383).FillColor(MagickColors.Blue).Rectangle(0, 576, 191, 767));
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz(("textures/cards/text_0.png", cells.ToByteArray(MagickFormat.Png)), ("textures/cards/cards_0.png", cells.ToByteArray(MagickFormat.Png)), ("textures/cards/cards_13.png", cells.ToByteArray(MagickFormat.Png)), ("textures/cards/cards_7.png", Png(768, MagickColors.Red)), ("textures/cards/cards_7_en.png", Png(768, MagickColors.Blue))));
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        using RemasteredGameDataSource source = new(directory);
        TextureReplacement card = Assert.IsType<TextureReplacement>(source.Resolve(Context(source, "cards/cards", "front/3", 64)));
        Assert.Equal(48, source.Resolve(Context(source, "cards/cards", "element/0", 16))!.Image.Width);
        Assert.Equal(192, card.Image.Width);
        Assert.Equal(64, card.LogicalWidth);
        Assert.Equal(new Rgba32(255, 0, 0), card.Image[96, 96]);
        Assert.Equal(new Rgba32(0, 0, 255), source.Resolve(Context(source, "cards/cards", "back", 64))!.Image[96, 96]);
        Assert.Equal(new Rgba32(0, 0, 255), source.Resolve(Context(source, "cards/cards", "front/56", 64))!.Image[0, 0]);
    }

    [Fact]
    public void RejectsNonuniformArtworkScale()
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz(("textures/hires/face_b00.tex.png", Png(1000, MagickColors.Red))));
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz());
        using RemasteredGameDataSource source = new(directory);
        Assert.Throws<InvalidDataException>(() => source.Resolve(Context(source, "menu/face_b00.tex")));
    }

    [Fact]
    public void InstalledArtworkConsumersRetainHighResolutionSpriteCrops()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        NativeMenuArtwork menu = NativeMenuArtwork.Load(directory!);
        Assert.Equal((256, 384), (menu.GetCharacterPortrait(0).Width, menu.GetCharacterPortrait(0).Height));
        Assert.Equal((256, 384), (menu.GetGuardianForcePortrait(0).Width, menu.GetGuardianForcePortrait(0).Height));
        Assert.Equal(256, CardArtwork.Load(directory!).GetFront(0).Width);
        Assert.Equal(192, NativeTripleTriadArtwork.Load(directory!).GetFront(109).Width);
        Assert.True(NativeMenuPointer.Read(directory!).Image.Width > 32);
        Assert.Equal(416, NativeMagazineArtwork.Load(directory!).GetImage(0).Height);
        NativeTutorialArtwork tutorials = NativeTutorialArtwork.Load(directory!);
        using GameAssets assets = new(directory!);
        NativeTutorialBrowserPage page = assets.ReadNativeTutorialBrowserCatalog().Categories.SelectMany(category => category.Pages).First(page => page.ContentGroup == 5);
        Assert.Equal(43, page.Id);
        Assert.Equal(3, page.ContentIndex);
        PixelImage tutorial = tutorials.Read(page.Id);
        Assert.Equal((1536, 1024), (tutorial.Width, tutorial.Height));
        using RemasteredGameDataSource source = new(directory!);
        PixelImage atlas = source.Resolve(Context(source, "menu/mag13.tex"))!.Image;
        // Page43 places sprite47 (UV0,0,152x48) at native screen116,46.
        // Check installed artwork pixels inside that sprite, independently of its composition code.
        int checkedPixels = 0;
        for (int y = 0; y < 48 * 4; y++)
        {
            for (int x = 0; x < 152 * 4; x++)
            {
                Rgba32 expected = atlas[x, y];
                if (expected.A == 255 && expected != new Rgba32(0, 0, 0))
                {
                    Assert.Equal(expected, tutorial[116 * 4 + x, 46 * 4 + y]);
                    checkedPixels++;
                }
            }
        }
        Assert.True(checkedPixels > 100);
        Assert.Equal(48, NativeTripleTriadArtwork.Load(directory!).GetElement(Memoria.FF8.Runtime.TripleTriadElement.Fire).Width);
    }

    private static TextureBuildContext Context(RemasteredGameDataSource source, string owner, string subresource = "atlas", int size = 256)
    {
        return new TextureBuildContext(new TextureKey(new ResourceKey(owner), subresource), source.Descriptor, 1, "native-artwork", size, size, () => new PixelImage(size, size));
    }

    private static byte[] Png(uint size, IMagickColor<byte> color)
    {
        using MagickImage image = new(color, size, size);
        return image.ToByteArray(MagickFormat.Png);
    }
}
