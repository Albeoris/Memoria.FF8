using System.Text.Json;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public sealed record LocalizationProjectReconciliation(LocalizationProject Project, IReadOnlyList<string> NewKeys, IReadOnlyList<string> ChangedSourceKeys, IReadOnlyList<string> ObsoleteKeys);

public static partial class LocalizationProjectExport
{
    /// <summary>Creates an independent, initially empty contribution with the base's exact source identities.</summary>
    public static LocalizationProject CreatePatch(LocalizationProject source, string packageName)
    {
        LocalizationProjectIO.Validate(source);
        if (string.IsNullOrWhiteSpace(packageName) || packageName.Any(char.IsControl))
        {
            throw new ArgumentException("A patch requires a package name without control characters.", nameof(packageName));
        }
        Dictionary<string, JsonElement> metadata = source.Manifest.ExtensionData.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
        metadata["packageName"] = JsonSerializer.SerializeToElement(packageName);
        LocalizationProject patch = new()
        {
            Manifest = source.Manifest with { ProjectId = Guid.NewGuid().ToString("D"), BaseProjectId = source.Manifest.ProjectId, Payloads = [], ExtensionsPath = null, ExtensionData = metadata },
            Entries = source.Entries.Select(row => Snapshot(row, source.Manifest.ReferenceLanguages.Keys) with { Target = "" }).ToArray(),
            Artwork = source.Artwork.Select(file => CopyArtwork(file, emptyTarget: true)).ToArray()
        };
        LocalizationProjectIO.Validate(patch);
        return patch;
    }

    /// <summary>Captures selected contributions in priority order, lowest first. Installed fallback is never translated Target data.</summary>
    public static LocalizationProject Resolve(IReadOnlyList<LocalizationProject> projects, string targetLanguage, long committedGeneration = 0, bool replacementOfSameTargetProfile = false)
    {
        if (projects.Count == 0)
        {
            throw new ArgumentException("Select at least one localization contribution.", nameof(projects));
        }
        foreach (LocalizationProject project in projects)
        {
            LocalizationProjectIO.Validate(project);
        }
        if (projects.Select(project => project.Manifest.ProjectId).Distinct(StringComparer.Ordinal).Count() != projects.Count)
        {
            throw new InvalidDataException("A localization contribution was selected more than once.");
        }
        if (!replacementOfSameTargetProfile && projects.Any(project => string.Equals(project.Manifest.TargetLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("An independent flattened translation requires a distinct Target identifier.");
        }
        if (replacementOfSameTargetProfile && projects.Any(project => !string.Equals(project.Manifest.TargetLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("A mixed-target stack requires an explicit distinct composite Target identifier.");
        }
        LocalizationManifest first = projects[0].Manifest;
        if (projects.Any(project => project.Manifest.MappingVersion != first.MappingVersion || project.Manifest.FieldScriptBaseLanguage != first.FieldScriptBaseLanguage))
        {
            throw new InvalidDataException("Flattened projects must use the same reviewed mapping version and script-base contract.");
        }
        if (projects.Any(project => project.Manifest.SourceLanguage != first.SourceLanguage || project.Manifest.ReferenceLanguages.Count != first.ReferenceLanguages.Count || project.Manifest.ReferenceLanguages.Any(pair => first.ReferenceLanguages.GetValueOrDefault(pair.Key) != pair.Value)))
        {
            throw new InvalidDataException("Flattened contributions require consistent authoring source and reference identities; normalize these explicitly before merging.");
        }
        Dictionary<string, LocalizationRecord> rows = new(StringComparer.Ordinal);
        foreach (LocalizationProject project in projects)
        {
            foreach (LocalizationRecord row in project.Entries)
            {
                string identity = LocalizationProjectIO.Identity(row);
                if (!rows.TryGetValue(identity, out LocalizationRecord? existing))
                {
                    rows.Add(identity, Snapshot(row, first.ReferenceLanguages.Keys));
                }
                else if (!string.IsNullOrEmpty(row.Target))
                {
                    Dictionary<string, JsonElement> metadata = existing.ExtensionData.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
                    metadata["translationContributor"] = JsonSerializer.SerializeToElement(project.Manifest.ProjectId);
                    rows[identity] = existing with { Target = row.Target, ExtensionData = metadata };
                }
            }
        }
        NormalizeSystemAliasTargets(projects, rows);
        Dictionary<string, JsonElement> provenance = first.ExtensionData.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
        provenance["contributors"] = JsonSerializer.SerializeToElement(projects.Select(project => new { projectId = project.Manifest.ProjectId, targetLanguage = project.Manifest.TargetLanguage }).ToArray());
        provenance["sourceGeneration"] = JsonSerializer.SerializeToElement(committedGeneration);
        IReadOnlyList<LocalizationArtworkFile> artwork = ResolveArtwork(projects);
        LocalizationProject result = new()
        {
            Manifest = first with { ProjectId = Guid.NewGuid().ToString("D"), BaseProjectId = null, TargetLanguage = targetLanguage, TargetColumn = "Target [" + targetLanguage + "]", Payloads = [], ExtensionsPath = null, ExtensionData = provenance, Artwork = artwork.Select(file => file.Declaration).ToArray() },
            Entries = rows.Values.OrderBy(row => row.Category, StringComparer.Ordinal).ThenBy(row => row.File, StringComparer.Ordinal).ThenBy(row => row.Key, StringComparer.Ordinal).ToArray(),
            Artwork = artwork
        };
        LocalizationProjectIO.Validate(result);
        return result;
    }

    private static void NormalizeSystemAliasTargets(IReadOnlyList<LocalizationProject> projects, Dictionary<string, LocalizationRecord> rows)
    {
        var layers = projects.Select(project => (project.Manifest.ProjectId, Rows: project.Entries.Where(row => row.Category == "System" && row.File == SystemTextCatalog.File && !string.IsNullOrEmpty(row.Target)).ToDictionary(row => row.Key, StringComparer.Ordinal))).ToArray();
        foreach ((string identity, LocalizationRecord row) in rows.ToArray())
        {
            if (row.Category != "System" || row.File != SystemTextCatalog.File || SystemTextCatalog.KeysForCatalogKey(row.Key) is not { } aliases)
            {
                continue;
            }
            foreach (var layer in layers.Reverse())
            {
                LocalizationRecord? winner = aliases.Select(key => layer.Rows.GetValueOrDefault(key)).FirstOrDefault(candidate => candidate is not null);
                if (winner is null)
                {
                    continue;
                }
                Dictionary<string, JsonElement> metadata = row.ExtensionData.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
                metadata["translationContributor"] = JsonSerializer.SerializeToElement(layer.ProjectId);
                metadata["resolvedSystemAlias"] = JsonSerializer.SerializeToElement(winner.Key);
                rows[identity] = row with { Target = winner.Target, ExtensionData = metadata };
                break;
            }
        }
    }

    /// <summary>Refreshes authoring sources while retaining all translator work, including obsolete identities.</summary>
    public static LocalizationProjectReconciliation Reconcile(LocalizationProject existing, LocalizationProject refreshedSource)
    {
        LocalizationProjectIO.Validate(existing);
        LocalizationProjectIO.Validate(refreshedSource);
        if (existing.Manifest.SourceLanguage != refreshedSource.Manifest.SourceLanguage || existing.Manifest.FieldScriptBaseLanguage != refreshedSource.Manifest.FieldScriptBaseLanguage)
        {
            throw new InvalidDataException("Resource updates must retain the source and script-base identities.");
        }
        Dictionary<string, LocalizationRecord> old = existing.Entries.ToDictionary(LocalizationProjectIO.Identity, StringComparer.Ordinal);
        Dictionary<string, string> referenceLanguages = existing.Manifest.ReferenceLanguages.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach ((string label, string dataset) in refreshedSource.Manifest.ReferenceLanguages)
        {
            if (referenceLanguages.TryGetValue(label, out string? previousDataset) && previousDataset != dataset)
            {
                throw new InvalidDataException($"Reference column {label} changed dataset identity during resource update.");
            }
            referenceLanguages[label] = dataset;
        }
        List<string> added = [];
        List<string> changed = [];
        List<LocalizationRecord> rows = [];
        foreach (LocalizationRecord fresh in refreshedSource.Entries)
        {
            string identity = LocalizationProjectIO.Identity(fresh);
            if (old.Remove(identity, out LocalizationRecord? previous))
            {
                if (previous.Source != fresh.Source || previous.SourcePresent != fresh.SourcePresent)
                {
                    changed.Add(fresh.Key);
                }
                Dictionary<string, JsonElement> metadata = previous.ExtensionData.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
                foreach ((string key, JsonElement value) in fresh.ExtensionData)
                {
                    if (!metadata.ContainsKey(key) || key is not ("Comment" or "Status" or "Translator"))
                    {
                        metadata[key] = value.Clone();
                    }
                }
                Dictionary<string, string?> references = previous.References.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                Dictionary<string, bool> presence = previous.ReferencePresence.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                foreach (string label in refreshedSource.Manifest.ReferenceLanguages.Keys)
                {
                    references[label] = fresh.References.GetValueOrDefault(label);
                    presence[label] = fresh.ReferencePresence.TryGetValue(label, out bool present) ? present : fresh.References.ContainsKey(label);
                }
                rows.Add(Snapshot(fresh, referenceLanguages.Keys) with { Target = previous.Target, ExtensionData = metadata, References = references, ReferencePresence = presence });
            }
            else
            {
                added.Add(fresh.Key);
                rows.Add(Snapshot(fresh, referenceLanguages.Keys) with { Target = null });
            }
        }
        rows.AddRange(old.Values.Select(row => Snapshot(row, referenceLanguages.Keys)));
        Dictionary<string, JsonElement> manifestMetadata = existing.Manifest.ExtensionData.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
        foreach ((string key, JsonElement value) in refreshedSource.Manifest.ExtensionData)
        {
            if (key != "packageName" || !manifestMetadata.ContainsKey(key))
            {
                manifestMetadata[key] = value.Clone();
            }
        }
        LocalizationProject result = new()
        {
            Manifest = refreshedSource.Manifest with { ProjectId = existing.Manifest.ProjectId, BaseProjectId = existing.Manifest.BaseProjectId, TargetLanguage = existing.Manifest.TargetLanguage, TargetColumn = existing.Manifest.TargetColumn, ReferenceLanguages = referenceLanguages, Payloads = [], ExtensionsPath = null, ExtensionData = manifestMetadata },
            Entries = rows,
            Artwork = ReconcileArtwork(existing, refreshedSource)
        };
        result = result with { Manifest = result.Manifest with { Artwork = result.Artwork.Select(file => file.Declaration).ToArray() } };
        LocalizationProjectIO.Validate(result);
        return new(result, added, changed, old.Values.Select(row => row.Key).ToArray());
    }

    private static LocalizationRecord Snapshot(LocalizationRecord row, IEnumerable<string> referenceLabels)
    {
        string[] labels = referenceLabels.ToArray();
        return row with
        {
            References = row.References.Where(pair => labels.Contains(pair.Key, StringComparer.Ordinal)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            ReferencePresence = row.ReferencePresence.Where(pair => labels.Contains(pair.Key, StringComparer.Ordinal)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            ExtensionData = row.ExtensionData.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal)
        };
    }
}
