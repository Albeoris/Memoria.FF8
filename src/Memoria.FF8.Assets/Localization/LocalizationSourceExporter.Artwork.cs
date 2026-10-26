using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Localization;

public static partial class LocalizationSourceExporter
{
    public static IReadOnlyList<LocalizationArtworkSource> ReadArtwork(string gameDirectory, string language, CancellationToken cancellation = default)
    {
        using IResourceSource source = Open(gameDirectory, FieldLocalizationKey.CanonicalLanguage(language));
        return LocalizationArtworkExporter.Read((IGameArchiveSource)source, cancellation);
    }
}
