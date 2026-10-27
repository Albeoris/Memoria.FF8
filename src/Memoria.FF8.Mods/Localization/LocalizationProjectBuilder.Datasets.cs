using System.Text.Json;
using System.Text.RegularExpressions;
using Albeoris.Games.FF8.TextEncoding.Tags;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public static partial class LocalizationProjectBuilder
{
    public const string EffectiveProfileDatasetId = "profile:active";
    private static readonly string[] BuiltInLanguages = ["en", "fr", "de", "es", "it", "jp", "ja"];

    public static string ProjectDatasetId(LocalizationProject project) => "project:" + project.Manifest.ProjectId;

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
        public Dictionary<string, LocalizationSourceEntry> Exact { get; }
        public Dictionary<(string File, string Key), LocalizationSourceEntry[]> Fields { get; }
        public Dictionary<string, LocalizationSourceEntry[]> CommonKeys { get; }
        private readonly Dictionary<string, string> displayTexts = new(StringComparer.Ordinal);

        public ReferenceDatasetIndex(LocalizationSourceCatalog catalog)
        {
            Exact = catalog.Entries.ToDictionary(row => row.Key, StringComparer.Ordinal);
            Fields = catalog.Entries.Where(row => row.Category == "Fields").GroupBy(row => (row.File, row.Metadata.GetValueOrDefault("Base Key") ?? row.Key)).ToDictionary(group => group.Key, group => group.ToArray());
            CommonKeys = catalog.Entries.GroupBy(row => WithoutLanguagePrefix(row.Key), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        }

        public string DisplayText(LocalizationSourceEntry entry) => DisplayText(entry, new HashSet<string>(StringComparer.Ordinal));

        private string DisplayText(LocalizationSourceEntry entry, HashSet<string> visiting)
        {
            if (displayTexts.TryGetValue(entry.Key, out string? cached))
            {
                return cached;
            }
            if (!visiting.Add(entry.Key))
            {
                throw new InvalidDataException(entry.Key + ": cyclic native term dictionary reference.");
            }
            string text = Regex.Replace(entry.Text, @"\{Term ([^{}]+)\}", match =>
            {
                if (!Enum.TryParse(match.Groups[1].Value, out TextTagTerm term))
                {
                    throw new InvalidDataException(entry.Key + ": invalid native term reference " + match.Value + ".");
                }
                int ordinal = unchecked((byte)((byte)term - 32));
                string commonKey = "namedic_menu_" + ordinal.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
                if (!CommonKeys.TryGetValue(commonKey, out LocalizationSourceEntry[]? terms) || terms.Length != 1 || !IsPresent(terms[0]))
                {
                    throw new InvalidDataException(entry.Key + ": missing native dictionary entry " + commonKey + ".");
                }
                return DisplayText(terms[0], visiting);
            }, RegexOptions.CultureInvariant);
            visiting.Remove(entry.Key);
            displayTexts.Add(entry.Key, text);
            return text;
        }
    }

    private static IReadOnlyList<string> DatasetEquivalentKeys(LocalizationSourceEntry identity, string dataset, ReferenceDatasetIndex index)
    {
        if (index.Exact.ContainsKey(identity.Key))
        {
            return [identity.Key];
        }
        string baseKey = identity.Metadata.GetValueOrDefault("Base Key") ?? identity.Key;
        if (!FieldCorrespondenceCatalog.Contains(baseKey))
        {
            return index.CommonKeys.TryGetValue(WithoutLanguagePrefix(identity.Key), out LocalizationSourceEntry[]? matches) ? matches.Select(row => row.Key).ToArray() : [];
        }
        IEnumerable<string> equivalentBases = BuiltInLanguages.Contains(dataset, StringComparer.OrdinalIgnoreCase) ? FieldCorrespondenceCatalog.GetEquivalents(baseKey, dataset) : BuiltInLanguages.SelectMany(language => FieldCorrespondenceCatalog.GetEquivalents(baseKey, language)).Distinct(StringComparer.Ordinal);
        List<string> keys = [];
        string? prefix = ContextPrefix(identity);
        bool variant = identity.Metadata.ContainsKey("Base Key");
        foreach (string equivalent in equivalentBases)
        {
            if (!variant)
            {
                if (index.Exact.ContainsKey(equivalent))
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
            else if (index.Exact.ContainsKey(equivalent))
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

    private static string WithoutLanguagePrefix(string key)
    {
        int separator = key.IndexOf('_');
        return key.StartsWith('$') && separator > 1 ? key[(separator + 1)..] : key;
    }
}
