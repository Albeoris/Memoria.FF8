using System.Diagnostics;
using System.Collections.Immutable;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public sealed record LocalizationLayer(string PackageId, string Name, LocalizationProject Project);
public sealed record LocalizationPreparation(LocalizationCatalog Catalog, IReadOnlyList<LocalizationLayer> Layers, IReadOnlyList<string> RequestedOrder, bool RequiresSelection, double PreparationMilliseconds)
{
    public LocalizationInputRevision? InputRevision { get; init; }
    public IReadOnlyList<string> DormantFieldKeys { get; init; } = [];
    public int ChangedKeys { get; init; }
}

public static class LocalizationRuntime
{
    /// <summary>Reads a complete candidate off the publication thread. The displayed order is low to high.</summary>
    public static LocalizationPreparation Prepare(ModCatalogSnapshot candidate, IReadOnlyList<string>? acceptedOrder, ResourceSourceDescriptor source, CancellationToken cancellation = default, bool allowMissingPayloads = false, bool watchForPublication = false, LocalizationProjectReadCache? readCache = null)
    {
        LocalizationInputRevision inputs = LocalizationInputRevision.Capture(candidate.Packages.Where(package => package.Status == ModEffectiveStatus.Active && package.HasLocalization).Select(package => package.RootDirectory), watchForPublication);
        try
        {
            return PrepareCore(candidate, acceptedOrder, source, cancellation, allowMissingPayloads, inputs, readCache);
        }
        catch
        {
            inputs.Dispose();
            throw;
        }
    }

    private static LocalizationPreparation PrepareCore(ModCatalogSnapshot candidate, IReadOnlyList<string>? acceptedOrder, ResourceSourceDescriptor source, CancellationToken cancellation, bool allowMissingPayloads, LocalizationInputRevision inputs, LocalizationProjectReadCache? readCache)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        List<LocalizationLayer> layers = [];
        foreach (ModPackage package in candidate.Packages.Where(package => package.Status == ModEffectiveStatus.Active && package.HasLocalization))
        {
            cancellation.ThrowIfCancellationRequested();
            LocalizationProject project = LocalizationProjectIO.Read(package.RootDirectory, allowMissingPayloads, readCache);
            if (project.Manifest.MappingVersion != FieldCorrespondenceCatalog.Version)
            {
                throw new InvalidDataException($"{package.Name}: localization mapping version is not supported.");
            }
            string variant = source.Edition + ":" + source.Language;
            string activeBase = source.Language.Equals("ja", StringComparison.OrdinalIgnoreCase) ? "jp" : source.Language;
            string declaredBase = project.Manifest.FieldScriptBaseLanguage.Equals("ja", StringComparison.OrdinalIgnoreCase) ? "jp" : project.Manifest.FieldScriptBaseLanguage;
            if (project.Manifest.SchemaVersion >= 2 && !declaredBase.Equals(activeBase, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"{package.Name}: localization base {declaredBase} does not match active resource language {activeBase}.");
            }
            if (project.Manifest.CompatibleVariants.Count != 0 && !project.Manifest.CompatibleVariants.Contains(variant, StringComparer.OrdinalIgnoreCase) && !project.Manifest.CompatibleVariants.Contains(source.Language == "ja" ? "jp" : source.Language, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"{package.Name}: resource variant {variant} is not declared compatible.");
            }
            layers.Add(new(package.Id, package.Name, project));
        }
        if (layers.Select(layer => layer.Project.Manifest.ProjectId).Distinct(StringComparer.Ordinal).Count() != layers.Count)
        {
            throw new InvalidDataException("Two active localization packages have the same project identity.");
        }
        string[] requested = DefaultOrder(layers);
        bool selection = acceptedOrder is null ? layers.Count > 1 : requested.Any(id => !acceptedOrder.Contains(id, StringComparer.OrdinalIgnoreCase));
        string[] order = acceptedOrder is null ? requested : acceptedOrder.Where(id => requested.Contains(id, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (layers.Count == 1 && order.Length == 0 && acceptedOrder is null)
        {
            order = requested;
        }
        List<string> dormant = [];
        Dictionary<TextureKey, LocalizationArtworkValue> artwork = [];
        Dictionary<string, LocalizationValue> values = new(StringComparer.Ordinal);
        List<IReadOnlyDictionary<string, LocalizationValue>> priorityLayers = [];
        foreach (string packageId in order)
        {
            LocalizationLayer layer = layers.Single(layer => layer.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            foreach (LocalizationArtworkFile file in layer.Project.Artwork.Where(file => file.TargetPng is not null))
            {
                var image = readCache?.ReadArtworkImage(file.TargetPng!, file.Declaration.TargetImage!) ?? DecodeArtworkTarget(file);
                artwork[file.Declaration.Key] = new(file.Declaration.Key, file.Declaration.Width, file.Declaration.Height, image.Width, image.Height, image.Pixels, file.Declaration.SourceSha256, layer.PackageId, layer.Project.Manifest.ProjectId);
            }
            Dictionary<string, LocalizationValue> contribution = new(StringComparer.Ordinal);
            foreach (LocalizationRecord row in layer.Project.Entries)
            {
                cancellation.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(row.Target))
                {
                    continue;
                }
                ValidateTarget(row);
                LocalizationValue value = new(row.Target, layer.PackageId, layer.Project.Manifest.ProjectId) { Field = row.Category == "Fields" ? LocalizationFieldPreparation.Prepare(row) : null };
                if (row.Category == "Fields" && value.Field is null)
                {
                    dormant.Add(row.Key);
                }
                values[row.Key] = value;
                contribution.Add(row.Key, value);
            }
            Dictionary<string, LocalizationRecord> records = layer.Project.Entries.ToDictionary(row => row.Key, StringComparer.Ordinal);
            string activeLanguage = source.Language == "ja" ? "jp" : source.Language;
            foreach (LocalizationRecord row in layer.Project.Entries.Where(row => row.Category == "Fields" && !string.IsNullOrEmpty(row.Target) && !row.Key.Contains("_ctx_", StringComparison.Ordinal) && !row.Key.StartsWith("$" + activeLanguage + "_", StringComparison.Ordinal)))
            {
                string ownLanguage = row.Key[1..row.Key.IndexOf('_')];
                IReadOnlyList<string> nativeKeys = FieldCorrespondenceCatalog.GetEquivalents(row.Key, activeLanguage);
                if (nativeKeys.Count != 1 || FieldCorrespondenceCatalog.GetEquivalents(row.Key, ownLanguage).Count != 1 || !records.TryGetValue(nativeKeys[0], out LocalizationRecord? nativeRow) || !string.IsNullOrEmpty(nativeRow.Target))
                {
                    continue;
                }
                Dictionary<string, System.Text.Json.JsonElement> reboundMetadata = new(nativeRow.ExtensionData, StringComparer.Ordinal);
                if (row.ExtensionData.TryGetValue("Choice Line Map", out var authoredLineMap))
                {
                    reboundMetadata["Choice Line Map"] = authoredLineMap;
                }
                LocalizationValue rebound = new(row.Target!, layer.PackageId, layer.Project.Manifest.ProjectId) { Field = LocalizationFieldPreparation.Prepare(nativeRow with { Target = row.Target, ExtensionData = reboundMetadata }) };
                if (contribution.TryGetValue(nativeRow.Key, out LocalizationValue? previous) && previous.Text != rebound.Text)
                {
                    throw new InvalidDataException($"{nativeRow.Key}: conflicting translations in a shared correspondence group require an explicit native-key translation.");
                }
                contribution[nativeRow.Key] = rebound;
                values[nativeRow.Key] = rebound;
            }
            priorityLayers.Add(contribution);
        }
        inputs.Validate();
        return new(new LocalizationCatalog(values, priorityLayers, artwork), layers.AsReadOnly(), order, selection, elapsed.Elapsed.TotalMilliseconds) { InputRevision = inputs, DormantFieldKeys = dormant.AsReadOnly() };
    }

    private static LocalizationProjectReadCache.ArtworkImage DecodeArtworkTarget(LocalizationArtworkFile file)
    {
        var image = TextureImageDecoder.Decode(file.TargetPng!, file.Declaration.TargetImage!).Image;
        return new(image.Width, image.Height, image.Pixels.ToImmutableArray());
    }

    private static void ValidateTarget(LocalizationRecord row)
    {
        // Runtime controls and placeholders retain their own syntax; source/reference cells never override presentation.
        if (row.Target!.Contains('\0'))
        {
            throw new InvalidDataException($"{row.Key}: translated text contains NUL.");
        }
        if (row.Category == "Fields")
        {
            int suffixStart = row.Key.IndexOf('_') + row.File.Length + 2;
            int contextStart = row.Key.IndexOf("_ctx_", suffixStart, StringComparison.Ordinal);
            string baseKey = contextStart < 0 ? row.Key : row.Key[..contextStart];
            if (!FieldCorrespondenceCatalog.Contains(baseKey))
            {
                throw new InvalidDataException($"{row.Key}: unknown field source identity.");
            }
        }
    }

    private static string[] DefaultOrder(IReadOnlyList<LocalizationLayer> layers)
    {
        Dictionary<string, LocalizationLayer> projects = layers.ToDictionary(layer => layer.Project.Manifest.ProjectId, StringComparer.Ordinal);
        HashSet<string> visiting = new(StringComparer.Ordinal);
        HashSet<string> added = new(StringComparer.Ordinal);
        List<string> result = [];
        void Add(LocalizationLayer layer)
        {
            if (added.Contains(layer.PackageId))
            {
                return;
            }
            if (!visiting.Add(layer.PackageId))
            {
                throw new InvalidDataException("Localization base relationships contain a cycle.");
            }
            if (layer.Project.Manifest.BaseProjectId is string baseId && projects.TryGetValue(baseId, out LocalizationLayer? parent))
            {
                Add(parent);
            }
            visiting.Remove(layer.PackageId);
            added.Add(layer.PackageId);
            result.Add(layer.PackageId);
        }
        foreach (LocalizationLayer layer in layers)
        {
            Add(layer);
        }
        return result.ToArray();
    }
}
