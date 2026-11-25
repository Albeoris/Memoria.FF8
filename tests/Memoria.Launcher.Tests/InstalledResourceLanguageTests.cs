using Memoria.FF8.Platform;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.Launcher.Tests;

public sealed class InstalledResourceLanguageTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.Language.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ClassicUsesItsInstalledBaseWithAnOlderRemasteredOverridePresent()
    {
        WriteClassicArchives("en");
        File.WriteAllBytes(Path.Combine(directory, "FF8_EN.exe"), []);
        string settingsPath = Path.Combine(directory, "launcher.json");
        new LauncherSettings(directory, ResourceLanguage: "fr").Save(settingsPath);
        LauncherSettings settings = LauncherSettings.Load(settingsPath);
        InstalledResourceLanguages languages = GameInstallationResourceLanguages.Discover(directory, TestContext.Current.CancellationToken);
        Assert.Equal(GameDataEdition.Classic, languages.Edition);
        Assert.Equal(["en"], languages.AvailableLanguages);
        Assert.Equal("en", languages.Resolve(settings, directory));
    }

    [Fact]
    public void UnsupportedClassicExecutableStillExposesItsActualGermanArchivesToTheLanguagePicker()
    {
        WriteClassicArchives("de");
        File.WriteAllBytes(Path.Combine(directory, "FF8_DE.exe"), []);
        Assert.NotNull(GameInstallationSelection.ValidateForEngine(directory));
        InstalledResourceLanguages languages = GameInstallationResourceLanguages.Discover(directory, TestContext.Current.CancellationToken);
        Assert.Equal(new GameLanguageProfile("Classic", "de"), Assert.Single(languages.NativeProfiles));
        Assert.Equal("de", languages.Resolve(null));
    }

    [Fact]
    public void MissingClassicFilesDoNotInventADetectedLanguage()
    {
        Directory.CreateDirectory(directory);
        InstalledResourceLanguages languages = GameInstallationResourceLanguages.Discover(directory, TestContext.Current.CancellationToken);
        Assert.Empty(languages.AvailableLanguages);
        Assert.Null(languages.Resolve("fr"));
    }

    [Fact]
    public void RemasteredOnlyOffersPresentSupportedBanksAndNormalizesSavedOverrides()
    {
        InstalledResourceLanguages languages = GameInstallationResourceLanguages.FromRemasteredAvailability([
            new("en", true, null),
            new("fr", true, null),
            new("de", false, "Missing bank"),
            new("jp", true, null)]);
        Assert.Equal(["en", "fr", "ja"], languages.AvailableLanguages);
        Assert.Equal("fr", languages.NormalizeOverride(" FR "));
        Assert.Equal("en", languages.Resolve(null));
        Assert.Equal("en", languages.Resolve("de"));
        Assert.Null(languages.NormalizeOverride("unknown"));
    }

    [Fact]
    public void RemasteredSingleBankUsesItsInstalledBase()
    {
        InstalledResourceLanguages languages = GameInstallationResourceLanguages.FromRemasteredAvailability([new("fr", true, null), new("en", false, "Missing bank")]);
        Assert.Equal("fr", languages.Resolve("en"));
        Assert.Equal(new GameLanguageProfile("Remastered", "fr"), Assert.Single(languages.NativeProfiles));
        Assert.Null(languages.NormalizeOverride("en"));
    }

    [Fact]
    public void RemasteredWithoutBanksDoesNotFallBackToEnglish()
    {
        InstalledResourceLanguages languages = GameInstallationResourceLanguages.FromRemasteredAvailability([new("en", false, "Missing bank")]);
        Assert.Empty(languages.AvailableLanguages);
        Assert.Null(languages.Resolve(null));
        Assert.Empty(languages.NativeProfiles);
    }

    [Fact]
    public void LaunchReadsTheSavedProviderBaseForItsOwnInstallation()
    {
        InstalledResourceLanguages languages = GameInstallationResourceLanguages.FromRemasteredAvailability([new("en", true, null), new("fr", true, null)]);
        LauncherSettings settings = new LauncherSettings(directory, ResourceLanguage: "en").WithLanguageChoice(new("ru", "memoria/russian-ffrtt", "ru", new("Remastered", "fr"), "Memoria/Translation"));
        LauncherSettings staleLegacyOverride = settings with { ResourceLanguage = "en" };
        string selected = Assert.IsType<string>(languages.Resolve(staleLegacyOverride, directory));
        var start = GameInstallationSelection.CreateStartInfo(directory, directory, resourceLanguage: selected);
        int argument = start.ArgumentList.IndexOf("--resource-language");
        Assert.True(argument >= 0);
        Assert.Equal("fr", start.ArgumentList[argument + 1]);
        Assert.Equal("en", languages.Resolve(settings, Path.Combine(directory, "other")));
    }

    private void WriteClassicArchives(string language)
    {
        string archives = Directory.CreateDirectory(Path.Combine(directory, "Data", "lang-" + language)).FullName;
        foreach (string family in new[] { "main", "menu", "field", "battle", "world", "magic" })
        {
            foreach (string extension in new[] { ".fl", ".fi", ".fs" })
            {
                File.WriteAllBytes(Path.Combine(archives, family + extension), []);
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
