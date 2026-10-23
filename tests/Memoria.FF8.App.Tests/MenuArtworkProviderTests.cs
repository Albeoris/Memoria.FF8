using System.Diagnostics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Memoria.FF8.Resources;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class MenuArtworkProviderTests(ITestOutputHelper output)
{
    [Fact]
    public void Card_faces_cache_complete_owner_layers_and_refresh_when_the_system_font_changes()
    {
        MenuArtworkProvider provider = new(InstalledGameDirectory());
        UiImageSource source = new(UiImageKind.CardFace, 0, 0);
        PixelImage player = provider.Read(source);
        Assert.Equal(CardFaceComposer.Size, player.Width);
        Assert.Equal(CardFaceComposer.Size, player.Height);
        Assert.Same(player, provider.Read(source));
        PixelImage opponent = provider.Read(source with { Palette = 1 });
        Assert.False(player.Pixels.SequenceEqual(opponent.Pixels));
        PixelImage changedFont = provider.Read(source, "Times New Roman");
        Assert.NotSame(player, changedFont);
        Assert.False(player.Pixels.SequenceEqual(changedFont.Pixels));
        Assert.Same(changedFont, provider.Read(source, "Times New Roman"));
    }

    [Fact]
    public void Every_prepared_card_preview_reuses_its_owner_face_without_decoding_or_rasterizing_again()
    {
        MenuArtworkProvider provider = new(InstalledGameDirectory());
        PixelImage[,] prepared = new PixelImage[110, 2];
        Stopwatch clock = Stopwatch.StartNew();
        for (int cardId = 0; cardId < 110; cardId++)
        {
            for (int owner = 0; owner < 2; owner++)
            {
                prepared[cardId, owner] = provider.Read(new UiImageSource(UiImageKind.CardFace, cardId, owner));
            }
        }
        TimeSpan preparation = clock.Elapsed;
        clock.Restart();
        for (int navigation = 0; navigation < 100; navigation++)
        {
            for (int cardId = 0; cardId < 110; cardId++)
            {
                Assert.Same(prepared[cardId, navigation % 2], provider.Read(new UiImageSource(UiImageKind.CardFace, cardId, navigation % 2)));
            }
        }
        output.WriteLine($"Prepared 220 owner faces in {preparation.TotalMilliseconds:F2} ms; 11,000 cached preview navigations in {clock.Elapsed.TotalMilliseconds:F2} ms.");
    }

    [Fact]
    public void Report_bounded_cold_title_and_list_name_rasterization_cost()
    {
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(InstalledGameDirectory());
        Stopwatch clock = Stopwatch.StartNew();
        using (var initial = SystemTextLayout.Rasterize(catalog.Name(0), 26, 440))
        {
            Assert.True(initial.Width > 1);
        }
        TimeSpan firstTitle = clock.Elapsed;
        clock.Restart();
        foreach (string name in catalog.Names)
        {
            using var title = SystemTextLayout.Rasterize(name, 26, 440);
            using var row = SystemTextLayout.Rasterize(name, 19, 292);
        }
        output.WriteLine($"First cold title: {firstTitle.TotalMilliseconds:F2} ms; 110 title/list name pairs: {clock.Elapsed.TotalMilliseconds:F2} ms.");
    }

    [Fact]
    public void Native_rank_positions_are_baked_into_the_same_owner_image_without_a_text_backplate()
    {
        PixelImage transparentArt = new(64, 64);
        CardFaceComposer composer = new(SystemFontCatalog.DefaultFamily);
        PixelImage low = composer.Compose(transparentArt, new TripleTriadCard(0, 1, 1, 1, 1, TripleTriadElement.None), 0, null);
        PixelImage high = composer.Compose(transparentArt, new TripleTriadCard(0, 10, 10, 10, 10, TripleTriadElement.None), 0, null);
        Assert.False(low.Pixels.SequenceEqual(high.Pixels));
        Assert.Equal(low[128, 128], high[128, 128]);
        Assert.Equal((byte)255, low[128, 128].A);
        Assert.Equal((byte)0, low[0, 0].A);
        Assert.Contains(low.Pixels, pixel => pixel.R > 220 && pixel.G > 160 && pixel.B < pixel.R);
    }
    [Fact]
    public void High_resolution_card_faces_keep_artwork_pixels_and_native_layer_positions()
    {
        PixelImage artwork = new(512, 512);
        artwork[256, 256] = new Rgba32(13, 57, 91);
        PixelImage element = new(128, 128);
        Array.Fill(element.Pixels, new Rgba32(23, 67, 103));
        CardFaceComposer composer = new(SystemFontCatalog.DefaultFamily);
        PixelImage face = composer.Compose(artwork, new TripleTriadCard(0, 1, 1, 1, 1, TripleTriadElement.Fire), 0, element);
        Assert.Equal(512, face.Width);
        Assert.Equal(512, face.Height);
        Assert.Equal(artwork[256, 256], face[256, 256]);
        Assert.NotEqual(face[256, 256], face[257, 256]);
        Assert.Equal(element[0, 0], face[44 * 8, 4 * 8]);
        Assert.Equal((byte)0, face[0, 0].A);
    }

    [Fact]
    public void Published_artwork_replacements_refresh_cached_faces_and_restore_original_pixels()
    {
        string directory = InstalledGameDirectory();
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        MenuArtworkProvider provider = new(directory, resources);
        UiImageSource card = new(UiImageKind.CardFace, 0);
        UiImageSource grayscale = new(UiImageKind.CardFaceGrayscale, 0);
        UiImageSource background = new(UiImageKind.TripleTriadBackground, 0);
        PixelImage original = provider.Read(card);
        PixelImage originalGray = provider.Read(grayscale);
        PixelImage originalBackground = provider.Read(background);
        resources.Prepare(new SolidArtworkResolver()).Commit();
        PixelImage replacement = provider.Read(card);
        Assert.Equal(512, replacement.Width);
        Assert.NotSame(original, replacement);
        Assert.Equal(new Rgba32(13, 57, 91), replacement[256, 256]);
        Assert.NotSame(originalGray, provider.Read(grayscale));
        Assert.Equal(new Rgba32(13, 57, 91), provider.Read(background)[0, 0]);
        Assert.Same(replacement, provider.Read(card));
        resources.Prepare(null).Commit();
        Assert.Equal(original.Pixels, provider.Read(card).Pixels);
        Assert.Equal(originalGray.Pixels, provider.Read(grayscale).Pixels);
        Assert.Equal(originalBackground.Pixels, provider.Read(background).Pixels);
    }

    [Fact]
    public void Optional_installed_card_mod_reaches_menu_and_match_loaders_with_exact_pixel_provenance()
    {
        string directory = InstalledGameDirectory();
        string? package = Environment.GetEnvironmentVariable("MEMORIA_FF8_CARD_MOD_DIR");
        Assert.SkipUnless(package is not null && Directory.Exists(package), "An optional external card texture package is required.");
        using (GameResourceHost installed = new(directory, watch: false))
        {
            ModPackage? selected = installed.Mods.Catalog.Packages.FirstOrDefault(item => Path.GetFullPath(item.RootDirectory!).Equals(Path.GetFullPath(package!), StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                Assert.Equal(ModEffectiveStatus.Active, selected.Status);
                Assert.Equal(3072, NativeTripleTriadArtwork.Load(directory, installed.Resources).Background.Width);
            }
        }
        string root = Path.Combine(Path.GetTempPath(), "memoria-card-artwork-" + Guid.NewGuid().ToString("N"));
        string textures = Path.Combine(root, "Demaster", "fixture", "Textures");
        Directory.CreateDirectory(Path.Combine(textures, "cards"));
        try
        {
            foreach (string file in Directory.GetFiles(Path.Combine(package!, "Textures"), "*.png", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(Path.Combine(package!, "Textures"), file);
                File.Copy(file, Path.Combine(textures, relative));
            }
            using GameResourceHost host = new(directory, watch: false, modsDirectory: root);
            Assert.Contains(host.Mods.Catalog.Packages, item => item.Id == "Demaster/fixture" && item.Status == ModEffectiveStatus.Active);
            MenuArtworkProvider provider = new(directory, host.Resources);
            PixelImage menu = provider.Read(new UiImageSource(UiImageKind.Card, 0));
            NativeTripleTriadArtwork match = NativeTripleTriadArtwork.Load(directory, host.Resources);
            Assert.Equal(512, menu.Width);
            Assert.Equal(512, match.GetFront(0).Width);
            Assert.Equal(3072, match.Background.Width);
            Assert.Equal(1792, match.BoardBackground.Height);
            PixelImage menuSheet = TextureImageDecoder.Decode(File.ReadAllBytes(Path.Combine(textures, "mc00.tex.png")), "fixture.png").Image;
            PixelImage matchSheet = TextureImageDecoder.Decode(File.ReadAllBytes(Path.Combine(textures, "cards", "cards_0.png")), "fixture.png").Image;
            for (int row = 0; row < 512; row++)
            {
                Assert.Equal(menuSheet.Pixels.AsSpan(row * menuSheet.Width, 512).ToArray(), menu.Pixels.AsSpan(row * 512, 512).ToArray());
                Assert.Equal(matchSheet.Pixels.AsSpan(row * matchSheet.Width, 512).ToArray(), match.GetFront(0).Pixels.AsSpan(row * 512, 512).ToArray());
            }
            TextureBuildContext resident = new(new TextureKey(new ResourceKey("cards/cards"), "front/0"), host.Resources.Source, 1, "native-artwork", 64, 64, () => throw new InvalidOperationException("The registered artwork must remain resident."));
            Assert.StartsWith("Demaster/fixture/", host.Resources.GetTexture(resident).Value.Provenance);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class SolidArtworkResolver : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            if (context.Layout != "native-artwork" || context.Key.Owner.Path != "cards/cards" || context.Key.Subresource is not ("front/0" or "background"))
            {
                return null;
            }
            PixelImage image = new(context.LogicalWidth * 8, context.LogicalHeight * 8);
            Array.Fill(image.Pixels, new Rgba32(13, 57, 91));
            return new TextureReplacement(image, "test-artwork", context.LogicalWidth, context.LogicalHeight);
        }
    }

    private static string InstalledGameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "FF8_EN.exe")) && File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original card resources are required.");
        return directory;
    }
}
