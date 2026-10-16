using System.Text;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionResourceHostTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-junction-host-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResolvingFilesUsesDataFallbackAndMaterializesOnlyTheSelectedReplacement()
    {
        Write("Data/movies/original.avi", "original movie");
        WritePackage();
        Write("Mods/Junction/Example/Assets/movies/opening.avi", "replacement movie");
        Write("Mods/Junction/Example/Assets/movies/opening.cam", "camera");
        using GameResourceHost host = Host();
        string original = host.ResolveGameFile("movies/original.avi");
        Assert.Equal(Path.Combine(root, "Data", "movies", "original.avi"), original);
        Assert.Equal("original movie", File.ReadAllText(original));
        string replacement = host.ResolveGameFile("MOVIES\\OPENING.AVI");
        Assert.Equal("replacement movie", File.ReadAllText(replacement));
        Assert.NotEqual(Path.Combine(root, "Mods/Junction/Example/Assets/movies/opening.avi"), replacement);
        Assert.Equal(replacement, host.ResolveGameFile("movies/opening.avi"));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(replacement)!));
        string camera = host.ResolveGameFile("movies/opening.cam");
        Assert.Equal("camera", File.ReadAllText(camera));
        Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(replacement)!).Length);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(host.Mods.Catalog.Packages).Status);
    }

    [Fact]
    public void DisablingAPackageRestoresTheOriginalAndPreservesTheFilePinnedByPlayback()
    {
        Write("Data/movies/opening.avi", "original movie");
        WritePackage();
        Write("Mods/Junction/Example/Assets/movies/opening.avi", "replacement movie");
        using GameResourceHost host = Host();
        string replacement = host.ResolveGameFile("movies/opening.avi");
        using FileStream playback = File.OpenRead(replacement);
        Assert.True(host.Mods.SetEnabled("Junction/Example", false).Success);
        Assert.True(host.Pump(true));
        string original = host.ResolveGameFile("movies/opening.avi");
        Assert.Equal(Path.Combine(root, "Data", "movies", "opening.avi"), original);
        Assert.Equal("original movie", File.ReadAllText(original));
        Assert.Equal("replacement movie", File.ReadAllText(replacement));
        using StreamReader decoder = new(playback, Encoding.UTF8, leaveOpen: true);
        Assert.Equal("replacement movie", decoder.ReadToEnd());
        Assert.True(host.Mods.SetEnabled("Junction/Example", true).Success);
        Assert.True(host.Pump(true));
        Assert.Equal(replacement, host.ResolveGameFile("movies/opening.avi"));
    }

    [Fact]
    public void ARefreshedReplacementGetsANewCachePathWhileTheOldPlaybackFileRemainsValid()
    {
        WritePackage();
        Write("Mods/Junction/Example/Assets/movies/opening.avi", "old movie");
        using GameResourceHost host = Host();
        string old = host.ResolveGameFile("movies/opening.avi");
        Write("Mods/Junction/Example/Assets/movies/opening.avi", "new and longer movie");
        host.Mods.RequestRefresh();
        Assert.True(host.Pump(true));
        string current = host.ResolveGameFile("movies/opening.avi");
        Assert.NotEqual(old, current);
        Assert.Equal("old movie", File.ReadAllText(old));
        Assert.Equal("new and longer movie", File.ReadAllText(current));
    }

    [Fact]
    public void ChangingTheSourceBeforeFirstUseFailsWithoutPublishingAPartialCacheFile()
    {
        WritePackage();
        Write("Mods/Junction/Example/Assets/movies/opening.avi", "old movie");
        using GameResourceHost host = Host();
        Write("Mods/Junction/Example/Assets/movies/opening.avi", "new and longer movie");
        Assert.Throws<IOException>(() => host.ResolveGameFile("movies/opening.avi"));
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "junction-replacement-failed" && diagnostic.PackageId == "Junction/Example");
        host.Mods.RequestRefresh();
        Assert.True(host.Pump(true));
        string current = host.ResolveGameFile("movies/opening.avi");
        Assert.Equal("new and longer movie", File.ReadAllText(current));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(current)!));
    }

    [Fact]
    public void InstalledCinemaArchivePublishesMovieAndCameraReplacementsWithoutReadingMoviesIntoMemory()
    {
        string game = TestResources.GameDirectory;
        string archive = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_CINEMA_ARCHIVE") ?? Path.Combine(game, "Mods/Junction/cinema-rf.iroj");
        Assert.SkipUnless(File.Exists(archive), "The installed cinema-rf Junction archive is required.");
        JunctionModPackage package = JunctionModImporter.Open(archive);
        Assert.Equal("[Tsunamods] Cinema-RF FMV (30fps)", package.Name);
        Assert.Equal(210, package.Files.Count);
        Assert.Equal(106, package.Files.Count(file => file.RelativePath.EndsWith(".avi", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(104, package.Files.Count(file => file.RelativePath.EndsWith(".cam", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(209, package.Files.Count(file => file.RelativePath.StartsWith("movies/", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("lang-en/movies/disc03_05h.avi", Assert.Single(package.Files, file => file.RelativePath.StartsWith("lang-en/", StringComparison.OrdinalIgnoreCase)).RelativePath);
        JunctionReplacement movie = package.Files.First(file => file.RelativePath.EndsWith(".avi", StringComparison.OrdinalIgnoreCase));
        using (Stream input = movie.OpenRead())
        {
            byte[] header = new byte[64];
            input.ReadExactly(header);
            Assert.Contains(header, value => value != 0);
        }
        JunctionReplacement camera = package.Files.First(file => file.RelativePath.EndsWith(".cam", StringComparison.OrdinalIgnoreCase));
        using MemoryStream bytes = new();
        camera.CopyTo(bytes);
        Assert.True(bytes.Length > 0);
        Assert.True(bytes.Length < 1024 * 1024, "The camera fixture should remain a small bounded streaming check.");
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private GameResourceHost Host()
    {
        return new(root, Path.Combine(root, "application"), watch: false);
    }

    private void WritePackage()
    {
        Write("Mods/Junction/Example/mod.xml", "<ModInfo><Name>Example</Name><ID>99e6cd7c-970d-45a4-8096-3c980b94bd7a</ID><Version>1</Version><ModFolder Folder=\"Assets\" /></ModInfo>");
    }

    private void Write(string relative, string text)
    {
        string path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}





