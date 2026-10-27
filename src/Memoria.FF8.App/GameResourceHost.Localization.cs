using System.Diagnostics;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Mods;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    private readonly CancellationTokenSource localizationCancellation = new();
    private readonly LocalizationProjectReadCache localizationReadCache = new();
    private Task<LocalizationPreparation>? localizationTask;
    private string? localizationInput;
    private string? observedLocalizationInput;
    private DateTime localizationObservedAt;
    private IReadOnlyDictionary<string, LocalizationSourceEntry>? knownLocalizationEntries;
    private IReadOnlyDictionary<Memoria.FF8.Resources.TextureKey, LocalizationArtworkSource>? knownLocalizationArtwork;
    public LocalizationPreparation? LocalizationPreview { get; private set; }
    public LocalizationPreparation? CommittedLocalizations { get; private set; }
    public double LastLocalizationPublicationMilliseconds { get; private set; }
    public int LastLocalizationChangedKeys { get; private set; }
    internal long ParsedLocalizationPayloads => localizationReadCache.ParsedPayloads;
    internal long ReusedLocalizationPayloads => localizationReadCache.CacheHits;

    private bool IsLocalizationOnlyChange(ModCatalogSnapshot candidate)
    {
        if (Mods.Catalog.Generation == 0)
        {
            return false;
        }
        var before = Mods.Catalog.Files.Where(file => !file.RelativePath.StartsWith("Localization/", StringComparison.OrdinalIgnoreCase)).Select(file => (file.PackageId, file.RelativePath, file.Revision));
        var after = candidate.Files.Where(file => !file.RelativePath.StartsWith("Localization/", StringComparison.OrdinalIgnoreCase)).Select(file => (file.PackageId, file.RelativePath, file.Revision));
        var beforePackages = Mods.Catalog.Packages.Select(package => (package.Id, package.Status, Metadata: System.Text.Json.JsonSerializer.Serialize(package.JunctionMetadata)));
        var afterPackages = candidate.Packages.Select(package => (package.Id, package.Status, Metadata: System.Text.Json.JsonSerializer.Serialize(package.JunctionMetadata)));
        return before.SequenceEqual(after) && beforePackages.SequenceEqual(afterPackages);
    }

    private ModPreparationResult PrepareLocalizationPublication(LocalizationPreparation localization)
    {
        var previous = Resources.Localization;
        bool changed = previous.Fingerprint != localization.Catalog.Fingerprint;
        var prepared = changed ? Resources.PrepareLocalization(localization.Catalog) : null;
        return new(true, Mods.Catalog.Packages.Where(package => package.Status == ModEffectiveStatus.Active && !package.LocalizationOnly).Select(package => package.Id).Concat(localization.Layers.Select(layer => layer.PackageId)).ToArray(), Commit: () =>
        {
            Stopwatch clock = Stopwatch.StartNew();
            localization.InputRevision?.ValidatePublication();
            prepared?.Commit(notify: false);
            CommittedLocalizations?.InputRevision?.Dispose();
            CommittedLocalizations = localization;
            LastLocalizationChangedKeys = localization.ChangedKeys;
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
        string membership = string.Join(';', order ?? []) + "\n" + string.Join('\n', candidate.Packages.Where(package => package.HasLocalization).Select(package => package.Id + ":" + package.RootDirectory + ":" + package.Status));
        ModFile[] files = candidate.Files.Where(file => file.RelativePath.StartsWith("Localization/", StringComparison.OrdinalIgnoreCase)).ToArray();
        string identity = membership + "\n" + string.Join('\n', files.Select(file => file.PackageId + ":" + file.RelativePath + ":" + file.Revision));
        string physicalIdentity = membership + "\n" + string.Join('\n', files.Select(file => file.PackageId + ":" + file.RelativePath + ":" + (file.ContentStamp?.ToString() ?? file.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        if (physicalIdentity != observedLocalizationInput)
        {
            observedLocalizationInput = physicalIdentity;
            localizationObservedAt = DateTime.UtcNow;
        }
        if (identity != localizationInput)
        {
            if (localizationTask is { IsCompleted: false })
            {
                return new(false) { Deferred = true };
            }
            if (localizationTask?.IsCompletedSuccessfully == true && !ReferenceEquals(localizationTask.Result, CommittedLocalizations))
            {
                localizationTask.Result.InputRevision?.Dispose();
            }
            _ = localizationTask?.Exception;
            localizationInput = identity;
            bool stableRemoval = DateTime.UtcNow - localizationObservedAt >= TimeSpan.FromSeconds(1) && candidate.Packages.Where(package => package.HasLocalization && package.Status == ModEffectiveStatus.Active).All(package => CommittedLocalizations?.Layers.Any(layer => layer.PackageId == package.Id) == true);
            localizationTask = Task.Run(() =>
            {
                Stopwatch clock = Stopwatch.StartNew();
                var preparation = LocalizationRuntime.Prepare(candidate, order, source.Descriptor, localizationCancellation.Token, stableRemoval, watchForPublication: true, readCache: localizationReadCache);
                try
                {
                    if (!stableRemoval && CommittedLocalizations is { } committed)
                    {
                        foreach (LocalizationLayer layer in preparation.Layers)
                        {
                            var oldArtwork = committed.Layers.FirstOrDefault(old => old.PackageId == layer.PackageId)?.Project.Artwork;
                            if (oldArtwork is not null && layer.Project.Artwork.Any(file => file.TargetPng is null && file.Declaration.TargetImage is not null && oldArtwork.Any(old => old.Declaration.Key == file.Declaration.Key && old.Declaration.TargetImage == file.Declaration.TargetImage && old.TargetPng is not null)))
                            {
                                throw new IOException("An artwork Target was removed during saving. Wait for a stable deletion or finish saving, then retry.");
                            }
                        }
                    }
                    if (!preparation.RequiresSelection && (preparation.Catalog.Values.Count != 0 || preparation.Catalog.Artwork.Count != 0))
                    {
                        using var verification = GameDataSourceFactory.Open(gameDirectory);
                        if (preparation.Layers.Any(layer => layer.Project.Entries.Any(row => !string.IsNullOrEmpty(row.Target) && row.Category is not ("Fields" or "System"))))
                        {
                            knownLocalizationEntries ??= LocalizationSourceExporter.ReadNonFields(gameDirectory, source.Descriptor.Language, localizationCancellation.Token).Entries.ToDictionary(row => row.Key, StringComparer.Ordinal);
                        }
                        LocalizationBindingValidation.Validate(preparation, verification, localizationCancellation.Token, knownLocalizationEntries);
                        if (preparation.Catalog.Artwork.Count != 0)
                        {
                            knownLocalizationArtwork ??= LocalizationArtworkExporter.Read(gameDirectory, source.Descriptor.Language, localizationCancellation.Token).ToDictionary(image => image.Key);
                            foreach (var image in preparation.Catalog.Artwork.Values)
                            {
                                if (!knownLocalizationArtwork.TryGetValue(image.Key, out var native) || !native.SourceSha256.Equals(image.SourceSha256, StringComparison.OrdinalIgnoreCase) || native.Width != image.LogicalWidth || native.Height != image.LogicalHeight)
                                {
                                    throw new InvalidDataException($"{image.Key}: artwork does not match a supported original image in this resource profile.");
                                }
                            }
                        }
                    }
                    var previous = Resources.Localization;
                    int changedKeys = previous.Values.Keys.Union(preparation.Catalog.Values.Keys).Count(key => previous.Values.GetValueOrDefault(key)?.Text != preparation.Catalog.Values.GetValueOrDefault(key)?.Text);
                    preparation.InputRevision?.Validate();
                    return preparation with { PreparationMilliseconds = clock.Elapsed.TotalMilliseconds, ChangedKeys = changedKeys };
                }
                catch
                {
                    preparation.InputRevision?.Dispose();
                    throw;
                }
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
