using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Localization;

internal static class TranslationReviewCommands
{
    private static readonly JsonSerializerOptions ReportOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Run(string command, string input, string reference, string reportPath, CancellationToken cancellation, string? glossaryPath = null)
    {
        if (!reference.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Translation review supports --reference en.");
        }
        SpeakerGlossary glossary = glossaryPath is null ? new(new(StringComparer.Ordinal), new(StringComparer.Ordinal)) : ReadSpeakerGlossary(glossaryPath, reportPath);
        using ReportDestination report = OpenReport(input, reportPath);
        List<ReviewFinding> findings = [];
        int targetColumns = 0;
        int withoutReference = 0;
        string? referenceColumn = null;
        string? referenceEdition = null;
        string? sourceEdition = null;
        IReadOnlyDictionary<string, string> defaultSpeakerNames = new Dictionary<string, string>();
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> fieldNames = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        bool fix = command == "fix";

        LocalizationProject Review(LocalizationProject project)
        {
            if (!IsRussianLanguage(project.Manifest.TargetLanguage))
            {
                throw new InvalidDataException($"Translation review currently supports Russian targets. Package target is '{project.Manifest.TargetLanguage}'.");
            }
            string[] columns = project.Manifest.ReferenceLanguages.Where(pair => pair.Value.Equals("en", StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToArray();
            if (columns.Length != 1)
            {
                throw new InvalidDataException(columns.Length == 0
                    ? "The package needs a declared English reading column. Run add-references --language en to populate it."
                    : "The package declares multiple English reading columns. Resolve the ambiguous reference before review.");
            }
            referenceColumn = columns[0];
            if (project.Manifest.ExtensionData.TryGetValue(LocalizationProjectIO.ReferenceEditionsMember, out JsonElement editions) &&
                editions.ValueKind == JsonValueKind.Object && editions.TryGetProperty(referenceColumn, out JsonElement edition) &&
                edition.ValueKind == JsonValueKind.String && edition.GetString() is ("Classic" or "Remastered"))
            {
                referenceEdition = edition.GetString();
            }
            sourceEdition = project.Manifest.SourceEdition;
            Dictionary<string, Reading> baseReadings = project.Entries.ToDictionary(row => row.Key,
                row => SelectReading(project.Manifest, row, referenceColumn, sourceEdition, referenceEdition, isOverride: false), StringComparer.Ordinal);
            Dictionary<string, string> names = new(ReadCharacterNames(project, baseReadings), StringComparer.Ordinal);
            foreach ((string source, string translation) in glossary.NpcNames)
            {
                names.Add(source, translation);
            }
            defaultSpeakerNames = names;
            fieldNames = ReadFieldSpeakerNames(project, referenceColumn, baseReadings, names, glossary.NpcNames, findings, cancellation);
            LocalizationRecord[] rows = project.Entries.Select(row =>
            {
                cancellation.ThrowIfCancellationRequested();
                string? target = ReviewColumn(row, project.Manifest.TargetColumn, row.Target, baseReadings[row.Key]);
                Dictionary<string, JsonElement>? metadata = null;
                if (row.ExtensionData.TryGetValue(LocalizationProjectIO.EditionTargetsColumn, out JsonElement overrides))
                {
                    JsonObject? updatedOverrides = null;
                    foreach (JsonProperty entry in overrides.EnumerateObject())
                    {
                        string? original = entry.Value.GetString();
                        string editionName = entry.Name.Split(':', 2)[0];
                        Reading reading = SelectReading(project.Manifest, row, referenceColumn, editionName, referenceEdition, isOverride: true);
                        string? updated = ReviewColumn(row, LocalizationProjectIO.EditionTargetsColumn + "/" + entry.Name, original, reading);
                        if (updated != original)
                        {
                            updatedOverrides ??= JsonNode.Parse(overrides.GetRawText())!.AsObject();
                            updatedOverrides[entry.Name] = updated;
                        }
                    }
                    if (updatedOverrides is not null)
                    {
                        metadata = new(row.ExtensionData, StringComparer.Ordinal)
                        {
                            [LocalizationProjectIO.EditionTargetsColumn] = JsonSerializer.SerializeToElement(updatedOverrides)
                        };
                    }
                }
                return target == row.Target && metadata is null ? row : row with { Target = target, ExtensionData = metadata ?? row.ExtensionData };
            }).ToArray();
            return project with { Entries = rows };
        }

        string? ReviewColumn(LocalizationRecord row, string column, string? target, Reading reading)
        {
            if (string.IsNullOrEmpty(target) || target == LocalizationValue.EmptyTarget)
            {
                return target;
            }
            targetColumns++;
            if (reading.Text is null)
            {
                withoutReference++;
                findings.Add(new(reading.Rule!, row.Category, row.File, row.Key, column, reading.Message!, false, false));
            }
            bool editionTarget = column.StartsWith(LocalizationProjectIO.EditionTargetsColumn + "/", StringComparison.Ordinal);
            IReadOnlyDictionary<string, string>? names = editionTarget ? null : fieldNames.GetValueOrDefault(row.File) ?? defaultSpeakerNames;
            TranslationReviewResult result = TranslationReviewRules.Review(target, reading.Text, row.Category, row.Key, fix, names, IsChoiceBound(row), !editionTarget && HasChoiceLineMap(row, target), glossary.PartyAliases);
            string? rejected = null;
            if (result.Target != target)
            {
                string? failure = TargetValidationError(row with { Target = result.Target });
                if (failure is not null && failure != TargetValidationError(row with { Target = target }))
                {
                    rejected = failure;
                }
            }
            foreach (TranslationReviewDiagnostic diagnostic in result.Diagnostics)
            {
                bool blocked = diagnostic.Fixed && rejected is not null;
                string message = blocked ? $"{diagnostic.Message} Automatic edit rejected by target validation: {rejected}" : diagnostic.Message;
                findings.Add(new(diagnostic.Rule, row.Category, row.File, row.Key, column, message, diagnostic.Fixable && !blocked, diagnostic.Fixed && !blocked));
            }
            return rejected is null ? result.Target : target;
        }

        int fixedCount = 0;
        int unresolved = 0;
        LocalizationProject ReviewAndStageReport(LocalizationProject project)
        {
            LocalizationProject reviewed = Review(project);
            RuleSummary[] rules = findings.GroupBy(finding => finding.Rule, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new RuleSummary(group.Key, group.Count(finding => finding.Fixed), group.Count(finding => !finding.Fixed && finding.Fixable), group.Count(finding => !finding.Fixable))).ToArray();
            fixedCount = findings.Count(finding => finding.Fixed);
            unresolved = findings.Count - fixedCount;
            report.Stage(new
            {
                schemaVersion = 1,
                command,
                reviewed.Manifest.ProjectId,
                referenceColumn,
                referenceEdition,
                sourceEdition,
                records = reviewed.Entries.Count,
                targetColumns,
                withoutReference,
                glossaryEntries = glossary.NpcNames.Count + glossary.PartyAliases.Count,
                fixedCount,
                unresolved,
                rules,
                findings
            });
            return reviewed;
        }
        if (fix)
        {
            LocalizationProjectIO.UpdateJsonTargets(input, ReviewAndStageReport, cancellation);
        }
        else
        {
            ReviewAndStageReport(LocalizationProjectIO.ReadJsonForReview(input));
        }
        report.Publish();
        Console.WriteLine($"{command}: {targetColumns} target columns reviewed, {fixedCount} fixed, {unresolved} findings remaining.");
        return unresolved == 0 ? 0 : 1;
    }

    private static string ValidateReportPath(string input, string reportPath)
    {
        string package = ResolveExistingPath(Path.GetFullPath(input));
        string report = Path.GetFullPath(reportPath);
        string file = Path.GetFileName(report);
        if (string.IsNullOrEmpty(file) || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Directory.Exists(report))
        {
            throw new ArgumentException("The report path must name a file.");
        }
        string packagePath = Path.TrimEndingDirectorySeparator(package);
        string reportDirectory = Path.GetDirectoryName(report)!;
        string destination = Path.Combine(ResolveExistingPath(reportDirectory), file);
        string relative = Path.GetRelativePath(packagePath, destination);
        if ((!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) ||
            string.Equals(Path.GetFullPath(input), report, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new ArgumentException("Write the review report outside the input localization package.");
        }
        return destination;
    }

    public static ReportDestination OpenReport(string input, string reportPath)
    {
        string destination = ValidateReportPath(input, reportPath);
        string directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        destination = ValidateReportPath(input, destination);
        if (File.Exists(destination))
        {
            using FileStream originalReport = new(destination, FileMode.Open, FileAccess.Write, FileShare.Read | FileShare.Delete);
        }
        return new ReportDestination(input, destination);
    }

    private static string ResolveExistingPath(string path)
    {
        string root = Path.GetPathRoot(path)!;
        string current = root;
        foreach (string segment in path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                current = new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true)?.FullName
                    ?? throw new IOException("Cannot resolve a directory link in the report path.");
            }
            else if (File.Exists(current))
            {
                throw new IOException($"Report directory component '{current}' is an existing file.");
            }
        }
        return current;
    }

    private static SpeakerGlossary ReadSpeakerGlossary(string path, string reportPath)
    {
        string source = Path.GetFullPath(path);
        string destination = Path.GetFullPath(reportPath);
        string actualSource = Path.Combine(ResolveExistingPath(Path.GetDirectoryName(source)!), Path.GetFileName(source));
        string actualDestination = Path.Combine(ResolveExistingPath(Path.GetDirectoryName(destination)!), Path.GetFileName(destination));
        if (File.Exists(actualSource))
        {
            actualSource = File.ResolveLinkTarget(actualSource, returnFinalTarget: true)?.FullName ?? actualSource;
        }
        if (File.Exists(actualDestination))
        {
            actualDestination = File.ResolveLinkTarget(actualDestination, returnFinalTarget: true)?.FullName ?? actualDestination;
        }
        if (string.Equals(actualSource, actualDestination, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new ArgumentException("Choose a report file distinct from the speaker glossary.");
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(source));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A speaker glossary must be a JSON object of approved speaker labels and party aliases.");
        }
        Dictionary<string, string> npcNames = new(StringComparer.Ordinal);
        Dictionary<string, string> partyAliases = new(StringComparer.Ordinal);
        HashSet<string> labels = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty entry in document.RootElement.EnumerateObject())
        {
            if (!labels.Add(entry.Name))
            {
                throw new InvalidDataException($"Duplicate speaker glossary label '{entry.Name}'.");
            }
            string? value = entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString() : null;
            if (value is not null && TranslationReviewRules.IsGlossaryEntry(entry.Name, value))
            {
                npcNames.Add(entry.Name, value);
            }
            else if (value is not null && TranslationReviewRules.IsPartyAlias(entry.Name, value))
            {
                partyAliases.Add(entry.Name, value);
            }
            else
            {
                throw new InvalidDataException($"Invalid speaker glossary entry '{entry.Name}'. Use an exact English NPC label with plain Russian text, or a Cyrillic party alias with its canonical Char tag.");
            }
        }
        return new(npcNames, partyAliases);
    }

    private static Dictionary<string, string> ReadCharacterNames(LocalizationProject project, IReadOnlyDictionary<string, Reading> baseReadings)
    {
        const string file = "kernel_characters_name";
        Dictionary<string, LocalizationRecord> records = project.Entries.Where(row => row.Category == "Characters" && row.File == file)
            .ToDictionary(row => row.Key, StringComparer.Ordinal);
        Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase);
        for (int ordinal = 0; ordinal <= 10; ordinal++)
        {
            string english = GameNames.DefaultName(GameNames.CharacterSubject(ordinal));
            string canonicalKey = "$" + project.Manifest.FieldScriptBaseLanguage + "_" + file + "_" + ordinal.ToString("D4", CultureInfo.InvariantCulture);
            if (!records.TryGetValue(canonicalKey, out LocalizationRecord? row) || string.IsNullOrWhiteSpace(row.Target) || row.Target == LocalizationValue.EmptyTarget ||
                !GameNames.IsValidDraft(row.Target) || row.Target.Contains('{') || row.Target.Contains('}'))
            {
                continue;
            }
            string? reference = baseReadings[row.Key].Text;
            if (string.Equals(reference, english, StringComparison.OrdinalIgnoreCase) || LocalizationProjectIO.MatchesRecordedSourceText(row, english) == true)
            {
                names.Add(english, row.Target);
            }
        }
        return names;
    }

    private static bool IsChoiceBound(LocalizationRecord row)
    {
        if (row.Category != "Fields")
        {
            return false;
        }
        if (FieldEvidence(row, "Choice Line Map").ValueKind != JsonValueKind.Undefined)
        {
            return true;
        }
        JsonElement contexts = FieldEvidence(row, "Call Contexts");
        return contexts.ValueKind == JsonValueKind.Array && contexts.EnumerateArray().Any(context =>
            context.ValueKind == JsonValueKind.Object && context.TryGetProperty("Opcode", out JsonElement opcode) && opcode.ValueKind == JsonValueKind.String && opcode.GetString() is ("ASK" or "AASK"));
    }

    private static bool HasChoiceLineMap(LocalizationRecord row, string target)
    {
        if (row.Category != "Fields")
        {
            return false;
        }
        JsonElement map = FieldEvidence(row, "Choice Line Map");
        if (map.ValueKind != JsonValueKind.Array || map.GetArrayLength() == 0)
        {
            return false;
        }
        HashSet<int> source = [];
        HashSet<int> translated = [];
        int lineCount = LocalizationCatalog.CountAuthoredLines(target);
        return map.EnumerateArray().All(entry => entry.ValueKind == JsonValueKind.Object &&
            entry.TryGetProperty("SourceLine", out JsonElement from) && from.ValueKind == JsonValueKind.Number && from.TryGetInt32(out int fromLine) &&
            fromLine >= 0 && source.Add(fromLine) && entry.TryGetProperty("TargetLine", out JsonElement to) && to.ValueKind == JsonValueKind.Number &&
            to.TryGetInt32(out int toLine) && toLine >= 0 && toLine < lineCount && translated.Add(toLine));
    }

    private static JsonElement FieldEvidence(LocalizationRecord row, string name)
    {
        if (!row.ExtensionData.TryGetValue(name, out JsonElement value) &&
            row.ExtensionData.TryGetValue("sourceContext", out JsonElement sourceContext) && sourceContext.ValueKind == JsonValueKind.Object)
        {
            sourceContext.TryGetProperty(name, out value);
        }
        return value.ValueKind == JsonValueKind.String ? JsonSerializer.Deserialize<JsonElement>(value.GetString()!) : value;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReadFieldSpeakerNames(LocalizationProject project, string referenceColumn, IReadOnlyDictionary<string, Reading> baseReadings, IReadOnlyDictionary<string, string> defaultSpeakerNames, IReadOnlyDictionary<string, string> declaredSpeakers, List<ReviewFinding> findings, CancellationToken cancellation)
    {
        List<SpeakerWitness> witnesses = [];
        foreach (LocalizationRecord row in project.Entries)
        {
            cancellation.ThrowIfCancellationRequested();
            if (row.Category != "Fields" || row.Target is not string target || baseReadings[row.Key].Text is not string english || IsChoiceBound(row) ||
                !TranslationReviewRules.TryGetGlossaryWitness(target, english, out string sourceSpeaker, out string localizedSpeaker))
            {
                continue;
            }
            IReadOnlyList<string> referenceKeys = row.ReferenceKeys.GetValueOrDefault(referenceColumn) ?? [];
            if (referenceKeys.Count > 1)
            {
                continue;
            }
            string canonical = (referenceKeys.SingleOrDefault() ?? row.Key).Split("_ctx_", 2, StringSplitOptions.None)[0];
            string message = FieldMessageContext.TryParseTag(0, english, out _, out int prefixLength) ? english[prefixLength..] : english;
            witnesses.Add(new(row.File, row.Key, canonical, message, sourceSpeaker, localizedSpeaker));
        }
        Dictionary<string, IReadOnlyDictionary<string, string>> fields = new(StringComparer.Ordinal);
        foreach (var field in witnesses.GroupBy(witness => witness.File, StringComparer.Ordinal))
        {
            Dictionary<string, string> names = new(defaultSpeakerNames, StringComparer.Ordinal);
            foreach (var source in field.GroupBy(witness => witness.Source, StringComparer.Ordinal))
            {
                if (declaredSpeakers.TryGetValue(source.Key, out string? declared))
                {
                    foreach (SpeakerWitness conflict in source.Where(witness => witness.Target != declared))
                    {
                        names.Remove(source.Key);
                        findings.Add(new("speaker-glossary-conflict", "Fields", field.Key, conflict.Key, project.Manifest.TargetColumn,
                            $"The authored speaker '{conflict.Target}' for '{source.Key}' conflicts with the glossary value '{declared}'. Review this field before applying the glossary.", false, false));
                    }
                    continue;
                }
                string[] candidates = source.Select(witness => witness.Target).Distinct(StringComparer.Ordinal).Take(2).ToArray();
                if (candidates.Length == 1 && source.Select(witness => witness.CanonicalKey).Distinct(StringComparer.Ordinal).Skip(1).Any() &&
                    source.Select(witness => witness.Message).Distinct(StringComparer.Ordinal).Skip(1).Any())
                {
                    names.TryAdd(source.Key, candidates[0]);
                }
            }
            fields.Add(field.Key, names);
        }
        return fields;
    }

    private static bool IsRussianLanguage(string language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language.Replace('_', '-')).TwoLetterISOLanguageName == "ru";
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private static Reading SelectReading(LocalizationManifest manifest, LocalizationRecord row, string referenceColumn, string? requiredEdition, string? referenceEdition, bool isOverride)
    {
        string? reference = row.References.GetValueOrDefault(referenceColumn);
        if (string.IsNullOrEmpty(reference))
        {
            return new(null, "reference-unavailable", $"The row has no usable value in '{referenceColumn}'. Add or review the corresponding native English text.");
        }
        if (requiredEdition is null || requiredEdition == referenceEdition)
        {
            return new(reference);
        }
        if (!isOverride)
        {
            bool? matches = LocalizationProjectIO.MatchesRecordedSourceText(row, reference);
            if (matches == true || LocalizationReferenceProvenance.MatchesSource(manifest, row, referenceColumn, reference))
            {
                return new(reference);
            }
            if (matches == false)
            {
                return new(null, "reference-edition-mismatch", $"The {referenceEdition ?? "unidentified-edition"} reading differs from the recorded {requiredEdition} source text. Use the {requiredEdition} native reading.");
            }
        }
        return new(null, "reference-edition-mismatch", $"A {requiredEdition} English reading is required for this column. The declared reading edition is {referenceEdition ?? "unidentified"}.");
    }

    private static string? TargetValidationError(LocalizationRecord row)
    {
        try
        {
            LocalizationTextValidation.Validate(row);
            return null;
        }
        catch (InvalidDataException exception)
        {
            return exception.Message;
        }
    }

    private sealed record ReviewFinding(string Rule, string Category, string File, string Key, string Column, string Message, bool Fixable, bool Fixed);
    private sealed record RuleSummary(string Rule, int Fixed, int PendingFixable, int Manual);
    private readonly record struct Reading(string? Text, string? Rule = null, string? Message = null);
    private sealed record SpeakerGlossary(Dictionary<string, string> NpcNames, Dictionary<string, string> PartyAliases);
    private sealed record SpeakerWitness(string File, string Key, string CanonicalKey, string Message, string Source, string Target);

    internal sealed class ReportDestination : IDisposable
    {
        private readonly string input;
        private readonly string destination;
        private readonly string staging;
        private readonly FileStream stream;

        public ReportDestination(string input, string destination)
        {
            this.input = input;
            this.destination = destination;
            staging = Path.Combine(Path.GetDirectoryName(destination)!, ".localization-report-" + Guid.NewGuid().ToString("N") + ".tmp");
            stream = new(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }

        public void Stage(object report)
        {
            JsonSerializer.Serialize(stream, report, ReportOptions);
            stream.Flush(flushToDisk: true);
        }

        public void Publish()
        {
            stream.Dispose();
            File.Move(staging, ValidateReportPath(input, destination), overwrite: true);
        }

        public void Dispose()
        {
            stream.Dispose();
            File.Delete(staging);
        }
    }
}
