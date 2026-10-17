using Memoria.FF8.Assets;

namespace Memoria.FF8.Remastered.Tests;

public sealed class InstalledProviderEntryPointTests
{
    [Fact]
    public void SharedArtworkAndWorldCatalogEntryPointsUseRemasteredArchives()
    {
        string directory = InstalledDirectory();
        Assert.NotNull(CardArtwork.Load(directory).GetFront(0));
        Assert.NotNull(NativeMenuPointer.Read(directory).Image);
        Assert.NotNull(WorldMapLocations.ReadInstalled(directory));
    }

    [Fact]
    public void UnsupportedClassicNativeConsumersRejectRemasteredThroughTheProvider()
    {
        string directory = InstalledDirectory();
        Assert.Throws<NotImplementedException>(() => new NativeMusicCatalog(directory));
        Assert.Throws<NotImplementedException>(() => new NativeWorldForestTables(directory));
        Assert.Throws<NotImplementedException>(() => NativeTripleTriadArtwork.Load(directory));
        Assert.Throws<NotImplementedException>(() => TripleTriadCatalog.Load(directory));
        Assert.Throws<NotImplementedException>(() => GlobalAudioFormat.Read(directory, 0));
        Assert.Throws<NotImplementedException>(() => NativeSteamDiscMountReader.Read(directory, 1));
    }

    private static string InstalledDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        return directory!;
    }
}
