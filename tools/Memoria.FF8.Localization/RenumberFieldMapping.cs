using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Localization;

internal static class RenumberFieldMapping
{
    private static readonly string[] Languages = ["en", "fr", "de", "es", "it", "jp"];
    private const string Rule = "common-English-base-v2: $<language>_<field>_<English base:D4><slot:D2>";

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
        string[] missingReferences = Languages.Skip(1).Where(language => !header.Contains(language + " English Keys", StringComparer.Ordinal)).ToArray();
        if (missingReferences.Length != 0)
        {
            Dictionary<string, int> originalColumns = header.Select((name, index) => (name, index)).ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
            for (int rowIndex = 1; rowIndex < records.Count; rowIndex++)
            {
                string[] source = records[rowIndex];
                if (source.Length != header.Length)
                {
                    throw new InvalidDataException("Every CSV record must have the same number of columns as the header.");
                }
                records[rowIndex] = [.. source, .. missingReferences.Select(language => source[originalColumns[language]].Length != 0 && (!originalColumns.TryGetValue(language + " Status", out int statusColumn) || source[statusColumn] is not ("ConfirmedLanguageOnly" or "MeaningReviewedLanguageOnly" or "MeaningReviewedEmptySpeakerStub" or "UnmatchedInventory")) ? source[0] : string.Empty)];
            }
            header = [.. header, .. missingReferences.Select(language => language + " English Keys")];
            records[0] = header;
        }
        Dictionary<string, int> columns = header.Select((name, index) => (name, index)).ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
        int sourceColumnCount = Languages.Count(language => columns.ContainsKey(language + " Source Ordinal"));
        if (sourceColumnCount is not 0 and not 6)
        {
            throw new InvalidDataException("Source ordinal columns must cover all six languages.");
        }
        bool sourceMetadata = sourceColumnCount == 6;
        if (!columns.TryGetValue("Field", out int fieldColumn))
        {
            throw new InvalidDataException("The mapping requires a Field column.");
        }
        List<FieldLocalizationNumberingInput> inputs = [];
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
                string key = row[languageIndex];
                if (key.Length == 0)
                {
                    if (sourceMetadata && row[columns[Languages[languageIndex] + " Source Ordinal"]].Length != 0)
                    {
                        throw new InvalidDataException("A source ordinal cannot identify a missing key.");
                    }
                    continue;
                }
                string language = Languages[languageIndex];
                string prefix = "$" + language + "_" + field + "_";
                int ordinal;
                if (sourceMetadata)
                {
                    if (!int.TryParse(row[columns[language + " Source Ordinal"]], NumberStyles.None, CultureInfo.InvariantCulture, out ordinal) || ordinal < 0 || !key.StartsWith(prefix, StringComparison.Ordinal) || key.Length < prefix.Length + 6 || !int.TryParse(key.AsSpan(prefix.Length, key.Length - prefix.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out int anchor) || !int.TryParse(key.AsSpan(key.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out int insertion) || key != FieldLocalizationKey.Create(language, field, anchor, insertion))
                    {
                        throw new InvalidDataException("Numbered keys require canonical anchor/insertion syntax and valid original Source Ordinal metadata.");
                    }
                }
                else if (!key.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(key.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out ordinal) || ordinal < 0 || key != prefix + ordinal.ToString("D4", CultureInfo.InvariantCulture))
                {
                    throw new InvalidDataException("The input must contain canonical original-ordinal keys for its field and language.");
                }
                bool languageOnly = false;
                if (language != "en" && columns.TryGetValue(language + " Status", out int statusColumn))
                {
                    languageOnly = row[statusColumn] is "ConfirmedLanguageOnly" or "MeaningReviewedLanguageOnly" or "MeaningReviewedEmptySpeakerStub" or "UnmatchedInventory";
                }
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
                }
                string[] englishReferences;
                if (language == "en")
                {
                    englishReferences = [];
                }
                else if (columns.TryGetValue(language + " English Keys", out int englishReferencesColumn))
                {
                    englishReferences = row[englishReferencesColumn].Length == 0 ? [] : row[englishReferencesColumn].Split(';');
                }
                else
                {
                    englishReferences = row[0].Length == 0 ? [] : [row[0]];
                }
                string kind = columns.TryGetValue(language + " Numbering Kind", out int kindColumn) ? row[kindColumn] : string.Empty;
                if (kind is not ("" or "ContextVariant" or "ContextualTranslation"))
                {
                    throw new InvalidDataException("Unsupported key numbering kind.");
                }
                if (kind == "ContextVariant")
                {
                    row[kindColumn] = "ContextualTranslation";
                }
                string? insertionGroup = columns.TryGetValue(language + " Insertion Group", out int insertionGroupColumn) && row[insertionGroupColumn].Length != 0 ? row[insertionGroupColumn] : null;
                string? insertionAnchor = columns.TryGetValue(language + " Insertion Anchor English Key", out int insertionAnchorColumn) && row[insertionAnchorColumn].Length != 0 ? row[insertionAnchorColumn] : null;
                inputs.Add(new(key, language, field, ordinal, languageOnly, englishReferences, insertionGroup, insertionAnchor));
            }
        }
        IReadOnlyList<FieldLocalizationNumberingEntry> entries = FieldLocalizationNumbering.Allocate(inputs);
        Dictionary<string, FieldLocalizationNumberingEntry> migration = entries.ToDictionary(entry => entry.OldKey, StringComparer.Ordinal);
        foreach (FieldLocalizationNumberingInput inputKey in inputs)
        {
            foreach (string reference in inputKey.EnglishKeys ?? [])
            {
                if (migration[inputKey.Key].AnchorOrdinal != migration[reference].AnchorOrdinal)
                {
                    throw new InvalidDataException("Every correspondence must retain the same common English base.");
                }
            }
        }
        HashSet<string> englishKeys = entries.Where(entry => entry.Language == "en").Select(entry => entry.OldKey).ToHashSet(StringComparer.Ordinal);
        List<string[]> transformed = [];
        transformed.Add(sourceMetadata ? header : [.. header, .. Languages.Select(language => language + " Source Ordinal")]);
        foreach (string[] source in records.Skip(1))
        {
            cancellation.ThrowIfCancellationRequested();
            string[] row = sourceMetadata ? source.ToArray() : [.. source, .. Languages.Select(_ => string.Empty)];
            for (int index = 0; index < Languages.Length; index++)
            {
                if (source[index].Length != 0)
                {
                    FieldLocalizationNumberingEntry entry = migration[source[index]];
                    row[index] = entry.NewKey;
                    row[sourceMetadata ? columns[Languages[index] + " Source Ordinal"] : header.Length + index] = entry.SourceOrdinal.ToString(CultureInfo.InvariantCulture);
                }
                foreach (string referenceKind in new[] { " English Keys", " English Candidates", " Insertion Anchor English Key" })
                {
                    if (columns.TryGetValue(Languages[index] + referenceKind, out int referencesColumn) && source[referencesColumn].Length != 0)
                    {
                        string[] references = source[referencesColumn].Split(';');
                        if (source[index].Length == 0 || references.Distinct(StringComparer.Ordinal).Count() != references.Length || references.Any(key => !englishKeys.Contains(key) || migration[key].Field != source[fieldColumn]))
                        {
                            throw new InvalidDataException("English references must identify distinct existing English keys in the same field.");
                        }
                        row[referencesColumn] = string.Join(';', references.Select(key => migration[key].NewKey));
                    }
                }
            }
            transformed.Add(row);
        }
        transformed = NumberedMappingRows.Normalize(transformed, migration);
        cancellation.ThrowIfCancellationRequested();
        Directory.CreateDirectory(output);
        string incomplete = Path.Combine(output, "INCOMPLETE");
        File.WriteAllText(incomplete, "Key numbering has not finished.", new UTF8Encoding(false));
        string mappingPath = Path.Combine(output, "mapping.csv");
        WriteCsv(mappingPath, transformed);
        WriteCsv(Path.Combine(output, "key-map.csv"), new[] { new[] { "Old Key", "New Key", "Language", "Field", "Source Ordinal", "Anchor Ordinal", "Insertion" } }.Concat(entries.Select(entry => new[] { entry.OldKey, entry.NewKey, entry.Language, entry.Field, entry.SourceOrdinal.ToString(CultureInfo.InvariantCulture), entry.AnchorOrdinal.ToString(CultureInfo.InvariantCulture), entry.Insertion.ToString(CultureInfo.InvariantCulture) })));
        string mappingHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(mappingPath)));
        object report = new
        {
            SchemaVersion = 1,
            KeyRule = Rule,
            SourceSha256 = Convert.ToHexString(SHA256.HashData(inputBytes)),
            MappingSha256 = mappingHash,
            Records = transformed.Count - 1,
            Keys = entries.Count,
            Fields = records.Skip(1).Select(row => row[fieldColumn]).Distinct(StringComparer.Ordinal).Count(),
            FieldsWithKeys = entries.Select(entry => entry.Field).Distinct(StringComparer.Ordinal).Count(),
            KeysAtSlotZero = entries.Count(entry => entry.Insertion == 0),
            KeysWithNonzeroSlot = entries.Count(entry => entry.Insertion != 0),
            LanguageOnlyKeys = inputs.Count(inputKey => inputKey.IsLanguageOnly),
            SharedInsertionGroups = inputs.Where(inputKey => inputKey.InsertionGroup is not null).Select(inputKey => inputKey.InsertionGroup).Distinct(StringComparer.Ordinal).Count(),
            SharedInsertionGroupKeys = inputs.Count(inputKey => inputKey.InsertionGroup is not null),
            ContextualTranslationKeys = records.Skip(1).Sum(row => Languages.Skip(1).Count(language => columns.TryGetValue(language + " Numbering Kind", out int kindColumn) && row[kindColumn] == "ContextualTranslation")),
            MaximumInsertion = entries.Select(entry => entry.Insertion).DefaultIfEmpty(0).Max(),
            InsertionAnchor = "Previous common English component in the source language; leading insertions use base zero",
            InsertionRange = "Slots 01..99 are reserved globally across languages for distinct content; overflow is rejected",
            Classification = "Established script correspondence takes priority over localized meaning; contextual wording changes never allocate an insertion; unproved absence remains explicit",
            CommonBasePerEnglishReference = true,
            SourceOrdinalsPreserved = true,
            CorrespondencesAndStatusesPreserved = true,
            StatusCounts = records.Skip(1).SelectMany(row => Languages.Skip(1).Where(language => row[columns[language]].Length != 0 && columns.ContainsKey(language + " Status")).Select(language => row[columns[language + " Status"]])).GroupBy(status => status, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count()),
            PerLanguage = entries.GroupBy(entry => entry.Language).ToDictionary(group => group.Key, group => group.Count()),
            Examples = entries.Where(entry => entry.Insertion != 0).Take(12).ToArray()
        };
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        StringBuilder summary = new();
        summary.AppendLine("# Localization key numbering").AppendLine();
        summary.AppendLine($"[mapping.csv](mapping.csv) contains {transformed.Count - 1} records and {entries.Count} unique keys. [key-map.csv](key-map.csv) records every original key, new key, source ordinal, anchor and insertion number.").AppendLine();
        summary.AppendLine($"Corresponding script messages share the same English slot even when their localized wording has a different meaning. {inputs.Count(inputKey => inputKey.IsLanguageOnly)} explicitly unmatched inventory keys use additional slots. Explicit Insertion Group metadata shares one suffix across corresponding non-English inventory entries. Wording alone cannot establish an extra message or defeat an established event correspondence. Source ordinals remain metadata.").AppendLine();
        summary.AppendLine("Explicit Insertion Anchor English Key metadata can retain a shared insertion base when corresponding resources occur at different source positions. Many-to-many correspondence components share their earliest English base. Duplicate identities retain distinct suffix slots; references and statuses are preserved. Technical uncertainty alone does not prove a semantic counterpart. More than 99 additional slots per base is rejected; no digits are truncated.").AppendLine();
        summary.AppendLine("| Original key | New key | Source ordinal | Anchor | Insertion |");
        summary.AppendLine("| --- | --- | ---: | ---: | ---: |");
        foreach (FieldLocalizationNumberingEntry entry in entries.Where(entry => entry.Insertion != 0).Take(12))
        {
            summary.AppendLine($"| `{entry.OldKey}` | `{entry.NewKey}` | {entry.SourceOrdinal} | {entry.AnchorOrdinal} | {entry.Insertion:D2} |");
        }
        summary.AppendLine().AppendLine($"Mapping SHA256: `{mappingHash}`.");
        File.WriteAllText(Path.Combine(output, "summary.md"), summary.ToString(), new UTF8Encoding(false));
        File.Delete(incomplete);
        return $"Renumbered {entries.Count} keys with common English bases; {inputs.Count(inputKey => inputKey.IsLanguageOnly)} explicitly unmatched inventory keys. Report: {output}";
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
