using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class DemasterCatalogComparisonTests
{
    [Fact]
    public async Task InstalledDemasterPackageComparesNativeMenuCardsAndBothBoardHalves()
    {
        string game = TestResources.GameDirectory;
        CancellationToken token = TestContext.Current.CancellationToken;
        string root = Path.Combine(Path.GetTempPath(), "catalog-demaster-preview-" + Guid.NewGuid().ToString("N"));
        string payload = Path.Combine(root, "Mods", "Demaster", "Cards");
        Directory.CreateDirectory(Path.Combine(payload, "Textures", "cards"));
        try
        {
            Rgba32 menuColor = new(240, 16, 128);
            Rgba32 cardColor = new(132, 16, 240);
            Rgba32 leftColor = new(16, 240, 132);
            Rgba32 rightColor = new(16, 132, 240);
            Write("Textures/mc00.tex.png", menuColor);
            Write("Textures/cards/cards_13.png", cardColor);
            Write("Textures/cards/board_0.png", leftColor);
            Write("Textures/cards/board_1.png", rightColor);
            using ModRuntime mods = new(root, watch: false);
            ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "Classic/en/test");
            using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "Classic/en/test", true);
            await library.StopAsync();
            ModPackage installed = new("Demaster/Cards", "Cards", ModFormat.Demaster, false, false, ModEffectiveStatus.Disabled, payload);
            Assert.True(ModPreviewDocument.CanOpenInstalled(installed));
            ModPreviewDocument document = await library.OpenPreviewAsync(null, installed, token);
            Assert.Equal(4, document.PackageCatalog.Files.Count);

            ModPreviewComparison menu = await Compare("mc00.tex");
            Assert.Equal(256, Assert.IsType<PixelImage>(menu.Before.Image).Width);
            Assert.Equal(menuColor, Assert.IsType<PixelImage>(menu.After.Image)[0, 0]);

            foreach (string cardName in new[] { "Card 110", "Card back" })
            {
                ModPreviewComparison card = await Compare(cardName);
                Assert.Equal(64, Assert.IsType<PixelImage>(card.Before.Image).Width);
                Assert.Equal(cardColor, Assert.IsType<PixelImage>(card.After.Image)[0, 0]);
            }

            foreach (string half in new[] { "board_0", "board_1" })
            {
                ModPreviewComparison board = await Compare(half);
                Assert.Equal(384, Assert.IsType<PixelImage>(board.Before.Image).Width);
                PixelImage replacement = Assert.IsType<PixelImage>(board.After.Image);
                Assert.Equal((768, 448), (replacement.Width, replacement.Height));
                Assert.Equal(leftColor, replacement[511, 0]);
                Assert.Equal(rightColor, replacement[512, 0]);
            }

            Task<ModPreviewComparison> Compare(string name)
            {
                return document.CompareAsync(Assert.Single(document.Entries, entry => entry.Name == name), new(game), token);
            }

            void Write(string path, Rgba32 color)
            {
                const int size = 512;
                PixelImage image = new(size, size, Enumerable.Repeat(color, size * size).ToArray());
                File.WriteAllBytes(Path.Combine(payload, path), LocalizationArtworkExporter.EncodePng(image));
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
