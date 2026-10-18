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
        Assert.EndsWith(".sgt", new NativeMusicCatalog(directory).MusicFileResolved(0));
        Assert.Throws<NotImplementedException>(() => new NativeWorldForestTables(directory));
        Assert.Throws<NotImplementedException>(() => NativeTripleTriadArtwork.Load(directory));
        Assert.Throws<NotImplementedException>(() => NativeSteamDiscMountReader.Read(directory, 1));
    }

    [Fact]
    public void NativeUiCatalogsInitializeFromAuthenticatedRemasteredData()
    {
        using GameAssets assets = new(InstalledDirectory());
        Assert.Equal(TripleTriadCatalog.CardCount, TripleTriadCatalog.Load(assets.GameDirectory).Cards.Length);
        NativeTutorialCatalog tutorials = assets.ReadNativeTutorialCatalog();
        Assert.Equal(Memoria.FF8.Runtime.NativeFieldRuntimeProfile.RemasteredEnImageHash, tutorials.ExecutableHash);
        Assert.Equal(9, tutorials.Tutorials.Length);
        Assert.NotNull(tutorials.CreateIsolatedParty(0));
        NativeTutorialBrowserCatalog browser = assets.ReadNativeTutorialBrowserCatalog();
        Assert.Equal(tutorials.ExecutableHash, browser.ExecutableHash);
        Assert.Equal(25, browser.Categories.Sum(category => category.Pages.Length));
        Assert.Equal(30, assets.ReadNativeTutorialExamCatalog().Levels.Length);
    }

    private static string InstalledDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        return directory!;
    }
}
