using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeStartupArtworkTests
{
    [Theory]
    [InlineData("main\\startup\\")]
    [InlineData("main/startup/")]
    [InlineData("main\\startup/")]
    public void ArchivePathsUseEitherSeparatorOnEveryPlatform(string prefix)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8.StartupTests", Guid.NewGuid().ToString("N"));
        string archiveDirectory = Path.Combine(directory, "Data", "lang-en");
        Directory.CreateDirectory(archiveDirectory);
        try
        {
            byte[] raw = new byte[8 + NativeStartupArtwork.Width * NativeStartupArtwork.Height * 2];
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(4), NativeStartupArtwork.Width);
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(6), NativeStartupArtwork.Height);
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(8), 0x001F);
            using MemoryStream compressed = new();
            compressed.Write(new byte[4]);
            for (int offset = 0; offset < raw.Length; offset += 8)
            {
                compressed.WriteByte(255);
                compressed.Write(raw, offset, Math.Min(8, raw.Length - offset));
            }
            byte[] imageBytes = compressed.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(imageBytes, (uint)(imageBytes.Length - 4));
            StringBuilder listing = new();
            using (BinaryWriter metrics = new(File.Create(Path.Combine(archiveDirectory, "main.fi"))))
            using (FileStream content = File.Create(Path.Combine(archiveDirectory, "main.fs")))
            {
                for (int index = 4; index <= 33; index++)
                {
                    listing.Append(FlArchive.InternalPathPrefix).Append(prefix).Append(NativeStartupArtwork.ResourceName(index).ToUpperInvariant()).Append("\r\n");
                    metrics.Write((uint)imageBytes.Length);
                    metrics.Write((uint)content.Position);
                    metrics.Write(0);
                    content.Write(imageBytes);
                }
            }
            File.WriteAllText(Path.Combine(archiveDirectory, "main.fl"), listing.ToString(), Encoding.ASCII);

            NativeStartupArtwork artwork = NativeStartupArtwork.Load(directory);
            for (int index = 4; index <= 33; index++)
            {
                PixelImage image = artwork.Read(index);
                Assert.Equal(NativeStartupArtwork.Width, image.Width);
                Assert.Equal(NativeStartupArtwork.Height, image.Height);
                Assert.Equal(new Rgba32(255, 0, 0, 255), image[0, 0]);
                Assert.Equal(new Rgba32(0, 0, 0, 255), image[1, 0]);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void OriginalStartupDirectoryDecodesEveryPictureIncludingNestedCompressedLogo()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "main.fs")), "Installed original startup resources are required.");
        AssertInstalledStartupArtwork(directory);
    }

    [Fact]
    public void RemasteredStartupDirectoryDecodesEveryPictureIncludingNestedCompressedLogo()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run the optional installed startup artwork check.");
        Assert.True(File.Exists(Path.Combine(directory!, "main.zzz")), "The selected Remastered installation must contain main.zzz.");
        Assert.True(File.Exists(Path.Combine(directory!, "other.zzz")), "The selected Remastered installation must contain other.zzz.");
        AssertInstalledStartupArtwork(directory!);
    }

    private static void AssertInstalledStartupArtwork(string directory)
    {
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
    public void Skipped_pause_retains_first_service_frame_and_every_later_picture_callback()
    {
        TimeSpan Time(int frame) => TimeSpan.FromSeconds((frame + 0.001) / NativeStartupCredits.FrameRate);
        Assert.Equal(NativeStartupCredits.SampleCredits(Time(0)), NativeStartupCredits.SampleCredits(Time(0), true));
        for (int frame = 1; frame <= 14837; frame++)
        {
            Assert.Equal(NativeStartupCredits.SampleCredits(Time(frame + 180)), NativeStartupCredits.SampleCredits(Time(frame), true));
        }
        Assert.Equal(5, NativeStartupCredits.SampleCredits(Time(1), true).ResourceIndex);
        Assert.False(NativeStartupCredits.SampleCredits(Time(14836), true).Finished);
        Assert.True(NativeStartupCredits.SampleCredits(Time(14837), true).Finished);
        Assert.Equal(TimeSpan.FromSeconds(14837 / NativeStartupCredits.FrameRate), NativeStartupCredits.GetCreditsCycleDuration(true));
        Assert.Equal(NativeStartupCredits.CreditsCycleDuration, NativeStartupCredits.GetCreditsCycleDuration(false));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeStartupCredits.SampleCredits(TimeSpan.FromTicks(-1), true));
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
