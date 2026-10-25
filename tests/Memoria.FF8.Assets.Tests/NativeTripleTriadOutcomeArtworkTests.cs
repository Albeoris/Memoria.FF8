using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTripleTriadOutcomeArtworkTests
{
    [Fact]
    public void NativeOutcomeMasksKeepTheirPageCoordinatesPaletteAndTransparentExtent()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(game, "FF8_EN.exe")), "Configure original native data for outcome artwork checks.");
        NativeTripleTriadArtwork artwork = NativeTripleTriadArtwork.Load(game);
        List<Rgba32[]> images = [];
        for (int outcome = 0; outcome < 3; outcome++)
        {
            PixelImage image = artwork.GetOutcomeBanner(outcome);
            Assert.Equal(256, image.Width);
            Assert.Equal(48, image.Height);
            Assert.Contains(image.Pixels, pixel => pixel.A == 0);
            Assert.Contains(image.Pixels, pixel => pixel.A == 255);
            Assert.Same(image, artwork.GetOutcomeBanner(outcome));
            Assert.DoesNotContain(images, other => image.Pixels.SequenceEqual(other));
            images.Add(image.Pixels);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetOutcomeBanner(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetOutcomeBanner(3));
    }
}
