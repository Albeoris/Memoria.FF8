using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Remastered.Tests;

public sealed class RemasteredTripleTriadArtworkTests
{
    [Fact]
    public void InstalledNativeArtworkRetainsIndexedTransparencyAndBothBackgrounds()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        using RemasteredGameDataSource source = new(directory!);
        NativeTripleTriadArtwork artwork = NativeTripleTriadArtwork.Read(source);
        Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, source.SourceHash);
        Assert.Equal("65F3198F9EFC69B35D0B274256779553ABEC4B75665FE28D1D17BCEDB0692CA7", Hash(artwork.Background));
        Assert.Equal("9E5D2EAF1D98AA0E84243D71070DE674A57EA8B3433140F5CC3C20E209F0604D", Hash(artwork.BoardBackground));
        Assert.All(artwork.Background.Pixels, pixel => Assert.Equal((byte)255, pixel.A));
        Assert.All(artwork.BoardBackground.Pixels, pixel => Assert.Equal((byte)255, pixel.A));
        Assert.Equal(1753, artwork.GetFront(0).Pixels.Count(pixel => pixel.A == 0));
        Assert.Equal("C926A7B340C6F23CC595AA59508A299C0CAE5FB144DC44FB895B0F61F1176A06", Hash(artwork.GetFront(0)));
        Assert.Equal(CardArtwork.CardCount, Enumerable.Range(0, CardArtwork.CardCount).Select(id => Hash(artwork.GetFront(id))).Distinct().Count());
        Assert.Equal(8, Enum.GetValues<TripleTriadElement>().Where(element => element != TripleTriadElement.None).Select(element => Hash(artwork.GetElement(element))).Distinct().Count());
        Assert.Equal(64, artwork.Back.Width);
        Assert.Equal(64, artwork.Back.Height);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void RejectsPaletteBlocksOutsideTheAuthenticatedTextureExtent(int paletteLength)
    {
        Assert.Throws<InvalidDataException>(() => NativeTripleTriadArtwork.Read(new InvalidPaletteMemory(paletteLength)));
    }

    private static string Hash(PixelImage image) => Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(image.Pixels.AsSpan())));

    private sealed class InvalidPaletteMemory(int paletteLength) : IGameNativeMemorySource
    {
        public string SourceHash => NativeFieldRuntimeProfile.RemasteredEnImageHash;

        public byte[] Read(uint address, int count)
        {
            Assert.Equal(0xB96A90u, address);
            Assert.Equal(76832, count);
            byte[] bytes = new byte[count];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), paletteLength);
            return bytes;
        }

        public byte[] ReadLoader(uint address, int count) => throw new InvalidOperationException();
    }
}
