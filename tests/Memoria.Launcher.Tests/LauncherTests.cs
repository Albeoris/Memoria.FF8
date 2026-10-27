using Memoria.FF8.Platform;
using System.Diagnostics;
using System.Net.Http;
using Memoria.FF8;
using Memoria.FF8.Resources;

namespace Memoria.Launcher.Tests;

public sealed class LauncherTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.Launcher.Tests", Guid.NewGuid().ToString("N"));

    public LauncherTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public void LauncherInstanceIdentityPreservesUnixPackagePathCase()
    {
        string first = UpdateInstaller.MutexName(Path.Combine(directory, "Game"));
        string second = UpdateInstaller.MutexName(Path.Combine(directory, "game"));
        Assert.Equal(OperatingSystem.IsWindows(), first == second);
    }

    [Fact]
    public void PortableSteamDiscoveryUsesLibraryDescriptorsWithoutRegistryAccess()
    {
        string game = CreateSteamInstallation("39150", false);
        string root = Path.Combine(directory, "Steam library");
        File.WriteAllText(Path.Combine(root, "steamapps", "appmanifest_39150.acf"), "\"AppState\" { \"appid\" \"39150\" \"Universe\" \"1\" \"name\" \"FINAL FANTASY VIII\" \"installdir\" \"Game files\" }");
        string escapedRoot = root.Replace("\\", "\\\\");
        File.WriteAllText(Path.Combine(root, "steamapps", "libraryfolders.vdf"), $"\"libraryfolders\" {{ \"0\" {{ \"path\" \"{escapedRoot}\" \"apps\" {{ \"39150\" \"0\" }} }} }}");
        List<string> warnings = [];
        GameInstallation installation = Assert.Single(GameInstallationDiscovery.FindSteamInstallations([root], warnings.Add, TestContext.Current.CancellationToken));
        Assert.Equal(game, installation.Directory);
        Assert.Empty(warnings);
    }

    [Fact]
    public void DiscoveryHonorsCancellationBeforeInspectingTheHost()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => GameInstallationDiscovery.Find(_ => throw new InvalidOperationException("Discovery must not start."), cancellation.Token));
    }

    [Fact]
    public void PlatformCapabilitiesRequireASupportedX64Desktop()
    {
        bool supported = (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64;
        Assert.Equal(supported, LauncherCapabilities.CanUpdate);
        Assert.Equal(supported, LauncherCapabilities.CanLaunchGame);
        Assert.Contains("x64", LauncherCapabilities.GameStatus);
        Assert.Contains("another operating system cannot be installed", LauncherCapabilities.UpdateStatus);
    }

    [Fact]
    public void PathIdentityMatchesHostFileSystem()
    {
        Assert.Equal(OperatingSystem.IsWindows(), LauncherCapabilities.PathComparer.Equals("Game", "game"));
    }

    [Fact]
    public void LastUsedInstallationIsFirstAndNotDuplicated()
    {
        string other = Path.Combine(directory, "Other");
        Directory.CreateDirectory(other);

        IReadOnlyList<GameInstallation> result = GameInstallationSelection.Order(directory, [new GameInstallation(other, "Steam"), new GameInstallation(directory.ToUpperInvariant() + Path.DirectorySeparatorChar, "Detected")]);

        Assert.Equal(2, result.Count);
        Assert.Equal("Last used", result[0].Release);
        Assert.Equal(directory, result[0].Directory);
        Assert.Equal(other, result[1].Directory);
    }

    [Fact]
    public void MissingLastUsedInstallationFallsBackToDiscovery()
    {
        IReadOnlyList<GameInstallation> result = GameInstallationSelection.Order(Path.Combine(directory, "Gone"), [new GameInstallation(directory, "Steam")]);

        Assert.Single(result);
        Assert.Equal("Steam", result[0].Release);
    }

    [Fact]
    public void SettingsRoundTripAndCorruptSettingsRecover()
    {
        string settingsPath = Path.Combine(directory, "settings", "launcher.json");
        new LauncherSettings(directory).Save(settingsPath);

        Assert.Equal(directory, LauncherSettings.Load(settingsPath).LastGameDirectory);
        File.WriteAllText(settingsPath, "{broken");
        Assert.Null(LauncherSettings.Load(settingsPath).LastGameDirectory);
        Assert.Null(LauncherSettings.Load(Path.Combine(directory, "absent.json")).LastGameDirectory);
    }

    [Fact]
    public void LocalOnlySelectionSurvivesSettingsAndLaunch()
    {
        string settingsPath = Path.Combine(directory, "launcher.json");
        new LauncherSettings(directory, true).Save(settingsPath);
        Assert.True(LauncherSettings.Load(settingsPath).AchievementsLocalOnly);
        Assert.Contains("--achievements-local-only", GameInstallationSelection.CreateStartInfo(directory, directory, true).ArgumentList);
        Assert.DoesNotContain("--achievements-local-only", GameInstallationSelection.CreateStartInfo(directory, directory).ArgumentList);
    }

    [Fact]
    public void BackendOverridePreservesLaunchArguments()
    {
        ProcessStartInfo start = GameInstallationSelection.CreateStartInfo(directory, directory, true, directory, Memoria.FF8.Platform.GraphicsBackend.Vulkan);
        Assert.Contains("--game-dir", start.ArgumentList);
        Assert.Contains("--mods-dir", start.ArgumentList);
        Assert.Contains("--media-tools", start.ArgumentList);
        Assert.Contains("--achievements-local-only", start.ArgumentList);
        int argument = start.ArgumentList.IndexOf("--graphics-backend");
        Assert.True(argument >= 0);
        Assert.Equal("Vulkan", start.ArgumentList[argument + 1]);
        Assert.DoesNotContain("--graphics-backend", GameInstallationSelection.CreateStartInfo(directory, directory).ArgumentList);
    }

    [Theory]
    [InlineData("39150", false)]
    [InlineData("1026680", true)]
    public void ManualRememberedAndDiscoveredSelectionsResolveTheSameProduct(string appId, bool remastered)
    {
        string game = CreateSteamInstallation(appId, remastered);
        GamePlatformContext manual = new GameInstallation(game, "Selected folder").ResolvePlatformContext();
        GamePlatformContext remembered = GameInstallationSelection.Order(game, []).Single().ResolvePlatformContext();
        GamePlatformContext discovered = new GameInstallation(game, "Untrusted display text").ResolvePlatformContext();
        Assert.Equal(GameDistributionProvider.Steam, manual.Provider);
        Assert.Equal(appId, manual.ProductId);
        Assert.Equal(remastered ? GameDataEdition.Remastered : GameDataEdition.Classic, manual.ResourceEdition);
        Assert.Equal(manual.ProductId, remembered.ProductId);
        Assert.Equal(manual.ProductId, discovered.ProductId);
        Assert.True(manual.CanInitializeExternalBackend);
        Assert.Equal(GameIntegrationTransport.SteamApi, manual.Transport);
    }

    [Fact]
    public void LooseFolderAndDllNamesDoNotSelectSteam()
    {
        WriteEdition(directory, true);
        File.WriteAllText(Path.Combine(directory, "steam_api.dll"), "not platform evidence");
        File.WriteAllText(Path.Combine(directory, "Galaxy.dll"), "not platform evidence");
        GamePlatformContext context = GamePlatformContextResolver.Resolve(directory);
        Assert.Equal(GameDistributionProvider.Unknown, context.Provider);
        Assert.Null(context.ProductId);
        Assert.False(context.CanInitializeExternalBackend);
    }

    [Fact]
    public void GogProductDoesNotBecomeSteamWhenBothLibrariesExist()
    {
        WriteEdition(directory, true);
        File.WriteAllText(Path.Combine(directory, "goggame-1086370078.info"), "{\"gameId\":\"1086370078\"}");
        File.WriteAllText(Path.Combine(directory, "steam_api.dll"), "wrapper candidate");
        File.WriteAllText(Path.Combine(directory, "Galaxy.dll"), "Galaxy candidate");
        GamePlatformContext context = GamePlatformContextResolver.Resolve(directory);
        Assert.Equal(GameDistributionProvider.Gog, context.Provider);
        Assert.Equal("1086370078", context.ProductId);
        Assert.Equal(GameIntegrationTransport.None, context.Transport);
        Assert.False(context.CanInitializeExternalBackend);
        Assert.True(GamePlatformContextResolver.Resolve(directory, true).LocalOnly);
    }

    [Fact]
    public void ConflictingStorefrontMetadataAndWrongEditionRemainLocal()
    {
        string game = CreateSteamInstallation("1026680", true);
        File.WriteAllText(Path.Combine(game, "goggame-1086370078.info"), "{\"gameId\":\"1086370078\"}");
        GamePlatformContext conflict = GamePlatformContextResolver.Resolve(game);
        Assert.Equal(GameDistributionProvider.Unknown, conflict.Provider);
        Assert.Contains("Conflicting", conflict.Diagnostic);
        File.Delete(Path.Combine(game, "goggame-1086370078.info"));
        File.WriteAllText(Path.Combine(game, "FF8_EN.exe"), "");
        Assert.Equal(GameDistributionProvider.Unknown, GamePlatformContextResolver.Resolve(game).Provider);
    }

    [Theory]
    [InlineData("\"AppState\" { \"appid\" \"39150\" \"installdir\" \"Game files\"")]
    [InlineData("\"AppState\" { \"appid\" \"1026680\" \"installdir\" \"Game files\" }")]
    [InlineData("\"AppState\" { \"appid\" \"39150\" \"appid\" \"39150\" \"installdir\" \"Game files\" }")]
    public void MalformedOrConflictingSteamMetadataCannotSelectAProvider(string metadata)
    {
        string game = CreateSteamInstallation("39150", false);
        File.WriteAllText(Path.Combine(directory, "Steam library", "steamapps", "appmanifest_39150.acf"), metadata);
        Assert.Equal(GameDistributionProvider.Unknown, GamePlatformContextResolver.Resolve(game).Provider);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("{\"gameId\":\"other-product\"}")]
    public void InvalidGogMetadataCannotEnableDelivery(string metadata)
    {
        WriteEdition(directory, true);
        File.WriteAllText(Path.Combine(directory, "goggame-1086370078.info"), metadata);
        GamePlatformContext context = GamePlatformContextResolver.Resolve(directory);
        Assert.Equal(GameDistributionProvider.Unknown, context.Provider);
        Assert.False(context.CanInitializeExternalBackend);
    }

    private string CreateSteamInstallation(string appId, bool remastered)
    {
        string steamApps = Path.Combine(directory, "Steam library", "steamapps");
        string game = Path.Combine(steamApps, "common", "Game files");
        Directory.CreateDirectory(game);
        WriteEdition(game, remastered);
        File.WriteAllText(Path.Combine(steamApps, $"appmanifest_{appId}.acf"), $"\"AppState\" {{ \"appid\" \"{appId}\" \"installdir\" \"Game files\" }}");
        return game;
    }

    private static void WriteEdition(string path, bool remastered)
    {
        foreach (string file in remastered ? new[] { "main.zzz", "other.zzz", "FFVIII_EN.exe.dat" } : new[] { "FF8_EN.exe" })
        {
            File.WriteAllText(Path.Combine(path, file), "");
        }
    }

    [Fact]
    public void LaunchArgumentsKeepPathsWithSpacesAsSeparateValues()
    {
        string package = Path.Combine(directory, "Memoria package");
        string game = Path.Combine(directory, "Game files");

        ProcessStartInfo result = GameInstallationSelection.CreateStartInfo(package, game);

        Assert.Equal(Path.Combine(package, "Data", Memoria.FF8.Platform.HostPlatforms.Current.ExecutableName("Memoria.FF8")), result.FileName);
        Assert.Equal(Path.Combine(package, "Data"), result.WorkingDirectory);
        Assert.Equal(new[] { "--game-dir", game, "--mods-dir", ApplicationInstallation.ResolveModsDirectory(package, HostPlatforms.Current, ApplicationPaths.Current.Configuration), "--media-tools", MediaToolsInstallation.ResolveDirectory() }, result.ArgumentList);
        string nextPackage = Path.Combine(directory, "Next release");
        Assert.Equal(ApplicationInstallation.ResolveModsDirectory(nextPackage, HostPlatforms.Current, ApplicationPaths.Current.Configuration), GameInstallationSelection.CreateStartInfo(nextPackage, game).ArgumentList[3]);
        Assert.False(result.UseShellExecute);
        Assert.True(result.RedirectStandardOutput);
        Assert.True(result.RedirectStandardError);
        Assert.True(result.CreateNoWindow);
    }

    [Fact]
    public async Task ValidInstalledMediaToolsAreReusedWithoutNetworkOrConsent()
    {
        using HttpClient client = new(new OfflineHandler());
        MediaToolsInstallation installation = new(client, new FfmpegDownloadOptions(new Uri("https://example.invalid/"), "ffmpeg-win64-lgpl.zip"), validate: path => Task.FromResult(path == directory));

        bool ready = await installation.EnsureAsync(directory, _ => throw new InvalidOperationException("Installed tools must not request download consent."), _ => throw new InvalidOperationException("Installed tools must not report download progress."));

        Assert.True(ready);
    }

    [Fact]
    public void ValidationRejectsUnsupportedAndIncompleteInstallations()
    {
        Assert.Contains("English Steam (2013)", GameInstallationSelection.ValidateForEngine(directory));
        File.WriteAllText(Path.Combine(directory, "FF8_EN.exe"), "");
        Assert.Contains("main.fs", GameInstallationSelection.ValidateForEngine(directory));
        string data = Path.Combine(directory, "Data", "lang-en");
        Directory.CreateDirectory(data);
        foreach (string archive in new[] { "main", "menu", "field", "battle", "world", "magic" })
        {
            foreach (string extension in new[] { ".fs", ".fi", ".fl" })
            {
                File.WriteAllText(Path.Combine(data, archive + extension), "");
            }
        }
        Assert.Null(GameInstallationSelection.ValidateForEngine(directory));
        File.Delete(Path.Combine(data, "field.fi"));
        Assert.Contains("field.fi", GameInstallationSelection.ValidateForEngine(directory));
    }

    [Fact]
    public void CompleteRemasteredInstallationDoesNotRequireClassicExecutableOrArchives()
    {
        foreach (string file in new[] { "main.zzz", "other.zzz", "FFVIII_EN.exe.dat" })
        {
            File.WriteAllText(Path.Combine(directory, file), "");
        }

        Assert.Null(GameInstallationSelection.ValidateForEngine(directory));
        Assert.False(File.Exists(Path.Combine(directory, "FF8_EN.exe")));
        Assert.False(Directory.Exists(Path.Combine(directory, "Data")));
    }

    [Theory]
    [InlineData("main.zzz")]
    [InlineData("other.zzz")]
    [InlineData("FFVIII_EN.exe.dat")]
    public void RemasteredValidationNamesEachMissingRequiredFile(string missing)
    {
        foreach (string file in new[] { "main.zzz", "other.zzz", "FFVIII_EN.exe.dat" })
        {
            if (file != missing)
            {
                File.WriteAllText(Path.Combine(directory, file), "");
            }
        }

        string error = Assert.IsType<string>(GameInstallationSelection.ValidateForEngine(directory));

        Assert.Contains("Remastered", error);
        Assert.Contains(missing, error);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Installed tools must not access the network.");
        }
    }
    [Theory]
    [InlineData(HostPlatform.Windows, "Memoria.FF8.exe")]
    [InlineData(HostPlatform.Linux, "Memoria.FF8")]
    [InlineData(HostPlatform.MacOS, "Memoria.FF8")]
    public void LaunchFactoryUsesPlatformExecutableAndWritableModPolicy(HostPlatform platform, string executable)
    {
        string package = Path.Combine(directory, "package.app", "Contents", "MacOS");
        string configuration = Path.Combine(directory, "user configuration");
        ProcessStartInfo start = GameInstallationSelection.CreateStartInfo(package, directory, hostPlatform: platform, configurationDirectory: configuration);
        Assert.Equal(Path.Combine(package, "Data", executable), start.FileName);
        Assert.Equal(Path.Combine(platform == HostPlatform.MacOS ? configuration : package, "Mods"), start.ArgumentList[3]);
        Assert.Equal(Path.Combine(package, "Data"), start.WorkingDirectory);
    }
}
