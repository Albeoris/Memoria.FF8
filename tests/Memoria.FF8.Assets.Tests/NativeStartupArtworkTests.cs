using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeStartupArtworkTests
{
    [Fact]
    public void OriginalStartupDirectoryDecodesEveryPictureIncludingNestedCompressedLogo()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "main.fs")), "Installed original startup resources are required.");
        NativeStartupArtwork artwork = NativeStartupArtwork.Load(directory);
        HashSet<string> hashes = [];
        for (int index = 4; index <= 33; index++)
        {
            PixelImage image = artwork.Read(index);
            Assert.Equal(640, image.Width);
            Assert.Equal(400, image.Height);
            Assert.Same(image, artwork.Read(index));
            Assert.True(image.Pixels.All(pixel => pixel.A == 255));
            Assert.Contains(image.Pixels, pixel => pixel.R != 0 || pixel.G != 0 || pixel.B != 0);
            hashes.Add(Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(image.Pixels.AsSpan()))));
        }
        Assert.Equal(30, hashes.Count);
        Assert.Equal("square.lzs", NativeStartupArtwork.ResourceName(4));
        Assert.Equal("name01.lzs", NativeStartupArtwork.ResourceName(5));
        Assert.Equal("loop01.lzs", NativeStartupArtwork.ResourceName(7));
        Assert.Equal("name14.lzs", NativeStartupArtwork.ResourceName(30));
        Assert.Equal("loop14.lzs", NativeStartupArtwork.ResourceName(32));
        Assert.Equal("ff8.lzs", NativeStartupArtwork.ResourceName(33));
        Assert.Throws<ArgumentOutOfRangeException>(() => artwork.Read(3));
    }

    [Fact]
    public void SquareHasNativeFourStepRevealTwoHoldsAndSingleStepExit()
    {
        NativeStartupCreditsFrame Frame(int frame) => NativeStartupCredits.SampleSquare(TimeSpan.FromSeconds((frame + 0.001) / 60.5));
        Assert.Equal(255, Frame(0).OverlayValue);
        Assert.Equal(3, Frame(63).OverlayValue);
        Assert.Equal(0, Frame(64).OverlayValue);
        Assert.Equal(2, Frame(65).NativeState);
        Assert.Equal(3, Frame(246).NativeState);
        Assert.Equal(4, Frame(367).NativeState);
        Assert.Equal(255, Frame(622).OverlayValue);
        Assert.False(Frame(622).Finished);
        Assert.True(Frame(623).Finished);
    }

    [Fact]
    public void CreditsUseOriginal374CallbackPanelsWhiteTransitionAndRepeatBoundary()
    {
        NativeStartupCreditsFrame Frame(int frame) => NativeStartupCredits.SampleCredits(TimeSpan.FromSeconds((frame + 0.001) / 60.5));
        Assert.Equal(79, NativeStartupCredits.MusicId);
        Assert.Equal(5, Frame(0).NativeState);
        Assert.Equal(255, Frame(180).OverlayValue);
        Assert.Equal(5, Frame(181).ResourceIndex);
        Assert.Equal(6, Frame(588).ResourceIndex);
        Assert.Equal(32, Frame(10653).ResourceIndex);
        Assert.True(Frame(10653).OverlayIsWhite);
        Assert.Equal(33, Frame(11030).ResourceIndex);
        Assert.True(Frame(11030).OverlayIsWhite);
        Assert.Equal(16, Frame(11160).NativeState);
        Assert.Equal(17, Frame(14761).NativeState);
        Assert.False(Frame(15016).Finished);
        Assert.True(Frame(15017).Finished);
    }
}
