using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Platform;
using Memoria.FF8.Resources;

namespace Memoria.Launcher;

internal sealed record InstalledResourceLanguages(GameDataEdition Edition, string? DetectedLanguage, IReadOnlyList<string> AvailableLanguages)
{
    public bool CanSelectLanguage => Edition == GameDataEdition.Remastered && AvailableLanguages.Count > 1;

    public string? NormalizeOverride(string? language)
    {
        if (Edition != GameDataEdition.Remastered || string.IsNullOrWhiteSpace(language))
        {
            return null;
        }
        string normalized = language.Trim().ToLowerInvariant();
        return AvailableLanguages.Contains(normalized, StringComparer.Ordinal) ? normalized : null;
    }

    public string? Resolve(string? language) => NormalizeOverride(language) ?? DetectedLanguage;
}

internal static class GameInstallationResourceLanguages
{
    public static InstalledResourceLanguages Discover(string gameDirectory, CancellationToken cancellation = default)
    {
        GameDataEdition edition = GameInstallationSelection.ResolveEdition(gameDirectory);
        if (GameInstallationSelection.ValidateForEngine(gameDirectory) is not null)
        {
            return new(edition, null, []);
        }
        if (edition == GameDataEdition.Classic)
        {
            using ClassicGameDataSource source = new(gameDirectory);
            return new(edition, source.Descriptor.Language, [source.Descriptor.Language]);
        }
        return FromRemasteredAvailability(LocalizationSourceExporter.ProbeLanguages(gameDirectory, cancellation));
    }

    internal static InstalledResourceLanguages FromRemasteredAvailability(IEnumerable<Memoria.FF8.Resources.Localization.LocalizationSourceAvailability> availability)
    {
        HashSet<string> present = availability.Where(language => language.Available).Select(language => language.Language).ToHashSet(StringComparer.Ordinal);
        string[] languages = GameResourceLanguages.Supported.Where(present.Contains).ToArray();
        string? detected = languages.Contains("en", StringComparer.Ordinal) ? "en" : languages.FirstOrDefault();
        return new(GameDataEdition.Remastered, detected, languages);
    }
}
