using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTripleTriadArtworkTests
{
    [Fact]
    public void Authenticated_native_element_sprites_are_distinct_and_keep_their_transparent_extent()
    {
        string gameDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(gameDirectory, "FF8_EN.exe")), "Installed original executable is required.");
        NativeTripleTriadArtwork artwork = NativeTripleTriadArtwork.Load(gameDirectory);
        HashSet<string> hashes = [];
        foreach (TripleTriadElement element in Enum.GetValues<TripleTriadElement>().Where(element => element != TripleTriadElement.None))
        {
            PixelImage image = artwork.GetElement(element);
            Assert.Equal(16, image.Width);
            Assert.Equal(16, image.Height);
            Assert.Contains(image.Pixels, pixel => pixel.A == 0);
            Assert.Contains(image.Pixels, pixel => pixel.A == 255);
            Assert.True(hashes.Add(Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(image.Pixels.AsSpan())))));
            Assert.Same(image, artwork.GetElement(element));
        }
        HashSet<string> fronts = [];
        for (int cardId = 0; cardId < CardArtwork.CardCount; cardId++)
        {
            PixelImage front = artwork.GetFront(cardId);
            Assert.Equal(64, front.Width);
            Assert.Equal(64, front.Height);
            Assert.True(fronts.Add(Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(front.Pixels.AsSpan())))));
        }
        PixelImage geezard = artwork.GetFront(0);
        Assert.Equal(1753, geezard.Pixels.Count(pixel => pixel.A == 0));
        Assert.Equal("C926A7B340C6F23CC595AA59508A299C0CAE5FB144DC44FB895B0F61F1176A06", Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(geezard.Pixels.AsSpan()))));
        Assert.Equal(64, artwork.Back.Width);
        Assert.Equal(64, artwork.Back.Height);
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetElement(TripleTriadElement.None));
        Assert.Throws<NotSupportedException>(() => NativeTripleTriadArtwork.ReadExecutable(new byte[256]));
    }
}
