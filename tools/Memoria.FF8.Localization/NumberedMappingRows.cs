using System.Globalization;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Localization;

internal static class NumberedMappingRows
{
    private static readonly string[] Languages = ["en", "fr", "de", "es", "it", "jp"];

    public static List<string[]> Normalize(List<string[]> records, IReadOnlyDictionary<string, FieldLocalizationNumberingEntry> migration)
    {
        string[] sourceHeader = records[0];
        foreach (string column in sourceHeader)
        {
            if (column is not ("Field" or "Ordinal" or "Field Name" or "Unused Across Languages") && !Languages.Contains(column, StringComparer.Ordinal) && !Languages.Any(language => column.StartsWith(language + " ", StringComparison.Ordinal)))
            {
                throw new InvalidDataException("Unsupported row-wide metadata column; use language-prefixed columns for key-specific metadata.");
            }
        }
        int unusedColumns = Languages.Count(language => sourceHeader.Contains(language + " Source Unused Across Languages", StringComparer.Ordinal));
        if (unusedColumns is not 0 and not 6)
        {
            throw new InvalidDataException("Source unused metadata must cover all six languages.");
        }
        string[] header = unusedColumns == 6 ? sourceHeader : [.. sourceHeader, .. Languages.Select(language => language + " Source Unused Across Languages")];
        Dictionary<string, int> columns = header.Select((name, index) => (name, index)).ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
        Dictionary<string, FieldLocalizationNumberingEntry> numbered = migration.Values.ToDictionary(entry => entry.NewKey, StringComparer.Ordinal);
        Dictionary<(string Field, int Base, int Slot), string[]> slots = [];
        Dictionary<(string Field, int Base, int Slot), List<string>> unused = [];
        Dictionary<string, string> names = new(StringComparer.Ordinal);
        Dictionary<(string Field, string Language), string> missing = [];
        List<string[]> placeholders = [];
        int fieldColumn = columns["Field"];
        bool hasOrdinal = columns.TryGetValue("Ordinal", out int ordinalColumn);
        bool hasName = columns.TryGetValue("Field Name", out int nameColumn);
        bool hasUnused = columns.TryGetValue("Unused Across Languages", out int unusedColumn);
        foreach (string[] source in records.Skip(1))
        {
            string field = source[fieldColumn];
            if (hasName)
            {
                if (names.TryGetValue(field, out string? name) && name != source[nameColumn])
                {
                    throw new InvalidDataException("Field Name metadata must agree across a field.");
                }
                names[field] = source[nameColumn];
            }
            bool anyKey = false;
            for (int languageIndex = 0; languageIndex < Languages.Length; languageIndex++)
            {
                string language = Languages[languageIndex];
                string key = source[languageIndex];
                if (key.Length == 0)
                {
                    if (columns.TryGetValue(language + " Status", out int statusColumn) && source[statusColumn] is "UnknownBank" or "EmptyBank" or "AbsentLocalizedSlot")
                    {
                        missing[(field, language)] = source[statusColumn];
                    }
                    continue;
                }
                anyKey = true;
                FieldLocalizationNumberingEntry entry = numbered[key];
                var identity = (field, entry.AnchorOrdinal, entry.Insertion);
                if (!slots.TryGetValue(identity, out string[]? row))
                {
                    row = Enumerable.Repeat(string.Empty, header.Length).ToArray();
                    row[fieldColumn] = field;
                    if (hasOrdinal)
                    {
                        row[ordinalColumn] = entry.AnchorOrdinal.ToString(CultureInfo.InvariantCulture);
                    }
                    if (hasName)
                    {
                        row[nameColumn] = source[nameColumn];
                    }
                    slots.Add(identity, row);
                    unused.Add(identity, []);
                }
                if (row[languageIndex].Length != 0)
                {
                    throw new InvalidDataException("A common numbering slot cannot contain two keys from one language.");
                }
                row[languageIndex] = key;
                for (int column = Languages.Length; column < sourceHeader.Length; column++)
                {
                    if (sourceHeader[column].StartsWith(language + " ", StringComparison.Ordinal))
                    {
                        row[column] = source[column];
                    }
                }
                int sourceUnusedColumn = columns[language + " Source Unused Across Languages"];
                string originalUnused = unusedColumns == 6 ? source[sourceUnusedColumn] : hasUnused ? source[unusedColumn] : string.Empty;
                row[sourceUnusedColumn] = originalUnused;
                unused[identity].Add(originalUnused);
            }
            if (!anyKey && (!hasOrdinal || source[ordinalColumn].Length == 0))
            {
                placeholders.Add([.. source, .. Enumerable.Repeat(string.Empty, header.Length - source.Length)]);
            }
        }
        HashSet<(string Field, string Language)> available = slots.Values.SelectMany(row => Languages.Where((language, index) => row[index].Length != 0).Select(language => (row[fieldColumn], language))).ToHashSet();
        foreach ((var identity, string[] row) in slots)
        {
            if (hasUnused)
            {
                string[] values = unused[identity].Distinct(StringComparer.Ordinal).ToArray();
                row[unusedColumn] = values.Length == 1 ? values[0] : "Mixed";
            }
            foreach (string language in Languages.Skip(1))
            {
                if (!available.Contains((identity.Field, language)) && missing.TryGetValue((identity.Field, language), out string? status) && columns.TryGetValue(language + " Status", out int statusColumn))
                {
                    row[statusColumn] = status;
                }
            }
        }
        List<string[]> result = [header];
        foreach (string field in slots.Keys.Select(identity => identity.Field).Concat(placeholders.Select(row => row[fieldColumn])).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            result.AddRange(slots.Where(pair => pair.Key.Field == field).OrderBy(pair => pair.Key.Base).ThenBy(pair => pair.Key.Slot).Select(pair => pair.Value));
            result.AddRange(placeholders.Where(row => row[fieldColumn] == field));
        }
        return result;
    }
}
