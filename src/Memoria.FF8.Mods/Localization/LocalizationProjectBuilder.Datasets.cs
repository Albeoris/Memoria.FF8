using System.Text.Json;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public static partial class LocalizationProjectBuilder
{
    public const string EffectiveProfileDatasetId = "profile:active";
    private static readonly string[] BuiltInLanguages = ["en", "fr", "de", "es", "it", "jp", "ja"];

    public static string ProjectDatasetId(LocalizationProject project) => "project:" + project.Manifest.ProjectId;

    private static LocalizationSourceCatalog ReadDataset(string gameDirectory, string dataset, IReadOnlyList<LocalizationLayer> loaded, IReadOnlyList<string>? profileOrder, CancellationToken cancellation)
    {
        if (dataset == EffectiveProfileDatasetId)
        {
            LocalizationLayer[] layers = profileOrder is null ? loaded.ToArray() : profileOrder.Select(id => loaded.Single(layer => string.Equals(layer.PackageId, id, StringComparison.OrdinalIgnoreCase))).ToArray();
            LocalizationProject profile = LocalizationProjectExport.Resolve(layers.Select(layer => layer.Project).ToArray(), "Effective active profile (source snapshot)");
            return ProjectDataset(profile, dataset, "effective-profile", cancellation);
        }
        if (dataset.StartsWith("project:", StringComparison.Ordinal))
        {
            LocalizationLayer layer = loaded.SingleOrDefault(layer => ProjectDatasetId(layer.Project) == dataset) ?? throw new InvalidDataException("The selected localization source project is no longer loaded: " + dataset);
            return ProjectDataset(layer.Project, dataset, "own-contribution", cancellation);
        }
        return LocalizationSourceExporter.Read(gameDirectory, dataset, cancellation);
    }

    public static LocalizationSourceCatalog ProjectDataset(LocalizationProject project, string dataset, string role = "own-contribution", CancellationToken cancellation = default)
    {
        LocalizationProjectIO.Validate(project);
        if (project.Manifest.MappingVersion != FieldCorrespondenceCatalog.Version)
        {
            throw new InvalidDataException("The selected project uses an unsupported correspondence mapping.");
        }
        List<LocalizationSourceEntry> entries = [];
        List<LocalizationSourceGap> gaps = [];
        foreach (LocalizationRecord row in project.Entries)
        {
            cancellation.ThrowIfCancellationRequested();
            bool present = !string.IsNullOrEmpty(row.Target);
            Dictionary<string, string?> metadata = row.ExtensionData.ToDictionary(pair => pair.Key, pair => pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() : pair.Value.GetRawText(), StringComparer.Ordinal);
            metadata["Source Present"] = present ? "true" : "false";
            metadata["Dataset Project Id"] = project.Manifest.ProjectId;
            metadata["Dataset Target Id"] = project.Manifest.TargetLanguage;
            metadata["Dataset Role"] = role;
            if (!present)
            {
                metadata["Decode Error"] = "This contribution has no translated Target for the source identity.";
                gaps.Add(new(row.Category, row.Key, "No translated Target in the selected contribution."));
            }
            entries.Add(new(row.Category, row.File, row.Key, present ? row.Target! : "", dataset, metadata));
        }
        return new(entries, gaps, [new(dataset, true, null)]);
    }

    private sealed class ReferenceDatasetIndex
    {
        public Dictionary<(string Category, string File, string Key), LocalizationSourceEntry> Exact { get; }
        public Dictionary<(string File, string Key), LocalizationSourceEntry[]> Fields { get; }
        public Dictionary<(string Category, string File, string Identity), LocalizationSourceEntry[]> Semantic { get; }

        public ReferenceDatasetIndex(LocalizationSourceCatalog catalog)
        {
            Exact = catalog.Entries.ToDictionary(row => (row.Category, row.File, row.Key));
            Fields = catalog.Entries.Where(row => row.Category == "Fields").GroupBy(row => (row.File, row.Metadata.GetValueOrDefault("Base Key") ?? row.Key)).ToDictionary(group => group.Key, group => group.ToArray());
            Semantic = catalog.Entries.Select(row => (Row: row, Identity: SemanticIdentity(row))).Where(pair => pair.Identity is not null).GroupBy(pair => (pair.Row.Category, pair.Row.File, pair.Identity!)).ToDictionary(group => group.Key, group => group.Select(pair => pair.Row).ToArray());
        }
    }

    private static IReadOnlyList<string> DatasetEquivalentKeys(LocalizationSourceEntry identity, string dataset, ReferenceDatasetIndex index)
    {
        if (index.Exact.ContainsKey((identity.Category, identity.File, identity.Key)))
        {
            return [identity.Key];
        }
        if (identity.Category != "Fields")
        {
            string? semantic = SemanticIdentity(identity);
            return semantic is not null && index.Semantic.TryGetValue((identity.Category, identity.File, semantic), out LocalizationSourceEntry[]? matches) && matches.Length == 1 ? [matches[0].Key] : [];
        }
        string baseKey = identity.Metadata.GetValueOrDefault("Base Key") ?? identity.Key;
        IEnumerable<string> equivalentBases = BuiltInLanguages.Contains(dataset, StringComparer.OrdinalIgnoreCase) ? FieldCorrespondenceCatalog.GetEquivalents(baseKey, dataset) : BuiltInLanguages.SelectMany(language => FieldCorrespondenceCatalog.GetEquivalents(baseKey, language)).Distinct(StringComparer.Ordinal);
        List<string> keys = [];
        string? prefix = ContextPrefix(identity);
        bool variant = identity.Metadata.ContainsKey("Base Key");
        foreach (string equivalent in equivalentBases)
        {
            if (!variant)
            {
                if (index.Exact.ContainsKey((identity.Category, identity.File, equivalent)))
                {
                    keys.Add(equivalent);
                }
                continue;
            }
            LocalizationSourceEntry[] compatible = prefix is not null && index.Fields.TryGetValue((identity.File, equivalent), out LocalizationSourceEntry[]? candidates) ? candidates.Where(candidate => ContextPrefix(candidate) == prefix).ToArray() : [];
            if (compatible.Length == 1)
            {
                keys.Add(compatible[0].Key);
            }
            else if (index.Exact.ContainsKey((identity.Category, identity.File, equivalent)))
            {
                keys.Add(equivalent);
            }
        }
        return keys.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string? ContextPrefix(LocalizationSourceEntry entry)
    {
        if (entry.Metadata.GetValueOrDefault("Context Bindings") is not string json)
        {
            return null;
        }
        LocalizationSourceContextBinding[] bindings = JsonSerializer.Deserialize<LocalizationSourceContextBinding[]>(json) ?? [];
        bool variant = entry.Metadata.ContainsKey("Base Key");
        LocalizationSourceContextBinding[] applicable = bindings.Where(binding => variant ? binding.VariantKey == entry.Key : binding.VariantKey is null).ToArray();
        return applicable.Length == 1 && !applicable[0].Dynamic ? applicable[0].Prefix : null;
    }

    private static string? SemanticIdentity(LocalizationSourceEntry row)
    {
        if (row.Category == "System")
        {
            return row.Metadata.GetValueOrDefault("Authored ID");
        }
        if (!int.TryParse(row.Metadata.GetValueOrDefault("Source Ordinal"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int ordinal) || ordinal < 0)
        {
            return null;
        }
        if (row.Metadata.GetValueOrDefault("Kernel Collection") is string collection && row.Metadata.GetValueOrDefault("Role") is string role && row.File == LocalizationSourceExporter.KernelTable(collection, role))
        {
            return collection + ":" + role + ":" + ordinal;
        }
        // These tables are indexed by fixed native entity IDs, independent of text layout.
        bool entity = row.File switch
        {
            "names_angelo_abilities" or "descriptions_angelo_abilities" => ordinal < 8,
            "defaults_guardians" => ordinal < 16,
            "names_cards" or "namesalternate_cards" or "named_texts_cards" => ordinal < 110,
            "scan_monsters" => ordinal < 160,
            "mngrp_slot192_records" => row.Category == "Cards" && row.Metadata.GetValueOrDefault("Menu Section") == "192" && ordinal < 110,
            "names_locations" => true,
            _ => row.Category == "Monsters" && row.File.StartsWith("c0m", StringComparison.Ordinal) && row.File.EndsWith("_names", StringComparison.Ordinal) && ordinal == 0
        };
        // World messages, namedic and menu section positions have no proved shared identity.
        return entity ? ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
    }
}
