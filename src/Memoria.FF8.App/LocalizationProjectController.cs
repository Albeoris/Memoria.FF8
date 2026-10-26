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
        IReadOnlyList<LocalizationDatasetOption> Datasets()
        {
            IReadOnlyList<LocalizationLayer> loaded = Loaded();
            List<LocalizationDatasetOption> datasets = Availability().Select(language => new LocalizationDatasetOption(language.Language, language.Language, language.Available, language.Reason)).ToList();
            datasets.AddRange(loaded.Select(layer => new LocalizationDatasetOption(LocalizationProjectBuilder.ProjectDatasetId(layer.Project), layer.Name + " · " + layer.Project.Manifest.TargetLanguage + " (own contribution)", ScriptBaseAvailable: false)));
            if (loaded.Count > 0)
            {
                bool active = readProfileOrder is null || readProfileOrder().Count > 0;
                datasets.Add(new(LocalizationProjectBuilder.EffectiveProfileDatasetId, "Effective active profile", active, active ? null : "Choose an active localization stack first.", ScriptBaseAvailable: false));
            }
            return datasets;
        }
        return new(memoriaRoot,
            Datasets,
            (request, cancellation, progress) => LocalizationProjectBuilder.Prepare(gameDirectory, request, cancellation, progress, Loaded().ToArray(), readProfileOrder?.Invoke().ToArray()),
            refresh,
            () => Availability().Select(language => language.Language).Concat(["ja"]).Concat(readTargetIds?.Invoke() ?? []).Concat(Loaded().Select(layer => layer.Project.Manifest.TargetLanguage)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

}
