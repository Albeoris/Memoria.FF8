using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTripleTriadOutcomeArtworkTests
{
    [Fact]
    public void NativeCaptureMasksRetainDistinctTransparentAnnouncements()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(game, "FF8_EN.exe")), "Configure original native data for capture artwork checks.");
        NativeTripleTriadArtwork artwork = NativeTripleTriadArtwork.Load(game);
        List<Rgba32[]> images = [];
        for (int capture = 0; capture < 3; capture++)
        {
            PixelImage image = artwork.GetCaptureBanner(capture);
            Assert.Equal(255, image.Width);
            Assert.Equal(64, image.Height);
            Assert.Contains(image.Pixels, pixel => pixel.A == 0);
            Assert.Contains(image.Pixels, pixel => pixel.A == 255);
            Assert.Equal(new[] { 12462, 13045, 11901 }[capture], image.Pixels.Count(pixel => pixel.A == 0));
            Assert.Same(image, artwork.GetCaptureBanner(capture));
            Assert.DoesNotContain(images, other => image.Pixels.SequenceEqual(other));
            images.Add(image.Pixels);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetCaptureBanner(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetCaptureBanner(3));
    }

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
