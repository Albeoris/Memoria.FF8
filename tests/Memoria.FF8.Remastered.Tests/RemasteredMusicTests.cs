using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Remastered.Tests;

public sealed class RemasteredMusicTests
{
    [Theory]
    [InlineData("026s-garden")]
    [InlineData("096s-keisho")]
    [InlineData("099-joriku2")]
    public void SegmentRequestsResolveToMountedOggWithTheActualDecoderExtension(string basename)
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        byte[] ogg = "OggS"u8.ToArray();
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz());
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz(($"data/music/dmusic/ogg/{basename}.ogg", ogg), ($"data/music/dmusic/{basename}.sgt", new byte[] { 99 })));
        string resolved;
        using (RemasteredGameDataSource source = new(directory))
        {
            resolved = source.ResolveFile($"music/dmusic/{basename}.sgt");
            Assert.Equal(".ogg", Path.GetExtension(resolved));
            Assert.Equal(ogg, File.ReadAllBytes(resolved));
            Assert.Equal(resolved, source.ResolveFile($"music/dmusic/{basename}.sgt"));
        }
        Assert.False(File.Exists(resolved));
    }

    [Fact]
    public void MissingMountedOggRefusesAnUnplayableSegment()
    {
        using SyntheticResources files = new();
        string directory = Path.GetDirectoryName(files.Write([]))!;
        File.WriteAllBytes(Path.Combine(directory, "main.zzz"), SyntheticResources.Zzz());
        File.WriteAllBytes(Path.Combine(directory, "other.zzz"), SyntheticResources.Zzz(("data/music/dmusic/026s-garden.sgt", new byte[] { 99 })));
        using RemasteredGameDataSource source = new(directory);
        Assert.Throws<FileNotFoundException>(() => source.ResolveFile("music/dmusic/026s-garden.sgt"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    [InlineData(96)]
    public void InstalledNativeMusicIdentitiesResolveToOggPayloads(int musicId)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        using RemasteredGameDataSource source = new(directory!);
        NativeMusicCatalog catalog = new(new ResourceService(source));
        string logical = catalog.MusicFileResolved(musicId).Replace('\\', '/');
        Assert.StartsWith("Data/", logical);
        string resolved = source.ResolveFile(logical[5..]);
        Assert.Equal(".ogg", Path.GetExtension(resolved));
        using FileStream stream = File.OpenRead(resolved);
        Span<byte> signature = stackalloc byte[4];
        stream.ReadExactly(signature);
        Assert.True(signature.SequenceEqual("OggS"u8));
    }
}
