using System.Text.Json;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public static partial class LocalizationProjectBuilder
{
    public static LocalizationProject Prepare(string gameDirectory, LocalizationAuthoringRequest request, CancellationToken cancellation = default, IProgress<string>? progress = null, IReadOnlyList<LocalizationLayer>? loadedProjects = null, IReadOnlyList<string>? profileOrder = null)
    {
        if (!BuiltInLanguages.Contains(request.FieldScriptBaseLanguage, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The field script base must identify an installed native script language, not a translation contribution.");
        }
        Dictionary<string, LocalizationSourceCatalog> catalogs = new(StringComparer.OrdinalIgnoreCase);
        foreach (string language in new[] { request.FieldScriptBaseLanguage, request.SourceLanguage }.Concat(request.ReferenceLanguages).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellation.ThrowIfCancellationRequested();
            progress?.Report("Reading " + language + " resources…");
            catalogs.Add(language, ReadDataset(gameDirectory, language, loadedProjects ?? [], profileOrder, cancellation));
        }
        foreach (string dataset in catalogs.Keys.ToArray())
        {
            LocalizationSourceCatalog catalog = catalogs[dataset];
            catalogs[dataset] = catalog with { Gaps = catalog.Gaps.Where(gap => gap.Category != "System" || gap.Resource != "authored-interface").ToArray() };
        }
        if (catalogs.TryGetValue("en", out LocalizationSourceCatalog? english))
        {
            catalogs["en"] = english with { Entries = english.Entries.Concat(SystemTextCatalog.SourceEntries()).ToArray(), Gaps = english.Gaps.Where(gap => gap.Category != "System" || gap.Resource != "authored-interface").ToArray() };
        }
        LocalizationProject project = CreateProject(request, catalogs, cancellation, includeSystem: true);
        string edition = File.Exists(Path.Combine(gameDirectory, "main.zzz")) ? "Remastered" : "Classic";
        Dictionary<string, JsonElement> manifestExtensions = new(project.Manifest.ExtensionData, StringComparer.Ordinal);
        manifestExtensions["sourceDatasets"] = JsonSerializer.SerializeToElement(catalogs.Keys.Select(dataset => new
        {
            Id = dataset,
            PrimaryReference = dataset == request.SourceLanguage,
            FieldScriptBase = dataset == request.FieldScriptBaseLanguage,
            Reference = project.Manifest.ReferenceLanguages.Values.Contains(dataset, StringComparer.OrdinalIgnoreCase),
            Contributors = (loadedProjects ?? []).Where(layer => dataset == ProjectDatasetId(layer.Project) || dataset == EffectiveProfileDatasetId && (profileOrder is null || profileOrder.Contains(layer.PackageId, StringComparer.OrdinalIgnoreCase))).Select(layer => new { layer.Project.Manifest.ProjectId, layer.Project.Manifest.TargetLanguage }).ToArray()
        }).ToArray());
        string scriptLanguage = request.FieldScriptBaseLanguage.Equals("ja", StringComparison.OrdinalIgnoreCase) ? "jp" : request.FieldScriptBaseLanguage.ToLowerInvariant();
        progress?.Report("Reading original lettering artwork…");
        IReadOnlyList<LocalizationArtworkFile> artwork = ReadArtwork(gameDirectory, scriptLanguage, cancellation);
        return project with { Manifest = project.Manifest with { CompatibleVariants = [edition + ":" + scriptLanguage], ExtensionData = manifestExtensions, Artwork = artwork.Select(file => file.Declaration).ToArray() }, Artwork = artwork };
    }

    public static LocalizationProject CreateProject(LocalizationAuthoringRequest request, IReadOnlyDictionary<string, LocalizationSourceCatalog> catalogs, CancellationToken cancellation = default, bool includeSystem = false)
    {
        if (string.IsNullOrWhiteSpace(request.TargetLanguage) || request.TargetLanguage.Any(char.IsControl))
        {
            throw new InvalidDataException("Target must be a nonempty identifier without control characters.");
        }
        if (string.IsNullOrWhiteSpace(request.PackageName) || request.PackageName.Any(char.IsControl))
        {
            throw new InvalidDataException("Package name must be nonempty and contain no control characters.");
        }
        string baseLanguage = request.FieldScriptBaseLanguage;
        if (!catalogs.TryGetValue(baseLanguage, out LocalizationSourceCatalog? baseCatalog))
        {
            throw new InvalidDataException("The selected base dataset is missing: " + baseLanguage);
        }
        Dictionary<string, ReferenceDatasetIndex> indexes = catalogs.ToDictionary(pair => pair.Key, pair => new ReferenceDatasetIndex(pair.Value), StringComparer.OrdinalIgnoreCase);
        string[] referenceOrder = new[] { request.SourceLanguage }.Concat(request.ReferenceLanguages).Append(baseLanguage).Where(language => !string.IsNullOrWhiteSpace(language)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Dictionary<string, string> referenceLanguages = referenceOrder.ToDictionary(language => "Reference [" + language + "]", language => language, StringComparer.Ordinal);
        IReadOnlyList<LocalizationSourceEntry> originals = includeSystem ? SystemTextCatalog.SourceEntries() : [];
        Dictionary<string, LocalizationSourceEntry> authored = originals.ToDictionary(row => row.Metadata["Authored ID"]!, StringComparer.Ordinal);
        IEnumerable<LocalizationSourceEntry> systemBase = originals.Select(row => baseLanguage.Equals("en", StringComparison.OrdinalIgnoreCase) ? row : row with
        {
            Key = "$" + (baseLanguage.Equals("ja", StringComparison.OrdinalIgnoreCase) ? "jp" : baseLanguage.ToLowerInvariant()) + "_" + row.File + "_" + row.Metadata["Authored ID"],
            Language = baseLanguage,
            Text = "",
            Metadata = new Dictionary<string, string?>(row.Metadata, StringComparer.Ordinal) { ["Source Present"] = "false" }
        });
        IEnumerable<LocalizationSourceEntry> identities = baseCatalog.Entries.Concat(systemBase).DistinctBy(row => (row.Category, row.File, row.Key)).OrderBy(row => row.Category, StringComparer.Ordinal).ThenBy(row => row.File, StringComparer.Ordinal).ThenBy(row => row.Key, StringComparer.Ordinal);
        List<LocalizationRecord> records = [];
        Dictionary<string, int> unmapped = new(StringComparer.Ordinal);
        foreach (LocalizationSourceEntry identity in identities)
        {
            cancellation.ThrowIfCancellationRequested();
            Dictionary<string, string?> references = new(StringComparer.Ordinal);
            Dictionary<string, bool> presence = new(StringComparer.Ordinal);
            Dictionary<string, IReadOnlyList<string>> referenceKeys = new(StringComparer.Ordinal);
            Dictionary<string, string[]> aliases = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, LocalizationSourceEntry[]> counterparts = new(StringComparer.OrdinalIgnoreCase);
            foreach (string language in referenceOrder)
            {
                string label = "Reference [" + language + "]";
                LocalizationSourceEntry[] matches;
                if (language.Equals(baseLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    matches = [identity];
                }
                else if (identity.Category == "System" && language.Equals("en", StringComparison.OrdinalIgnoreCase) && identity.Metadata.GetValueOrDefault("Authored ID") is string id && authored.TryGetValue(id, out LocalizationSourceEntry? original))
                {
                    matches = [original];
                }
                else
                {
                    matches = indexes.TryGetValue(language, out var index) ? DatasetEquivalentKeys(identity, language, index).Select(key => index.Exact.GetValueOrDefault(key)).OfType<LocalizationSourceEntry>().ToArray() : [];
                }
                counterparts[language] = matches;
                aliases[language] = matches.Select(row => row.Key).ToArray();
                referenceKeys[label] = aliases[language];
                bool present = matches.Any(IsPresent);
                references[label] = present ? string.Join("{Line}", matches.Where(IsPresent).Select(row => row.Text)) : null;
                presence[label] = present;
                if (matches.Length == 0)
                {
                    string family = language + ":" + identity.Category + "/" + identity.File;
                    unmapped[family] = unmapped.GetValueOrDefault(family) + 1;
                }
            }
            Dictionary<string, JsonElement> extensions = identity.Metadata.ToDictionary(pair => pair.Key, pair => MetadataValue(pair.Key, pair.Value), StringComparer.Ordinal);
            extensions["sourceContext"] = JsonSerializer.SerializeToElement(identity.Metadata);
            extensions["equivalentKeys"] = JsonSerializer.SerializeToElement(aliases);
            extensions["equivalentContexts"] = JsonSerializer.SerializeToElement(counterparts.Where(pair => pair.Value.Length > 1).ToDictionary(pair => pair.Key, pair => pair.Value.Select(entry => new { entry.Key, entry.Text, entry.Metadata }).ToArray()));
            if (identity.Category == "Fields" && identity.Metadata.ContainsKey("Base Key"))
            {
                Dictionary<string, LocalizationSourceEntry[]> contextAlternatives = counterparts.ToDictionary(pair => pair.Key, pair => pair.Value.SelectMany(entry => indexes.TryGetValue(pair.Key, out ReferenceDatasetIndex? index) && index.Fields.TryGetValue((entry.File, entry.Metadata.GetValueOrDefault("Base Key") ?? entry.Key), out LocalizationSourceEntry[]? alternatives) ? alternatives : new[] { entry }).DistinctBy(entry => entry.Key).ToArray());
                var ambiguousContexts = contextAlternatives.Where(pair => pair.Value.Length > 1 && pair.Value.Any(entry => entry.Metadata.ContainsKey("Base Key"))).ToDictionary(pair => pair.Key, pair => pair.Value.Select(entry => new { entry.Key, entry.Text, entry.Metadata }).ToArray());
                if (ambiguousContexts.Count > 0)
                {
                    extensions["referenceContextAlternatives"] = JsonSerializer.SerializeToElement(ambiguousContexts);
                }
            }
            extensions["sourceGap"] = JsonSerializer.SerializeToElement(IsPresent(identity) ? null : identity.Metadata.GetValueOrDefault("Decode Error") ?? "No native text exists in the selected base dataset.");
            records.Add(new()
            {
                Category = identity.Category,
                File = identity.File,
                Key = identity.Key,
                Source = IsPresent(identity) ? identity.Text : null,
                SourcePresent = IsPresent(identity),
                Target = "",
                References = references,
                ReferencePresence = presence,
                ReferenceKeys = referenceKeys,
                ExtensionData = extensions
            });
        }
        return new()
        {
            Manifest = new()
            {
                SchemaVersion = 2,
                SourceLanguage = baseLanguage,
                FieldScriptBaseLanguage = baseLanguage,
                TargetLanguage = request.TargetLanguage,
                SourceColumn = "Source [" + baseLanguage + "]",
                MappingVersion = FieldCorrespondenceCatalog.Version,
                ReferenceLanguages = referenceLanguages,
                CompatibleVariants = [],
                ExtensionData = new(StringComparer.Ordinal)
                {
                    ["packageName"] = JsonSerializer.SerializeToElement(request.PackageName),
                    ["unmappedReferenceFamilies"] = JsonSerializer.SerializeToElement(unmapped),
                    ["sourceGaps"] = JsonSerializer.SerializeToElement(catalogs.SelectMany(pair => pair.Value.Gaps.Select(gap => new { Language = pair.Key, gap.Category, gap.Resource, gap.Reason })).ToArray())
                }
            },
            Entries = records
        };
    }

    private static bool IsPresent(LocalizationSourceEntry entry) => entry.Metadata.GetValueOrDefault("Source Present") != "false";

    private static JsonElement MetadataValue(string key, string? value)
    {
        if (key is "Context Bindings" or "Call Contexts" or "Place IDs" or "Choice Line Map" && value is not null)
        {
            using JsonDocument document = JsonDocument.Parse(value);
            return document.RootElement.Clone();
        }
        return JsonSerializer.SerializeToElement(value);
    }
}
