namespace Memoria.Launcher.Tests;

public sealed class Ff7InstallationDiscoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.Ff7Discovery.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void FindsCurrentReleaseInASecondarySteamLibrary()
    {
        string steam = Path.Combine(root, "Steam");
        string secondary = Path.Combine(root, "Secondary Library");
        string game = WriteManifest(secondary, 3837340, "Current FF7");
        WriteLibraries(steam, (steam, []), (secondary, [3837340]));
        List<string> reports = [];

        IReadOnlyList<GameInstallation> found = GameInstallationDiscovery.FindSteamInstallations([steam], reports.Add, TestContext.Current.CancellationToken, ff7: true);

        Assert.Equal(game, Assert.Single(found).Directory);
        Assert.Empty(reports);
    }

    [Fact]
    public void CurrentReleasePrecedesLegacyAndLegacyExplainsItsSupportStatus()
    {
        string steam = Path.Combine(root, "Steam");
        string secondary = Path.Combine(root, "Secondary Library");
        string legacy = WriteManifest(steam, 39140, "Legacy FF7");
        string current = WriteManifest(secondary, 3837340, "Current FF7");
        WriteLibraries(steam, (steam, [39140]), (secondary, [3837340]));
        List<string> reports = [];

        IReadOnlyList<GameInstallation> found = GameInstallationDiscovery.FindSteamInstallations([steam], reports.Add, TestContext.Current.CancellationToken, ff7: true);

        Assert.Equal([current, legacy], found.Select(installation => installation.Directory));
        Assert.Contains("unsupported", found[1].Release, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("own risk", found[1].Release, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unsupported", found[0].Release, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(reports);
    }

    [Fact]
    public void Ff7DiscoveryDoesNotReturnEitherFf8Release()
    {
        string steam = Path.Combine(root, "Steam");
        WriteManifest(steam, 39150, "FF8 Classic");
        WriteManifest(steam, 1026680, "FF8 Remastered");
        WriteLibraries(steam, (steam, [39150, 1026680]));
        List<string> reports = [];

        IReadOnlyList<GameInstallation> found = GameInstallationDiscovery.FindSteamInstallations([steam], reports.Add, TestContext.Current.CancellationToken, ff7: true);

        Assert.Empty(found);
        Assert.Empty(reports);
    }

    [Fact]
    public void DefaultDiscoveryStillReturnsOnlyFf8Releases()
    {
        string steam = Path.Combine(root, "Steam");
        WriteManifest(steam, 3837340, "Current FF7");
        WriteManifest(steam, 39140, "Legacy FF7");
        string classic = WriteManifest(steam, 39150, "FF8 Classic");
        string remastered = WriteManifest(steam, 1026680, "FF8 Remastered");
        WriteLibraries(steam, (steam, [3837340, 39140, 39150, 1026680]));
        List<string> reports = [];

        IReadOnlyList<GameInstallation> found = GameInstallationDiscovery.FindSteamInstallations([steam], reports.Add, TestContext.Current.CancellationToken);

        Assert.Equal([classic, remastered], found.Select(installation => installation.Directory));
        Assert.Empty(reports);
    }

    [Fact]
    public void ManifestWithoutAnInstalledDirectoryIsFiltered()
    {
        string steam = Path.Combine(root, "Steam");
        WriteManifest(steam, 3837340, "Missing FF7", installed: false);
        string legacy = WriteManifest(steam, 39140, "Legacy FF7");
        WriteLibraries(steam, (steam, [3837340, 39140]));
        List<string> reports = [];

        IReadOnlyList<GameInstallation> found = GameInstallationDiscovery.FindSteamInstallations([steam], reports.Add, TestContext.Current.CancellationToken, ff7: true);

        Assert.Equal(legacy, Assert.Single(found).Directory);
    }

    [Fact]
    public void DuplicateSteamRootsDoNotDuplicateInstallations()
    {
        string steam = Path.Combine(root, "Steam");
        string current = WriteManifest(steam, 3837340, "Current FF7");
        WriteLibraries(steam, (steam, [3837340]));
        List<string> reports = [];

        IReadOnlyList<GameInstallation> found = GameInstallationDiscovery.FindSteamInstallations([steam, steam, Path.Combine(steam, ".")], reports.Add, TestContext.Current.CancellationToken, ff7: true);

        Assert.Equal(current, Assert.Single(found).Directory);
        Assert.Empty(reports);
    }

    [Fact]
    public void MalformedCurrentManifestDoesNotHideLegacyRelease()
    {
        string steam = Path.Combine(root, "Steam");
        string legacy = WriteManifest(steam, 39140, "Legacy FF7");
        WriteLibraries(steam, (steam, [3837340, 39140]));
        File.WriteAllText(Path.Combine(steam, "steamapps", "appmanifest_3837340.acf"), "\"AppState\" { \"appid\" \"invalid\" }");
        List<string> reports = [];

        IReadOnlyList<GameInstallation> found = GameInstallationDiscovery.FindSteamInstallations([steam], reports.Add, TestContext.Current.CancellationToken, ff7: true);

        Assert.Equal(legacy, Assert.Single(found).Directory);
        Assert.NotEmpty(reports);
    }

    [Fact]
    public void CancelledSteamDiscoveryDoesNotInspectLibraries()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        List<string> reports = [];

        Assert.ThrowsAny<OperationCanceledException>(() => GameInstallationDiscovery.FindSteamInstallations([Path.Combine(root, "Steam")], reports.Add, cancellation.Token, ff7: true));
        Assert.Empty(reports);
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void CancelledPlatformDiscoveryDoesNotInspectInstalledGames()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        List<string> reports = [];

        Assert.ThrowsAny<OperationCanceledException>(() => GameInstallationDiscovery.Find(reports.Add, cancellation.Token, ff7: true));
        Assert.Empty(reports);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string WriteManifest(string library, uint appId, string installationName, bool installed = true)
    {
        string steamApps = Path.Combine(library, "steamapps");
        Directory.CreateDirectory(steamApps);
        File.WriteAllText(Path.Combine(steamApps, $"appmanifest_{appId}.acf"), $$"""
            "AppState"
            {
                "appid" "{{appId}}"
                "Universe" "1"
                "name" "{{installationName}}"
                "installdir" "{{installationName}}"
            }
            """);
        string game = Path.Combine(steamApps, "common", installationName);
        if (installed)
        {
            Directory.CreateDirectory(game);
        }
        return game;
    }

    private static void WriteLibraries(string steam, params (string Path, uint[] AppIds)[] libraries)
    {
        string steamApps = Path.Combine(steam, "steamapps");
        Directory.CreateDirectory(steamApps);
        List<string> entries = [];
        for (int index = 0; index < libraries.Length; index++)
        {
            (string library, uint[] appIds) = libraries[index];
            string escapedPath = library.Replace("\\", "\\\\");
            string apps = string.Join(Environment.NewLine, appIds.Select(appId => $"\"{appId}\" \"1\""));
            entries.Add($$"""
                "{{index}}"
                {
                    "path" "{{escapedPath}}"
                    "apps"
                    {
                        {{apps}}
                    }
                }
                """);
        }
        File.WriteAllText(Path.Combine(steamApps, "libraryfolders.vdf"), "\"libraryfolders\"\n{\n" + string.Join(Environment.NewLine, entries) + "\n}\n");
    }
}
