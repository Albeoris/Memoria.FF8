using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTutorialArtworkTests
{
    [Fact]
    public void EveryOriginalStaticPageDecodesItsBackgroundAndScreenshotSpriteParts()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original tutorial artwork is required.");
        using GameAssets assets = new(directory);
        NativeTutorialArtwork artwork = NativeTutorialArtwork.Load(directory);
        NativeTutorialBrowserCatalog browser = assets.ReadNativeTutorialBrowserCatalog();
        HashSet<string> images = [];
        foreach (NativeTutorialBrowserPage page in browser.Categories.SelectMany(category => category.Pages))
        {
            PixelImage image = artwork.Read(page.Id);
            Assert.Equal(384, image.Width);
            Assert.Equal(256, image.Height);
            Assert.Same(image, artwork.Read(page.Id));
            Assert.Equal((byte)0, image[0, 0].A);
            Assert.NotEqual((byte)0, image[24, 8].A);
            images.Add(Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(image.Pixels.AsSpan()))));
        }
        Assert.True(images.Count >= 18, $"Only{images.Count} different native static page backgrounds/screenshot compositions were produced.");
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.Read(0));
    }
}
