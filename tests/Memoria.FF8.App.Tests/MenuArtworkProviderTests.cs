using System.Diagnostics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

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
    private static string InstalledGameDirectory()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "FF8_EN.exe")) && File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original card resources are required.");
        return directory;
    }
}
