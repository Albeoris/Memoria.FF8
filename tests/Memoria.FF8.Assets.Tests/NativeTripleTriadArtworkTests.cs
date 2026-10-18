using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTripleTriadArtworkTests
{
    [Fact]
    public void Authenticated_rules_background_decodes_the_complete_opaque_native_framebuffer()
    {
        string gameDirectory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(gameDirectory, "FF8_EN.exe")), "Installed original executable is required.");
        NativeTripleTriadArtwork artwork = NativeTripleTriadArtwork.Load(gameDirectory);
        PixelImage image = artwork.Background;
        Assert.Equal(384, image.Width);
        Assert.Equal(224, image.Height);
        Assert.All(image.Pixels, pixel => Assert.Equal((byte)255, pixel.A));
        Assert.Equal("65F3198F9EFC69B35D0B274256779553ABEC4B75665FE28D1D17BCEDB0692CA7", Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(image.Pixels.AsSpan()))));
        Assert.Same(image, artwork.Background);
        PixelImage board = artwork.BoardBackground;
        Assert.Equal(384, board.Width);
        Assert.Equal(224, board.Height);
        Assert.All(board.Pixels, pixel => Assert.Equal((byte)255, pixel.A));
        Assert.Equal("9E5D2EAF1D98AA0E84243D71070DE674A57EA8B3433140F5CC3C20E209F0604D", Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(board.Pixels.AsSpan()))));
        Assert.Same(board, artwork.BoardBackground);
    }

    [Fact]
    public void Authenticated_native_element_sprites_are_distinct_and_keep_their_transparent_extent()
    {
        string gameDirectory = TestResources.GameDirectory;
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
