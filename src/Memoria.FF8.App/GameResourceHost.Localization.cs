using System.Diagnostics;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    private readonly CancellationTokenSource localizationCancellation = new();
    private Task<LocalizationPreparation>? localizationTask;
    private string? localizationInput;
    private string? observedLocalizationInput;
    private DateTime localizationObservedAt;
    public LocalizationPreparation? LocalizationPreview { get; private set; }
    public LocalizationPreparation? CommittedLocalizations { get; private set; }
    public double LastLocalizationPublicationMilliseconds { get; private set; }
    public int LastLocalizationChangedKeys { get; private set; }

    private bool IsLocalizationOnlyChange(ModCatalogSnapshot candidate)
    {
        if (Mods.Catalog.Generation == 0)
        {
            return false;
        }
        var before = Mods.Catalog.Files.Where(file => !file.RelativePath.StartsWith("Localization/", StringComparison.OrdinalIgnoreCase)).Select(file => (file.PackageId, file.RelativePath, file.Revision));
        var after = candidate.Files.Where(file => !file.RelativePath.StartsWith("Localization/", StringComparison.OrdinalIgnoreCase)).Select(file => (file.PackageId, file.RelativePath, file.Revision));
        return before.SequenceEqual(after);
    }

    private ModPreparationResult PrepareLocalizationPublication(LocalizationPreparation localization)
    {
        var previous = Resources.Localization;
        bool changed = previous.Fingerprint != localization.Catalog.Fingerprint;
        var prepared = changed ? Resources.PrepareLocalization(localization.Catalog) : null;
        return new(true, Mods.Catalog.Packages.Where(package => package.Status == ModEffectiveStatus.Active && !package.LocalizationOnly).Select(package => package.Id).Concat(localization.Layers.Select(layer => layer.PackageId)).ToArray(), Commit: () =>
        {
            Stopwatch clock = Stopwatch.StartNew();
            localization.InputRevision?.Validate();
            prepared?.Commit(notify: false);
            CommittedLocalizations = localization;
            LastLocalizationChangedKeys = previous.Values.Keys.Union(localization.Catalog.Values.Keys).Count(key => previous.Values.GetValueOrDefault(key)?.Text != localization.Catalog.Values.GetValueOrDefault(key)?.Text);
            LastLocalizationPublicationMilliseconds = clock.Elapsed.TotalMilliseconds;
        }, AfterCommit: prepared is null ? null : prepared.Notify);
    }

    private ModPreparationResult? PrepareLocalization(ModCatalogSnapshot candidate, out LocalizationPreparation? prepared)
    {
        prepared = null;
        IReadOnlyList<string>? order = Mods.LocalizationOrder;
        if (!candidate.Packages.Any(package => package.HasLocalization && package.Status == ModEffectiveStatus.Active))
        {
            prepared = new(LocalizationCatalog.Empty, [], [], false, 0);
            LocalizationPreview = prepared;
            return null;
        }
        string identity = string.Join(';', order ?? []) + "\n" + string.Join('\n', candidate.Packages.Where(package => package.HasLocalization).Select(package => package.Id + ":" + package.Status)) + "\n" + string.Join('\n', candidate.Files.Where(file => file.RelativePath.StartsWith("Localization/", StringComparison.OrdinalIgnoreCase)).Select(file => file.PackageId + ":" + file.RelativePath + ":" + file.Revision));
        if (identity != observedLocalizationInput)
        {
            observedLocalizationInput = identity;
            localizationObservedAt = DateTime.UtcNow;
        }
        if (identity != localizationInput)
        {
            if (localizationTask is { IsCompleted: false })
            {
                return new(false) { Deferred = true };
            }
            _ = localizationTask?.Exception;
            localizationInput = identity;
            bool stableRemoval = DateTime.UtcNow - localizationObservedAt >= TimeSpan.FromSeconds(1) && candidate.Packages.Where(package => package.HasLocalization && package.Status == ModEffectiveStatus.Active).All(package => CommittedLocalizations?.Layers.Any(layer => layer.PackageId == package.Id) == true);
            localizationTask = Task.Run(() =>
            {
                Stopwatch clock = Stopwatch.StartNew();
                var preparation = LocalizationRuntime.Prepare(candidate, order, source.Descriptor, localizationCancellation.Token, stableRemoval);
                if (!preparation.RequiresSelection && preparation.Catalog.Values.Count != 0)
                {
                    using var verification = GameDataSourceFactory.Open(gameDirectory);
                    LocalizationBindingValidation.Validate(preparation, verification, localizationCancellation.Token);
                }
                return preparation with { PreparationMilliseconds = clock.Elapsed.TotalMilliseconds };
            }, localizationCancellation.Token);
        }
        if (!localizationTask!.IsCompleted)
        {
            return new(false) { Deferred = true };
        }
        try
        {
            prepared = localizationTask.GetAwaiter().GetResult();
            LocalizationPreview = prepared;
            if (prepared.RequiresSelection)
            {
                return new(false, Diagnostics: [new("localization-selection-required", "Choose active localizations and their priority order. The previous stack remains active.")]);
            }
            return null;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or InvalidOperationException or NotSupportedException or FormatException or OverflowException)
        {
            localizationInput = null;
            return new(false, Diagnostics: [new("localization-invalid", exception.Message)]);
        }
    }
}
