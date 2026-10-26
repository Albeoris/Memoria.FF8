using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Localization;

internal static class ReviewedCorrespondence
{
    private static readonly string[] Languages = ["en", "fr", "de", "es", "it", "jp"];

    public static List<string[]> Create(IReadOnlyList<string[]> records, IReadOnlyList<FieldLocalizationNumberingEntry> entries)
    {
        Dictionary<string, int> columns = records[0].Select((name, index) => (name, index)).ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
        Dictionary<string, FieldLocalizationNumberingEntry> identities = entries.ToDictionary(entry => entry.NewKey, StringComparer.Ordinal);
        Dictionary<string, string> parents = identities.Keys.ToDictionary(key => key, key => key, StringComparer.Ordinal);
        Dictionary<(string Field, string Group), string> groups = [];
        string Find(string key)
        {
            string root = key;
            while (parents[root] != root)
            {
                root = parents[root];
            }
            while (parents[key] != key)
            {
                string next = parents[key];
                parents[key] = root;
                key = next;
            }
            return root;
        }
        void Join(string key, string reference)
        {
            if (!identities.TryGetValue(reference, out FieldLocalizationNumberingEntry? other) || other.Field != identities[key].Field)
            {
                throw new InvalidDataException("Correspondence references must identify existing keys in the same field.");
            }
            parents[Find(key)] = Find(reference);
        }
        foreach (string[] row in records.Skip(1))
        {
            foreach (string language in Languages)
            {
                string key = row[columns[language]];
                if (key.Length == 0)
                {
                    continue;
                }
                if (columns.TryGetValue(language + " English Keys", out int referencesColumn) && row[referencesColumn].Length != 0)
                {
                    foreach (string reference in row[referencesColumn].Split(';'))
                    {
                        Join(key, reference);
                    }
                }
                if (columns.TryGetValue(language + " Correspondence Group", out int groupColumn) && row[groupColumn].Length != 0)
                {
                    var group = (identities[key].Field, row[groupColumn]);
                    if (groups.TryGetValue(group, out string? previous))
                    {
                        Join(key, previous);
                    }
                    else
                    {
                        groups.Add(group, key);
                    }
                }
            }
        }
        var components = identities.Values.GroupBy(entry => Find(entry.NewKey)).Select(group => group.ToArray()).OrderBy(group => group[0].Field, StringComparer.Ordinal).ThenBy(group => group.Where(entry => entry.Language == "en").Select(entry => entry.SourceOrdinal).DefaultIfEmpty(group.Min(entry => entry.SourceOrdinal)).Min()).ThenBy(group => group.Any(entry => entry.Language == "en") ? 0 : 1).ThenBy(group => group.Min(entry => entry.NewKey), StringComparer.Ordinal);
        List<string[]> result = [[.. Languages, "Field", "Kind"]];
        foreach (FieldLocalizationNumberingEntry[] component in components)
        {
            result.Add([.. Languages.Select(language => string.Join(';', component.Where(entry => entry.Language == language).OrderBy(entry => entry.SourceOrdinal).Select(entry => entry.NewKey))), component[0].Field, component.Any(entry => entry.Language == "en") ? "EnglishReference" : "LocalizedOnly"]);
        }
        return result;
    }
}
