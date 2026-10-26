using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Localization;

internal static class RenumberFieldMapping
{
    private static readonly string[] Languages = ["en", "fr", "de", "es", "it", "jp"];
    private const string Rule = "source-ordinal-v1: $<language>_<field>_<source ordinal:D4>";

    public static string Run(string inputFile, string outputDirectory, CancellationToken cancellation)
    {
        string input = Path.GetFullPath(inputFile);
        string output = Path.GetFullPath(outputDirectory);
        RemasteredFieldAudit.ValidateOutput(output, Path.GetDirectoryName(input)!);
        if (File.Exists(output) || Directory.Exists(output))
        {
            throw new IOException("The report destination must be new; existing reports are never replaced.");
        }
        if (new FileInfo(input).Length > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("The mapping exceeds the 64 MiB input limit.");
        }
        byte[] inputBytes = File.ReadAllBytes(input);
        string text = new UTF8Encoding(false, true).GetString(inputBytes).TrimStart('\uFEFF');
        List<string[]> records = ReadCsv(text);
        if (records.Count == 0 || !records[0].Take(6).SequenceEqual(Languages))
        {
            throw new InvalidDataException("The mapping must start with the six English-first language columns.");
        }
        string[] header = records[0];
        if (header.Distinct(StringComparer.Ordinal).Count() != header.Length)
        {
            throw new InvalidDataException("The mapping has duplicate columns.");
        }
        Dictionary<string, int> columns = header.Select((name, index) => (name, index)).ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
        if (!columns.TryGetValue("Field", out int fieldColumn))
        {
            throw new InvalidDataException("The mapping requires a Field column.");
        }
        int sourceColumnCount = Languages.Count(language => columns.ContainsKey(language + " Source Ordinal"));
        if (sourceColumnCount is not 0 and not 6)
        {
            throw new InvalidDataException("Source ordinal columns must cover all six languages.");
        }
        bool sourceMetadata = sourceColumnCount == 6;
        List<FieldLocalizationNumberingInput> inputs = [];
        int languageOnlyCount = 0;
        foreach (string[] row in records.Skip(1))
        {
            cancellation.ThrowIfCancellationRequested();
            if (row.Length != header.Length)
            {
                throw new InvalidDataException("Every CSV record must have the same number of columns as the header.");
            }
            string field = row[fieldColumn];
            for (int languageIndex = 0; languageIndex < Languages.Length; languageIndex++)
            {
                string language = Languages[languageIndex];
                string key = row[languageIndex];
                if (key.Length == 0)
                {
                    if (sourceMetadata && row[columns[language + " Source Ordinal"]].Length != 0)
                    {
                        throw new InvalidDataException("A source ordinal cannot identify a missing key.");
                    }
                    continue;
                }
                string prefix = "$" + language + "_" + field + "_";
                int ordinal;
                if (sourceMetadata)
                {
                    if (!int.TryParse(row[columns[language + " Source Ordinal"]], NumberStyles.None, CultureInfo.InvariantCulture, out ordinal) || ordinal < 0)
                    {
                        throw new InvalidDataException("Keys require valid original Source Ordinal metadata.");
                    }
                    if (key != FieldLocalizationKey.Create(language, field, ordinal) && !IsLegacyKey(key, prefix))
                    {
                        throw new InvalidDataException("Keys must use canonical source ordinal syntax or legacy anchor/insertion syntax with authoritative Source Ordinal metadata.");
                    }
                }
                else if (!key.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(key.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out ordinal) || ordinal < 0 || key != FieldLocalizationKey.Create(language, field, ordinal))
                {
                    throw new InvalidDataException("The input must contain canonical original-ordinal keys for its field and language.");
                }
                bool languageOnly = language != "en" && columns.TryGetValue(language + " Status", out int statusColumn) && row[statusColumn] is "ConfirmedLanguageOnly" or "MeaningReviewedLanguageOnly" or "MeaningReviewedEmptySpeakerStub" or "UnmatchedInventory";
                if (languageOnly && (!columns.TryGetValue(language + " English Keys", out int referencesColumn) || row[referencesColumn].Length != 0))
                {
                    throw new InvalidDataException("Language-only status requires an explicit empty English Keys column.");
                }
                if (languageOnly)
                {
                    string basis = columns.TryGetValue(language + " Correspondence Basis", out int basisColumn) ? row[basisColumn] : string.Empty;
                    if (basis is not ("NativeUnmatchedCall" or "UnmatchedBankSlot" or "UnmatchedBankBlock" or "UserAcceptedUnique") && !basis.StartsWith("NoEnglishBankSlot", StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("An unmatched inventory slot requires structural correspondence evidence or an explicit UserAcceptedUnique review decision; different wording or a missing semantic translation is insufficient.");
                    }
                    languageOnlyCount++;
                }
                inputs.Add(new(key, language, field, ordinal));
            }
        }
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs);
        Dictionary<string, FieldLocalizationNumberingEntry> migration = entries.ToDictionary(entry => entry.OldKey, StringComparer.Ordinal);
        HashSet<string> englishKeys = entries.Where(entry => entry.Language == "en").Select(entry => entry.OldKey).ToHashSet(StringComparer.Ordinal);
        string[] retiredHeader = ["Field", .. Languages, "Ordinal", .. Languages.Select(language => language + " Insertion Anchor English Key")];
        List<string[]> retired = [retiredHeader];
        string[] retainedColumns = header.Where(name => name != "Ordinal" && !Languages.Any(language => name == language + " Insertion Anchor English Key")).ToArray();
        string RenameColumn(string name)
        {
            foreach (string language in Languages)
            {
                if (name == language + " Insertion Group")
                {
                    return language + " Correspondence Group";
                }
                if (name == language + " Numbering Kind")
                {
                    return language + " Translation Kind";
                }
            }
            return name;
        }
        string[] addedColumns = [.. Languages.Select(language => language + " Source Ordinal").Where(name => !columns.ContainsKey(name)), .. Languages.Select(language => language + " Source Unused Across Languages").Where(name => !columns.ContainsKey(name))];
        string[] outputHeader = [.. retainedColumns.Select(RenameColumn), .. addedColumns];
        if (outputHeader.Distinct(StringComparer.Ordinal).Count() != outputHeader.Length)
        {
            throw new InvalidDataException("Renamed correspondence metadata columns must remain unique.");
        }
        Dictionary<string, int> outputColumns = outputHeader.Select((name, index) => (name, index)).ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
        List<string[]> transformed = [outputHeader];
        foreach (string[] source in records.Skip(1))
        {
            cancellation.ThrowIfCancellationRequested();
            retired.Add(retiredHeader.Select(name => columns.TryGetValue(name, out int column) ? source[column] : string.Empty).ToArray());
            string[] row = [.. retainedColumns.Select(name => source[columns[name]]), .. addedColumns.Select(_ => string.Empty)];
            for (int index = 0; index < Languages.Length; index++)
            {
                string language = Languages[index];
                if (source[index].Length != 0)
                {
                    FieldLocalizationNumberingEntry entry = migration[source[index]];
                    row[index] = entry.NewKey;
                    if (!sourceMetadata)
                    {
                        row[outputColumns[language + " Source Ordinal"]] = entry.SourceOrdinal.ToString(CultureInfo.InvariantCulture);
                    }
                    if (!columns.ContainsKey(language + " Source Unused Across Languages"))
                    {
                        row[outputColumns[language + " Source Unused Across Languages"]] = columns.TryGetValue("Unused Across Languages", out int unusedColumn) ? source[unusedColumn] : string.Empty;
                    }
                }
                foreach (string referenceKind in new[] { " English Keys", " English Candidates" })
                {
                    if (columns.TryGetValue(language + referenceKind, out int referencesColumn) && source[referencesColumn].Length != 0)
                    {
                        string[] references = source[referencesColumn].Split(';');
                        if (source[index].Length == 0 || references.Distinct(StringComparer.Ordinal).Count() != references.Length || references.Any(key => !englishKeys.Contains(key) || migration[key].Field != source[fieldColumn]))
                        {
                            throw new InvalidDataException("English references must identify distinct existing English keys in the same field.");
                        }
                        row[outputColumns[language + referenceKind]] = string.Join(';', references.Select(key => migration[key].NewKey));
                    }
                }
            }
            transformed.Add(row);
        }
        List<string[]> correspondence = ReviewedCorrespondence.Create(transformed, entries);
        cancellation.ThrowIfCancellationRequested();
        Directory.CreateDirectory(output);
        string incomplete = Path.Combine(output, "INCOMPLETE");
        File.WriteAllText(incomplete, "Key numbering has not finished.", new UTF8Encoding(false));
        string mappingPath = Path.Combine(output, "mapping.csv");
        WriteCsv(mappingPath, transformed);
        WriteCsv(Path.Combine(output, "retired-numbering.csv"), retired);
        WriteCsv(Path.Combine(output, "correspondence.csv"), correspondence);
        WriteCsv(Path.Combine(output, "key-map.csv"), new[] { new[] { "Old Key", "New Key", "Language", "Field", "Source Ordinal" } }.Concat(entries.Select(entry => new[] { entry.OldKey, entry.NewKey, entry.Language, entry.Field, entry.SourceOrdinal.ToString(CultureInfo.InvariantCulture) })));
        string mappingHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mappingPath)));
        object report = new
        {
            SchemaVersion = 2,
            KeyRule = Rule,
            SourceSha256 = Convert.ToHexString(SHA256.HashData(inputBytes)),
            MappingSha256 = mappingHash,
            Records = transformed.Count - 1,
            Keys = entries.Count,
            CorrespondenceGroups = correspondence.Count - 1,
            Fields = records.Skip(1).Select(row => row[fieldColumn]).Distinct(StringComparer.Ordinal).Count(),
            FieldsWithKeys = entries.Select(entry => entry.Field).Distinct(StringComparer.Ordinal).Count(),
            LanguageOnlyKeys = languageOnlyCount,
            SourceOrdinalsPreserved = true,
            CorrespondencesAndStatusesPreserved = true,
            RowOrderPreserved = true,
            RetiredNumberingRecords = retired.Count - 1,
            StatusCounts = records.Skip(1).SelectMany(row => Languages.Skip(1).Where(language => row[columns[language]].Length != 0 && columns.ContainsKey(language + " Status")).Select(language => row[columns[language + " Status"]])).GroupBy(status => status, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count()),
            PerLanguage = entries.GroupBy(entry => entry.Language).ToDictionary(group => group.Key, group => group.Count()),
            Examples = entries.Take(12).ToArray()
        };
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        StringBuilder summary = new();
        summary.AppendLine("# Localization source ordinal keys").AppendLine();
        summary.AppendLine($"[mapping.csv](mapping.csv) contains {transformed.Count - 1} records and {entries.Count} unique keys. [key-map.csv](key-map.csv) records every original key, new key, language, field and source ordinal.").AppendLine();
        summary.AppendLine($"[correspondence.csv](correspondence.csv) contains {correspondence.Count - 1} explicit correspondence groups, independently of the numeric key positions.").AppendLine();
        summary.AppendLine("Each key uses its language's original source ordinal with at least four digits. Correspondence references, statuses, metadata and row order are preserved. [retired-numbering.csv](retired-numbering.csv) retains the original row keys, global ordinal and insertion anchors.").AppendLine();
        summary.AppendLine("| Original key | New key | Source ordinal |");
        summary.AppendLine("| --- | --- | ---: |");
        foreach (FieldLocalizationNumberingEntry entry in entries.Take(12))
        {
            summary.AppendLine($"| `{entry.OldKey}` | `{entry.NewKey}` | {entry.SourceOrdinal} |");
        }
        summary.AppendLine().AppendLine($"Mapping SHA256: `{mappingHash}`.");
        File.WriteAllText(Path.Combine(output, "summary.md"), summary.ToString(), new UTF8Encoding(false));
        File.Delete(incomplete);
        return $"Assigned {entries.Count} source ordinal keys; {languageOnlyCount} explicitly unmatched inventory keys. Report: {output}";
    }

    private static bool IsLegacyKey(string key, string prefix)
    {
        if (!key.StartsWith(prefix, StringComparison.Ordinal) || key.Length < prefix.Length + 6)
        {
            return false;
        }
        return int.TryParse(key.AsSpan(prefix.Length, key.Length - prefix.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out int anchor)
            && int.TryParse(key.AsSpan(key.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out int insertion)
            && key == prefix + anchor.ToString("D4", CultureInfo.InvariantCulture) + insertion.ToString("D2", CultureInfo.InvariantCulture);
    }

    private static List<string[]> ReadCsv(string text)
    {
        using StringReader reader = new(text);
        List<string[]> records = [];
        List<string> row = [];
        StringBuilder value = new();
        bool quoted = false;
        bool closedQuote = false;
        bool started = false;
        while (reader.Read() is int character && character >= 0)
        {
            char current = (char)character;
            if (quoted)
            {
                if (current == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        value.Append('"');
                    }
                    else
                    {
                        quoted = false;
                        closedQuote = true;
                    }
                }
                else
                {
                    value.Append(current);
                }
                continue;
            }
            if (current is ',' or '\r' or '\n')
            {
                row.Add(value.ToString());
                value.Clear();
                closedQuote = false;
                started = false;
                if (current != ',')
                {
                    records.Add(row.ToArray());
                    row.Clear();
                    if (current == '\r' && reader.Peek() == '\n')
                    {
                        reader.Read();
                    }
                }
            }
            else if (closedQuote)
            {
                throw new InvalidDataException("Unexpected data after a quoted CSV value.");
            }
            else if (current == '"')
            {
                if (started)
                {
                    throw new InvalidDataException("A CSV quote must start a value.");
                }
                quoted = true;
                started = true;
            }
            else
            {
                started = true;
                value.Append(current);
            }
        }
        if (quoted)
        {
            throw new InvalidDataException("Unterminated quoted CSV value.");
        }
        if (row.Count != 0 || started || closedQuote)
        {
            row.Add(value.ToString());
            records.Add(row.ToArray());
        }
        return records;
    }

    private static void WriteCsv(string path, IEnumerable<string[]> records)
    {
        using StreamWriter writer = new(path, false, new UTF8Encoding(false));
        foreach (string[] row in records)
        {
            writer.WriteLine(string.Join(',', row.Select(value => value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value)));
        }
    }
}
