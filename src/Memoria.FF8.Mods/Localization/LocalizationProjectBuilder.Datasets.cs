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

    private static IReadOnlyList<string> DatasetEquivalentKeys(LocalizationSourceEntry identity, string dataset, IReadOnlyDictionary<(string Category, string File, string Key), LocalizationSourceEntry> index)
    {
        if (identity.Category != "Fields")
        {
            return [];
        }
        if (BuiltInLanguages.Contains(dataset, StringComparer.OrdinalIgnoreCase))
        {
            return FieldCorrespondenceCatalog.GetEquivalents(identity.Key, dataset);
        }
        return BuiltInLanguages.SelectMany(language => FieldCorrespondenceCatalog.GetEquivalents(identity.Key, language)).Distinct(StringComparer.Ordinal).Where(key => index.ContainsKey((identity.Category, identity.File, key))).ToArray();
    }
}
