using System.Diagnostics;
using System.Net.Http;
using Memoria.FF8;

namespace Memoria.Launcher.Tests;

public sealed class LauncherTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.Launcher.Tests", Guid.NewGuid().ToString("N"));

    public LauncherTests()
    {
        Directory.CreateDirectory(directory);
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
    public void LaunchArgumentsKeepPathsWithSpacesAsSeparateValues()
    {
        string package = Path.Combine(directory, "Memoria package");
        string game = Path.Combine(directory, "Game files");

        ProcessStartInfo result = GameInstallationSelection.CreateStartInfo(package, game);

        Assert.Equal(Path.Combine(package, "Data", "Memoria.FF8.exe"), result.FileName);
        Assert.Equal(Path.Combine(package, "Data"), result.WorkingDirectory);
        Assert.Equal(new[] { "--game-dir", game, "--media-tools", Path.Combine(package, "Data", "media-tools") }, result.ArgumentList);
        Assert.False(result.UseShellExecute);
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
}
