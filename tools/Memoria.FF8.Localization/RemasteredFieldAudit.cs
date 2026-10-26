using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Assets.Remastered;

namespace Memoria.FF8.Localization;

internal sealed record FieldAuditReport(bool Complete, string Summary);

internal static class RemasteredFieldAudit
{
    private static readonly string[] Languages = ["en", "fr", "de", "es", "it", "ja"];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Converters = { new JsonStringEnumConverter() } };
    private const int MaximumMemberBytes = 128 * 1024 * 1024;

    public static FieldAuditReport Run(string gameDirectory, string outputDirectory, Action<string> progress, CancellationToken cancellation)
    {
        string output = Path.GetFullPath(outputDirectory);
        ValidateOutput(output, Path.GetFullPath(gameDirectory));
        if (Directory.Exists(output) || File.Exists(output))
        {
            throw new IOException("The report destination must be new; existing reports are never replaced.");
        }
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "INCOMPLETE"), "Audit has not finished. Do not treat partial files as complete coverage.");
        Stopwatch timer = Stopwatch.StartNew();
        using ZzzArchive main = new(Path.Combine(gameDirectory, "main.zzz"));
        using ZzzArchive other = new(Path.Combine(gameDirectory, "other.zzz"));
        List<string> errors = [];
        List<Member> members = [];
        List<object> containers = [];
        Dictionary<string, string[]> mapLists = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> discoveredFields = new(StringComparer.OrdinalIgnoreCase);
        foreach (string stem in new[] { "data/field" }.Concat(Languages.Select(language => $"data/lang-{Suffix(language)}/field")))
        {
            cancellation.ThrowIfCancellationRequested();
            if (Find(stem + ".fl") is null)
            {
                continue;
            }
            progress($"Inventory {stem}");
            byte[] listing = ReadRoot(stem + ".fl");
            byte[] index = ReadRoot(stem + ".fi");
            containers.Add(new { Stem = stem, ListingSha256 = Hash(listing), IndexSha256 = Hash(index), Content = Find(stem + ".fs"), Source = main.Find(stem + ".fl") is null ? "other.zzz" : "main.zzz" });
            using IFlArchive outer = FlArchive.Open(new MemoryStream(listing, false), new MemoryStream(index, false), OpenRoot(stem + ".fs"), false, FlArchiveRepresentation.Files);
            Dictionary<string, IFlArchiveEntry> entries = Index(outer);
            foreach (IFlArchiveEntry nestedListing in outer.Entries.Where(entry => entry.RelativePath.EndsWith(".fl", StringComparison.OrdinalIgnoreCase)).OrderBy(entry => entry.RelativePath, StringComparer.Ordinal))
            {
                cancellation.ThrowIfCancellationRequested();
                string field = Path.GetFileNameWithoutExtension(Basename(nestedListing.RelativePath));
                ValidateFieldName(field);
                if (field == "main_chr")
                {
                    continue;
                }
                try
                {
                    using IFlArchive nested = FlArchive.Open(new MemoryStream(Read(nestedListing), false), new MemoryStream(Read(Require(entries, field + ".fi")), false), new MemoryStream(Read(Require(entries, field + ".fs")), false), false, FlArchiveRepresentation.Files);
                    if (field == "mapdata")
                    {
                        IFlArchiveEntry maplist = Require(Index(nested), "maplist");
                        mapLists.Add(stem, Encoding.ASCII.GetString(Read(maplist)).Replace("\r", "").Split('\n'));
                        continue;
                    }
                    var textMembers = nested.Entries.Where(entry => Path.GetExtension(entry.RelativePath).ToLowerInvariant() is ".jsm" or ".msd").ToArray();
                    if (textMembers.Length == 0)
                    {
                        errors.Add($"No JSM/MSD members in nested field archive {stem}/{field}.");
                        discoveredFields.Add(field);
                    }
                    foreach (IFlArchiveEntry entry in textMembers)
                    {
                        string basename = Basename(entry.RelativePath);
                        string name = Path.GetFileNameWithoutExtension(basename);
                        string? language = Languages.FirstOrDefault(language => name.EndsWith("_" + Suffix(language), StringComparison.OrdinalIgnoreCase));
                        if (language is null)
                        {
                            errors.Add($"Unrecognized language suffix: {stem}/{field}/{entry.RelativePath}.");
                            continue;
                        }
                        string logicalField = name[..^(Suffix(language).Length + 1)];
                        ValidateFieldName(logicalField);
                        if (!logicalField.Equals(field, StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"Unexpected field identity {logicalField} inside {stem}/{field}; member retained separately.");
                        }
                        discoveredFields.Add(logicalField);
                        members.Add(new(logicalField, language, stem, Path.GetExtension(basename).ToLowerInvariant(), $"{stem}/{field}/{entry.RelativePath.Replace('\\', '/')}", Read(entry)));
                    }
                }
                catch (Exception exception) when (IsResourceError(exception))
                {
                    discoveredFields.Add(field);
                    errors.Add($"{stem}/{field}: {exception.GetType().Name}: {exception.Message}");
                }
            }
        }
        if (members.Count == 0)
        {
            throw new InvalidDataException("No localized field JSM/MSD resources found in the selected Remastered installation.");
        }
        string[] fields = discoveredFields.Order(StringComparer.Ordinal).ToArray();
        Dictionary<(string Field, string Language), Dataset> datasets = [];
        foreach (string field in fields)
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (string language in Languages)
            {
                string localizedStem = $"data/lang-{Suffix(language)}/field";
                string effectiveStem = Find(localizedStem + ".fl") is null ? "data/field" : localizedStem;
                Member[] scripts = members.Where(member => member.Stem == effectiveStem && member.Field.Equals(field, StringComparison.OrdinalIgnoreCase) && member.Language == language && member.Extension == ".jsm").ToArray();
                Member[] banks = members.Where(member => member.Stem == effectiveStem && member.Field.Equals(field, StringComparison.OrdinalIgnoreCase) && member.Language == language && member.Extension == ".msd").ToArray();
                List<string> problems = [];
                Member? script = Select(scripts, "JSM", problems);
                Member? bank = Select(banks, "MSD", problems);
                FieldAuditDecodedBank? decoded = null;
                if (bank is not null)
                {
                    try
                    {
                        decoded = FieldAuditDecoder.Decode(bank.Bytes, language, field);
                        problems.AddRange(decoded.Rows.Where(row => row.Error is not null).Select(row => $"MSD ordinal {row.Ordinal}: {row.Error}"));
                    }
                    catch (Exception exception) when (IsResourceError(exception))
                    {
                        problems.Add($"MSD: {exception.GetType().Name}: {exception.Message}");
                    }
                }
                string directory = Path.Combine(output, "banks", field, language);
                Directory.CreateDirectory(directory);
                if (script is not null)
                {
                    File.WriteAllBytes(Path.Combine(directory, "original.jsm"), script.Bytes);
                }
                if (bank is not null)
                {
                    File.WriteAllBytes(Path.Combine(directory, "original.msd"), bank.Bytes);
                }
                Write(Path.Combine(directory, "bank.json"), new { Field = field, Language = language, Script = Metadata(script), Text = Metadata(bank), Decoded = decoded, Errors = problems });
                FieldCompatibilityText[]? rows = decoded is { Complete: true } ? decoded.Rows.Select(row => new FieldCompatibilityText(row.Ordinal, row.Text!, row.Tokens)).ToArray() : null;
                FieldCompatibilityInput input = new(field, language, script?.Identity ?? bank?.Identity ?? $"missing:field/{field}/{language}", script?.Bytes, rows, problems, decoded?.Rows.Length);
                PreparedFieldCompatibility prepared = FieldCompatibilityAudit.Prepare(input);
                datasets.Add((field, language), new(input, decoded, script is not null && bank is not null, prepared));
                FieldCompatibilityInventory inventory = prepared.Inventory;
                Write(Path.Combine(directory, "inventory.json"), new
                {
                    inventory.Field, inventory.Language, inventory.ResourceIdentity, inventory.JsmSha256, inventory.JsmLength,
                    inventory.ParserVersion, inventory.BankCountKnown, inventory.DeclaredTextCount,
                    BankReference = "bank.json", inventory.Instructions, Contexts = CompactContexts(inventory.Contexts),
                    inventory.ReferencedOrdinals, inventory.UnreferencedOrdinals, inventory.EmptyOrdinals, inventory.Problems, inventory.ExecutableLayout
                });
            }
        }
        List<Pair> pairs = [];
        List<object> locationGraphs = [];
        List<string> locationProblems = [];
        string graphCache = Path.Combine(Path.GetDirectoryName(output)!, "localization-graph-cache");
        ValidateOutput(graphCache, Path.GetFullPath(gameDirectory));
        foreach (string language in Languages)
        {
            cancellation.ThrowIfCancellationRequested();
            string localizedStem = $"data/lang-{Suffix(language)}/field";
            string effectiveStem = Find(localizedStem + ".fl") is null ? "data/field" : localizedStem;
            if (!mapLists.TryGetValue(effectiveStem, out string[]? maplist))
            {
                locationProblems.Add($"{language}: missing canonical field/mapdata/maplist; no numeric field identities invented.");
                continue;
            }
            FieldLocationSource[] graphSources = maplist.Select((field, id) => new FieldLocationSource(id, field, datasets.GetValueOrDefault((field, language))?.Input.Jsm)).Where(field => !string.IsNullOrWhiteSpace(field.Field)).ToArray();
            FieldLocationGraphResult graph = FieldLocationAudit.Run(gameDirectory, language, graphSources, graphCache, locationProblems, progress);
            Write(Path.Combine(output, "graphs", language + ".json"), graph);
            locationGraphs.Add(new { Language = language, graph.Fingerprint, Nodes = graph.Nodes.Length, Resolved = graph.Resolutions.Count(resolution => resolution.Names.Length == 1), Ambiguous = graph.Resolutions.Count(resolution => resolution.Status == FieldLocationStatus.Ambiguous), NoPath = graph.Resolutions.Count(resolution => resolution.Status == FieldLocationStatus.NoPath), Incomplete = graph.Resolutions.Count(resolution => resolution.Incomplete) });
            foreach (string field in fields)
            {
                FieldLocationResolution[] resolutions = graph.Resolutions.Where(resolution => resolution.Field.Equals(field, StringComparison.OrdinalIgnoreCase)).ToArray();
                string[] names = resolutions.SelectMany(resolution => resolution.Names).Distinct(StringComparer.Ordinal).ToArray();
                Write(Path.Combine(output, "banks", field, language, "field-name.json"), new Dictionary<string, object?>
                {
                    ["Field"] = field,
                    ["Field Name"] = names.Length == 1 ? names[0] : null,
                    ["Field Name Candidates"] = names,
                    ["Resolutions"] = resolutions,
                    ["Graph"] = $"graphs/{language}.json"
                });
            }
        }
        HashSet<(string Field, int Ordinal)> englishCandidates = [];
        foreach (string field in fields)
        {
            cancellation.ThrowIfCancellationRequested();
            Dataset english = datasets[(field, "en")];
            foreach (string language in Languages.Where(language => language != "en"))
            {
                Dataset localized = datasets[(field, language)];
                FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(english.Prepared, localized.Prepared);
                string diagnostic = $"pairs/{field}.{language}.json";
                Write(Path.Combine(output, diagnostic), new
                {
                    result.Kind,
                    EnglishBank = $"banks/{field}/en/bank.json",
                    LocalizedBank = $"banks/{field}/{language}/bank.json",
                    result.FirstDivergence,
                    EnglishInventory = $"banks/{field}/en/inventory.json",
                    LocalInventory = $"banks/{field}/{language}/inventory.json",
                    EnglishSuffixContexts = CompactContexts(result.EnglishSuffixContexts),
                    LocalSuffixContexts = CompactContexts(result.LocalSuffixContexts),
                    result.MessageIdentityEvidence,
                    result.ProvenOrdinalMatches,
                    result.PotentialLocalOrdinals,
                    result.ImplicatedEnglishOrdinals,
                    result.SemanticReviewCases,
                    result.ContextOnlyProblems,
                    result.CandidateCountKnown,
                    result.Reasons,
                    result.ReviewCases,
                    EnglishProblems = result.English.Problems,
                    LocalProblems = result.Localized.Problems
                });
                foreach (int ordinal in result.ImplicatedEnglishOrdinals)
                {
                    englishCandidates.Add((field, ordinal));
                }
                pairs.Add(new(field, language, result.Kind.ToString(), result.PotentialLocalOrdinals.Length, result.SemanticReviewCases, result.ContextOnlyProblems, result.CandidateCountKnown, diagnostic));
            }
            progress($"Compared {pairs.Count}/{fields.Length * 5} pairs ({field})");
        }
        var perLanguage = Languages.Select(language => new
        {
            Language = language,
            Expected = fields.Length,
            Found = datasets.Count(pair => pair.Key.Language == language && pair.Value.Found),
            Decoded = datasets.Count(pair => pair.Key.Language == language && pair.Value.Decoded is { Complete: true }),
            Compared = pairs.Count(pair => pair.Language == language),
            Compatible = pairs.Count(pair => pair.Language == language && pair.Kind is "ByteIdentical" or "StorageOnly"),
            Divergent = pairs.Count(pair => pair.Language == language && pair.Kind == "Divergent"),
            Incomplete = pairs.Count(pair => pair.Language == language && pair.Kind == "Incomplete"),
            PotentialRows = pairs.Where(pair => pair.Language == language).Sum(pair => pair.PotentialRows),
            ReviewCases = pairs.Where(pair => pair.Language == language).Sum(pair => pair.ReviewCases),
            ContextProblems = pairs.Where(pair => pair.Language == language).Sum(pair => pair.ContextProblems),
            UnknownCandidatePairs = pairs.Count(pair => pair.Language == language && !pair.CountKnown)
        }).ToArray();
        int baselineRows = datasets.Where(pair => pair.Key.Language == "en").Sum(pair => pair.Value.Decoded?.Rows.Length ?? 0);
        int potentialRows = pairs.Sum(pair => pair.PotentialRows);
        int cases = pairs.Sum(pair => pair.ReviewCases);
        bool complete = errors.Count == 0 && perLanguage.All(row => row.Found == row.Expected && row.Decoded == row.Expected && row.UnknownCandidatePairs == 0 && row.Incomplete == 0);
        string fingerprint = Hash(Encoding.UTF8.GetBytes(string.Join('\n', members.OrderBy(member => member.Identity, StringComparer.Ordinal).Select(member => $"{member.Identity}|{Hash(member.Bytes)}"))));
        string summary = $"Automatic audit: {potentialRows} potentially differing localized rows, {englishCandidates.Count} distinct English rows implicated, {cases} review cases. Coverage {(complete ? "complete" : "INCOMPLETE; counts are partial/conservative")}; {fields.Length} fields, {pairs.Count} comparison pairs; elapsed {timer.Elapsed.TotalSeconds:F2}s. No semantic mappings approved.";
        string limitations = "Field resources only. Sequential archive reads and decoding (parallelism=1); each nested archive opened once. No semantic comparison or exception mappings. JSM byte equality is insufficient when operands, references or decoding are unresolved. Presentation metadata is reported from original operations; effective WINSIZE/control-flow and cross-language option meaning require review. Parser/decoder errors retain conservative candidate sets; unknown counts are explicit. No gameplay/font/runtime compatibility is established by this audit.";
        Write(Path.Combine(output, "report.json"), new
        {
            SchemaVersion = 1,
            Edition = "Remastered",
            CanonicalLanguage = "en",
            Provider = typeof(RemasteredGameDataSource).Assembly.GetName().ToString(),
            Parser = typeof(FieldCompatibilityAudit).Assembly.GetName().ToString(),
            ParserAssemblySha256 = Hash(File.ReadAllBytes(typeof(FieldCompatibilityAudit).Assembly.Location)),
            JsmAssemblySha256 = Hash(File.ReadAllBytes(typeof(Albeoris.Games.FF8.Jsm.JsmDocument).Assembly.Location)),
            CorpusFingerprint = fingerprint,
            Containers = containers,
            PhysicalMembers = members.Select(member => Metadata(member)).ToArray(),
            BaselineRows = baselineRows,
            PotentialRows = potentialRows,
            EnglishRowsImplicated = englishCandidates.Count,
            ReviewCases = cases,
            PerLanguage = perLanguage,
            Pairs = pairs,
            Errors = errors,
            LocationGraphs = locationGraphs,
            LocationProblems = locationProblems,
            Complete = complete,
            ElapsedSeconds = timer.Elapsed.TotalSeconds,
            Parallelism = 1,
            Limitations = limitations
        });
        StringBuilder human = new();
        human.AppendLine(summary).AppendLine().AppendLine(limitations).AppendLine();
        human.AppendLine("| Language | Expected | Found | Decoded | Compatible | Divergent | Incomplete | Potential rows | Review cases | Unknown counts |");
        human.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var row in perLanguage)
        {
            human.AppendLine($"| {row.Language} | {row.Expected} | {row.Found} | {row.Decoded} | {row.Compatible} | {row.Divergent} | {row.Incomplete} | {row.PotentialRows} | {row.ReviewCases} | {row.UnknownCandidatePairs} |");
        }
        human.AppendLine().AppendLine($"English baseline rows: {baselineRows}. Corpus fingerprint: {fingerprint}.");
        human.AppendLine("Full indexed banks and original bytes: banks/<field>/<language>/. Separate comparison diagnostics: pairs/<field>.<language>.json. No translation project has been created.");
        foreach (string error in errors)
        {
            human.AppendLine("- " + error);
        }
        File.WriteAllText(Path.Combine(output, "summary.md"), human.ToString(), new UTF8Encoding(false));
        File.Delete(Path.Combine(output, "INCOMPLETE"));
        return new(complete, summary);

        ZzzArchiveEntry? Find(string path)
        {
            ZzzArchiveEntry? first = main.Find(path);
            ZzzArchiveEntry? second = other.Find(path);
            if (first is not null && second is not null)
            {
                throw new InvalidDataException("Ambiguous mounted resource: " + path);
            }
            return first ?? second;
        }
        Stream OpenRoot(string path)
        {
            _ = Find(path);
            return main.Find(path) is { } entry ? main.Open(entry) : other.Open(path);
        }
        byte[] ReadRoot(string path)
        {
            using Stream stream = OpenRoot(path);
            return ReadBounded(stream);
        }
    }

    private static Member? Select(Member[] members, string kind, List<string> problems)
    {
        if (members.Length == 1)
        {
            return members[0];
        }
        problems.Add(members.Length == 0 ? $"Missing {kind}." : $"Ambiguous {kind}: {string.Join(", ", members.Select(member => member.Identity))}.");
        return null;
    }

    private static object[] CompactContexts(IEnumerable<FieldCompatibilityContext> contexts) => contexts.Select(context => (object)new
    {
        context.Position, context.Opcode, context.Operands, context.MessageId, context.IsMessage, context.Dynamic,
        // Dynamic references are bounded by the complete indexed bank, never by an observed runtime value.
        CandidateRange = context.CandidateOrdinals.IsEmpty ? null : new { First = context.CandidateOrdinals[0], Count = context.CandidateOrdinals.Length },
        BankOrdinal = context.Row?.Ordinal
    }).ToArray();

    internal static void ValidateOutput(string output, string gameDirectory)
    {
        string gameRoot = gameDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (output.Equals(gameDirectory, StringComparison.OrdinalIgnoreCase) || output.StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The audit output must be outside the selected source directory.");
        }
        for (DirectoryInfo? directory = new(output); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".git")) || Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                throw new ArgumentException("Audit reports contain original game assets and must be written outside every Git checkout.");
            }
        }
    }

    private static Dictionary<string, IFlArchiveEntry> Index(IFlArchive archive) => archive.Entries.ToDictionary(entry => Basename(entry.RelativePath), StringComparer.OrdinalIgnoreCase);
    private static string Basename(string path) => path.Replace('\\', '/').Split('/')[^1];
    private static string Suffix(string language) => language == "ja" ? "jp" : language;
    private static IFlArchiveEntry Require(Dictionary<string, IFlArchiveEntry> entries, string name) => entries.GetValueOrDefault(name) ?? throw new InvalidDataException($"Missing archive member {name}.");
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static object? Metadata(Member? member) => member is null ? null : new { member.Identity, Length = member.Bytes.Length, Sha256 = Hash(member.Bytes) };
    private static bool IsResourceError(Exception exception) => exception is IOException or InvalidDataException or ArgumentException or InvalidOperationException or FormatException or IndexOutOfRangeException or OverflowException or NotSupportedException;

    private static byte[] Read(IFlArchiveEntry entry)
    {
        if (entry.Size > MaximumMemberBytes)
        {
            throw new InvalidDataException($"Archive member {entry.RelativePath} exceeds the audit limit.");
        }
        using Stream stream = entry.OpenForRead();
        return ReadBounded(stream);
    }

    internal static byte[] ReadBounded(Stream stream)
    {
        using MemoryStream result = new();
        byte[] buffer = new byte[64 * 1024];
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            if (result.Length + read > MaximumMemberBytes)
            {
                throw new InvalidDataException("Resource exceeds the audit input limit.");
            }
            result.Write(buffer, 0, read);
        }
        return result.ToArray();
    }

    private static void Write(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream stream = File.Create(path);
        JsonSerializer.Serialize(stream, value, Json);
    }

    private static void ValidateFieldName(string field)
    {
        string[] reserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
        if (field.Length == 0 || field.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')) || reserved.Contains(field, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Unsupported logical field name: " + field);
        }
    }

    private sealed record Member(string Field, string Language, string Stem, string Extension, string Identity, byte[] Bytes);
    private sealed record Dataset(FieldCompatibilityInput Input, FieldAuditDecodedBank? Decoded, bool Found, PreparedFieldCompatibility Prepared);
    private sealed record Pair(string Field, string Language, string Kind, int PotentialRows, int ReviewCases, int ContextProblems, bool CountKnown, string Diagnostic);
}
