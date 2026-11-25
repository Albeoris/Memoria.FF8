using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Platform;
using Memoria.FF8.Resources;

namespace Memoria.Launcher;

internal sealed record InstalledResourceLanguages(GameDataEdition Edition, string? DetectedLanguage, IReadOnlyList<string> AvailableLanguages)
{
    public string? NormalizeOverride(string? language)
    {
        if (Edition != GameDataEdition.Remastered || string.IsNullOrWhiteSpace(language))
        {
            return null;
        }
        string normalized = language.Trim().ToLowerInvariant();
        if (normalized == "jp")
        {
            normalized = "ja";
        }
        return AvailableLanguages.Contains(normalized, StringComparer.Ordinal) ? normalized : null;
    }

    public string? Resolve(string? language) => NormalizeOverride(language) ?? DetectedLanguage;

    public string? Resolve(LauncherSettings settings, string gameDirectory)
    {
        GameLanguageChoice? choice = settings.LanguageChoiceFor(gameDirectory);
        return Resolve(choice?.Profile.Edition == Edition.ToString() ? choice.Profile.ResourceLanguage : settings.LegacyResourceLanguageFor(gameDirectory));
    }

    public IReadOnlyList<GameLanguageProfile> NativeProfiles => (Edition == GameDataEdition.Classic && DetectedLanguage is { } classic ? [classic] : AvailableLanguages)
        .Select(language => new GameLanguageProfile(Edition.ToString(), language)).ToArray();
}

internal static class GameInstallationResourceLanguages
{
    public static InstalledResourceLanguages Discover(string gameDirectory, CancellationToken cancellation = default)
    {
        GameDataEdition edition = GameInstallationSelection.ResolveEdition(gameDirectory);
        if (edition == GameDataEdition.Classic)
        {
            IReadOnlyList<string> languages = GameResourceLanguages.DiscoverClassicArchives(gameDirectory);
            return new(edition, languages.FirstOrDefault(), languages);
        }
        if (GameInstallationSelection.ValidateForEngine(gameDirectory) is not null)
        {
            return new(edition, null, []);
        }
        return FromRemasteredAvailability(LocalizationSourceExporter.ProbeLanguages(gameDirectory, cancellation));
    }

    internal static InstalledResourceLanguages FromRemasteredAvailability(IEnumerable<Memoria.FF8.Resources.Localization.LocalizationSourceAvailability> availability)
    {
        HashSet<string> present = availability.Where(language => language.Available).Select(language => language.Language == "jp" ? "ja" : language.Language).ToHashSet(StringComparer.Ordinal);
        string[] languages = GameResourceLanguages.Supported.Where(present.Contains).ToArray();
        string? detected = languages.Contains("en", StringComparer.Ordinal) ? "en" : languages.FirstOrDefault();
        return new(GameDataEdition.Remastered, detected, languages);
    }
}
