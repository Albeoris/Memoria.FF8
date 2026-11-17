using Memoria.FF8.Platform;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.Launcher.Tests;

public sealed class InstalledResourceLanguageTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.Language.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ClassicUsesItsInstalledProviderLanguageAndClearsAnOlderOverride()
    {
        Directory.CreateDirectory(Path.Combine(directory, "Data", "lang-en"));
        File.WriteAllBytes(Path.Combine(directory, "FF8_EN.exe"), []);
        foreach (string family in new[] { "main", "menu", "field", "battle", "world", "magic" })
        {
            foreach (string extension in new[] { ".fl", ".fi", ".fs" })
            {
                File.WriteAllBytes(Path.Combine(directory, "Data", "lang-en", family + extension), []);
            }
        }
        string settingsPath = Path.Combine(directory, "launcher.json");
        new LauncherSettings(directory, ResourceLanguage: "fr").Save(settingsPath);
        LauncherSettings settings = LauncherSettings.Load(settingsPath);
        InstalledResourceLanguages languages = GameInstallationResourceLanguages.Discover(directory, TestContext.Current.CancellationToken);
        Assert.Equal(GameDataEdition.Classic, languages.Edition);
        Assert.Equal(["en"], languages.AvailableLanguages);
        Assert.False(languages.CanSelectLanguage);
        Assert.Equal("en", languages.Resolve(settings.ResourceLanguage));
        (settings with { ResourceLanguage = languages.NormalizeOverride(settings.ResourceLanguage) }).Save(settingsPath);
        Assert.Null(LauncherSettings.Load(settingsPath).ResourceLanguage);
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
        Assert.Equal(["en", "fr"], languages.AvailableLanguages);
        Assert.True(languages.CanSelectLanguage);
        Assert.Equal("fr", languages.NormalizeOverride(" FR "));
        Assert.Equal("en", languages.Resolve(null));
        Assert.Equal("en", languages.Resolve("de"));
        Assert.Null(languages.NormalizeOverride("unknown"));
    }

    [Fact]
    public void RemasteredSingleBankUsesThatLanguageWithoutAnOverrideSelector()
    {
        InstalledResourceLanguages languages = GameInstallationResourceLanguages.FromRemasteredAvailability([new("fr", true, null), new("en", false, "Missing bank")]);
        Assert.Equal("fr", languages.Resolve("en"));
        Assert.False(languages.CanSelectLanguage);
        Assert.Null(languages.NormalizeOverride("en"));
    }

    [Fact]
    public void RemasteredWithoutBanksDoesNotFallBackToEnglish()
    {
        InstalledResourceLanguages languages = GameInstallationResourceLanguages.FromRemasteredAvailability([new("en", false, "Missing bank")]);
        Assert.Empty(languages.AvailableLanguages);
        Assert.Null(languages.Resolve(null));
        Assert.False(languages.CanSelectLanguage);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
