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
            MainSource = dataset == request.SourceLanguage,
            FieldScriptBase = dataset == request.FieldScriptBaseLanguage,
            Reference = request.ReferenceLanguages.Contains(dataset, StringComparer.OrdinalIgnoreCase),
            Contributors = (loadedProjects ?? []).Where(layer => dataset == ProjectDatasetId(layer.Project) || dataset == EffectiveProfileDatasetId && (profileOrder is null || profileOrder.Contains(layer.PackageId, StringComparer.OrdinalIgnoreCase))).Select(layer => new { layer.Project.Manifest.ProjectId, layer.Project.Manifest.TargetLanguage }).ToArray()
        }).ToArray());
        string scriptLanguage = request.FieldScriptBaseLanguage.Equals("ja", StringComparison.OrdinalIgnoreCase) ? "jp" : request.FieldScriptBaseLanguage.ToLowerInvariant();
        return project with { Manifest = project.Manifest with { CompatibleVariants = [edition + ":" + scriptLanguage], ExtensionData = manifestExtensions } };
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
        Dictionary<string, Dictionary<(string Category, string File, string Key), LocalizationSourceEntry>> indexes = catalogs.ToDictionary(pair => pair.Key, pair => pair.Value.Entries.ToDictionary(row => (row.Category, row.File, row.Key)), StringComparer.OrdinalIgnoreCase);
        List<LocalizationRecord> records = [];
        IEnumerable<LocalizationSourceEntry> union = catalogs.Values.SelectMany(catalog => catalog.Entries).Concat(includeSystem ? SystemTextCatalog.SourceEntries() : []).DistinctBy(row => (row.Category, row.File, row.Key)).OrderBy(row => row.Category, StringComparer.Ordinal).ThenBy(row => row.File, StringComparer.Ordinal).ThenBy(row => row.Key, StringComparer.Ordinal);
        foreach (LocalizationSourceEntry identity in union)
        {
            cancellation.ThrowIfCancellationRequested();
            LocalizationSourceEntry? Lookup(string language)
            {
                Dictionary<(string Category, string File, string Key), LocalizationSourceEntry> index = indexes[language];
                if (index.TryGetValue((identity.Category, identity.File, identity.Key), out LocalizationSourceEntry? exact))
                {
                    return exact;
                }
                IReadOnlyList<string> equivalents = DatasetEquivalentKeys(identity, language, index);
                if (equivalents.Count == 1)
                {
                    return index.GetValueOrDefault((identity.Category, identity.File, equivalents[0]));
                }
                return null;
            }
            LocalizationSourceEntry? source = Lookup(request.SourceLanguage);
            bool sourcePresent = source is not null && source.Metadata.GetValueOrDefault("Source Present") != "false";
            Dictionary<string, string[]> aliases = new(StringComparer.OrdinalIgnoreCase);
            if (identity.Category == "Fields")
            {
                foreach (string language in catalogs.Keys)
                {
                    aliases[language] = DatasetEquivalentKeys(identity, language, indexes[language]).ToArray();
                }
            }
            Dictionary<string, string?> references = new(StringComparer.Ordinal);
            Dictionary<string, bool> presence = new(StringComparer.Ordinal);
            foreach (string language in request.ReferenceLanguages.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                LocalizationSourceEntry? reference = Lookup(language);
                bool referencePresent = reference is not null && reference.Metadata.GetValueOrDefault("Source Present") != "false";
                references.Add("Reference [" + language + "]", referencePresent ? reference!.Text : null);
                presence.Add("Reference [" + language + "]", referencePresent);
            }
            Dictionary<string, JsonElement> extensions = identity.Metadata.ToDictionary(pair => pair.Key, pair => MetadataValue(pair.Key, pair.Value), StringComparer.Ordinal);
            extensions["sourceContext"] = JsonSerializer.SerializeToElement(identity.Metadata);
            extensions["equivalentKeys"] = JsonSerializer.SerializeToElement(aliases);
            extensions["equivalentContexts"] = JsonSerializer.SerializeToElement(aliases.Where(pair => pair.Value.Length > 1).ToDictionary(pair => pair.Key, pair => pair.Value.Select(key => indexes[pair.Key].GetValueOrDefault((identity.Category, identity.File, key))).Where(entry => entry is not null).Select(entry => new { entry!.Key, entry.Text, entry.Metadata }).ToArray()));
            extensions["sourceGap"] = JsonSerializer.SerializeToElement(!sourcePresent ? source?.Metadata.GetValueOrDefault("Decode Error") ?? (aliases.GetValueOrDefault(request.SourceLanguage)?.Length > 1 ? "Several source counterparts exist; review each preserved source key." : "No source counterpart is available.") : null);
            records.Add(new()
            {
                Category = identity.Category,
                File = identity.File,
                Key = identity.Key,
                Source = sourcePresent ? source!.Text : null,
                SourcePresent = sourcePresent,
                Target = "",
                References = references,
                ReferencePresence = presence,
                ExtensionData = extensions
            });
        }
        return new()
        {
            Manifest = new()
            {
                SourceLanguage = request.SourceLanguage,
                FieldScriptBaseLanguage = request.FieldScriptBaseLanguage,
                TargetLanguage = request.TargetLanguage,
                SourceColumn = "Source [" + request.SourceLanguage + "]",
                MappingVersion = FieldCorrespondenceCatalog.Version,
                ReferenceLanguages = request.ReferenceLanguages.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(language => "Reference [" + language + "]", language => language),
                CompatibleVariants = [],
                ExtensionData = new(StringComparer.Ordinal)
                {
                    ["packageName"] = JsonSerializer.SerializeToElement(request.PackageName),
                    ["sourceGaps"] = JsonSerializer.SerializeToElement(catalogs.SelectMany(pair => pair.Value.Gaps.Select(gap => new { Language = pair.Key, gap.Category, gap.Resource, gap.Reason })).ToArray())
                }
            },
            Entries = records
        };
    }

    private static JsonElement MetadataValue(string key, string? value)
    {
        if (key is "Context Bindings" or "Call Contexts" or "Place IDs" && value is not null)
        {
            using JsonDocument document = JsonDocument.Parse(value);
            return document.RootElement.Clone();
        }
        return JsonSerializer.SerializeToElement(value);
    }
}
