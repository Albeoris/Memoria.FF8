using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets.Tests;

public sealed class CardArtworkTests
{
    [Fact]
    public void OriginalAtlasesSupplyEveryDistinctFrontAndGoldBackAtNativeAspectRatio()
    {
        string gameDirectory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(gameDirectory, "Data", "lang-en", "menu.fs")), "Installed original menu resources are required.");
        CardArtwork artwork = CardArtwork.Load(gameDirectory);
        HashSet<string> fronts = [];
        for (int cardId = 0; cardId < CardArtwork.CardCount; cardId++)
        {
            PixelImage front = artwork.GetFront(cardId);
            Assert.Equal(64, front.Width);
            Assert.Equal(64, front.Height);
            Assert.True(fronts.Add(Hash(front)), $"Card {cardId} duplicates another front.");
        }
        Assert.Equal(64, artwork.Back.Width);
        Assert.Equal(64, artwork.Back.Height);
        Assert.DoesNotContain(Hash(artwork.Back), fronts);
        Assert.Equal("41411D34BE839341719EE37CC6667B6A751051A01FBD276FE73D651BE9407D56", Hash(artwork.GetFront(0)));
        Assert.Equal("721ECAD5F351E4FFF64CCFA45941A8B97B1FD8B23F7AB6527875BB2EC0EC4061", Hash(artwork.GetFront(10)));
        Assert.Equal("1A8A055D648A9AA7AD7C2ADCFFAE00A2D539D0EF4338EFE47F6F1327D0DAA02A", Hash(artwork.GetFront(11)));
        Assert.Equal("5D0475CB3A8D0A66D0631C1312F882E70E65E10F7466573E770D383F93141A70", Hash(artwork.GetFront(109)));
        Assert.Equal("60EAE4BF8FFEA731ACF5B0CBB0A66A0BEE1D6634C4626E0EAAF63505E01483B1", Hash(artwork.Back));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetFront(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.GetFront(CardArtwork.CardCount));
    }

    private static string Hash(PixelImage image)
    {
        return Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(image.Pixels.AsSpan())));
    }
}
