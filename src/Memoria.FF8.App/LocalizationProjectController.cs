using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8;

internal static class LocalizationProjectController
{
    internal static LocalizationAuthoringServices CreateServices(string gameDirectory, string memoriaRoot, Action? refresh = null, Func<IReadOnlyList<string>>? readTargetIds = null, Func<IReadOnlyList<LocalizationLayer>>? readLoaded = null, Func<IReadOnlyList<string>>? readProfileOrder = null)
    {
        IReadOnlyList<LocalizationSourceAvailability>? available = null;
        IReadOnlyList<LocalizationSourceAvailability> Availability() => available ??= LocalizationSourceExporter.ProbeLanguages(gameDirectory);
        IReadOnlyList<LocalizationLayer> Loaded() => readLoaded?.Invoke() ?? [];
        IReadOnlyList<LocalizationDatasetOption> Datasets() => Availability().Select(language => new LocalizationDatasetOption(language.Language, language.Language, language.Available, language.Reason)).ToArray();
        return new(memoriaRoot,
            Datasets,
            (request, cancellation, progress) => LocalizationProjectBuilder.Prepare(gameDirectory, request, cancellation, progress),
            refresh,
            () => Availability().Select(language => language.Language).Concat(["ja"]).Concat(readTargetIds?.Invoke() ?? []).Concat(Loaded().Select(layer => layer.Project.Manifest.TargetLanguage)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

}
