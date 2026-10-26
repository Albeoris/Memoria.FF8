using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Localization;

internal static class RefineFieldAudit
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Converters = { new JsonStringEnumConverter() } };
    private static readonly string[] Languages = ["en", "fr", "de", "es", "it", "ja"];

    internal static FieldAuditReport Run(string inputDirectory, string outputDirectory, Action<string> progress, CancellationToken cancellation)
    {
        string input = Path.GetFullPath(inputDirectory);
        string output = Path.GetFullPath(outputDirectory);
        RemasteredFieldAudit.ValidateOutput(output, input);
        if (Directory.Exists(output) || File.Exists(output))
        {
            throw new IOException("The report destination must be new; existing reports are never replaced.");
        }
        if (File.Exists(Path.Combine(input, "INCOMPLETE")))
        {
            throw new InvalidDataException("The input audit has not finished; partial coverage cannot be refined.");
        }
        string sourceReport = Path.Combine(input, "report.json");
        byte[] sourceBytes = ReadJsonBytes(sourceReport);
        using JsonDocument source = JsonDocument.Parse(sourceBytes);
        JsonElement original = source.RootElement;
        if (original.GetProperty("SchemaVersion").GetInt32() != 1 || original.GetProperty("Edition").GetString() != "Remastered" || original.GetProperty("CanonicalLanguage").GetString() != "en")
        {
            throw new InvalidDataException("Cached refinement requires the English canonical field audit.");
        }
        PhysicalMember[] physicalMembers = JsonSerializer.Deserialize<PhysicalMember[]>(original.GetProperty("PhysicalMembers").GetRawText(), Json) ?? throw new InvalidDataException("Missing physical resource manifest.");
        Dictionary<string, PhysicalMember> manifest = new(StringComparer.Ordinal);
        HashSet<string> physicalIdentities = new(StringComparer.OrdinalIgnoreCase);
        foreach (PhysicalMember member in physicalMembers)
        {
            cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(member.Identity) || member.Length < 0 || member.Sha256 is null || !Regex.IsMatch(member.Sha256, "^[0-9A-F]{64}$", RegexOptions.CultureInvariant))
            {
                throw new InvalidDataException("Invalid physical resource manifest entry.");
            }
            if (!physicalIdentities.Add(member.Identity))
            {
                throw new InvalidDataException("Duplicate physical resource identity: " + member.Identity);
            }
            manifest.Add(member.Identity, member);
        }
        string fingerprint = Hash(Encoding.UTF8.GetBytes(string.Join('\n', physicalMembers.OrderBy(member => member.Identity, StringComparer.Ordinal).Select(member => $"{member.Identity}|{member.Sha256}"))));
        if (!fingerprint.Equals(original.GetProperty("CorpusFingerprint").GetString(), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Cached physical manifest does not match the source corpus fingerprint.");
        }
        OldPair[] oldPairs = JsonSerializer.Deserialize<OldPair[]>(original.GetProperty("Pairs").GetRawText(), Json) ?? throw new InvalidDataException("Missing comparison pairs.");
        if (oldPairs.Select(pair => (pair.Field, pair.Language)).Distinct().Count() != oldPairs.Length)
        {
            throw new InvalidDataException("Duplicate cached comparison pairs.");
        }
        Directory.CreateDirectory(output);
        string marker = Path.Combine(output, "INCOMPLETE");
        File.WriteAllText(marker, "Refinement has not finished. Partial output is not an audit result.");
        Stopwatch timer = Stopwatch.StartNew();
        Dictionary<(string Field, string Language), PreparedFieldCompatibility> prepared = [];
        List<NewPair> pairs = [];
        List<BundleMember> bundles = [];
        List<TechnicalMember> technical = [];
        HashSet<(string Field, string Language)> technicalSeen = [];
        HashSet<(string Field, int Ordinal)> englishRows = [];
        foreach (OldPair prior in oldPairs)
        {
            cancellation.ThrowIfCancellationRequested();
            ValidateIdentity(prior.Field, prior.Language);
            if (prior.Language == "en")
            {
                throw new InvalidDataException("Cached comparison pairs must use a localized language.");
            }
            PreparedFieldCompatibility english = Prepare(prior.Field, "en");
            PreparedFieldCompatibility local = Prepare(prior.Field, prior.Language);
            FieldCompatibilityResult result = FieldCompatibilityAudit.Compare(english, local);
            string diagnostic = $"pairs/{prior.Field}.{prior.Language}.json";
            Write(Path.Combine(output, diagnostic), new
            {
                result.Kind,
                SourceReport = sourceReport,
                EnglishBank = SourceBank(prior.Field, "en"),
                LocalizedBank = SourceBank(prior.Field, prior.Language),
                EnglishFieldName = Path.Combine(input, "banks", prior.Field, "en", "field-name.json"),
                LocalFieldName = Path.Combine(input, "banks", prior.Field, prior.Language, "field-name.json"),
                FirstDivergence = result.FirstDivergence is null ? null : new
                {
                    result.FirstDivergence.Reason,
                    result.FirstDivergence.RawByteOffset,
                    result.FirstDivergence.EnglishPosition,
                    result.FirstDivergence.LocalPosition,
                    result.FirstDivergence.StructuralBoundaryOperationIndex
                },
                EnglishSuffixContexts = CompactContexts(result.EnglishSuffixContexts, SourceBank(prior.Field, "en")),
                LocalSuffixContexts = CompactContexts(result.LocalSuffixContexts, SourceBank(prior.Field, prior.Language)),
                result.ProvenOrdinalMatches,
                result.PotentialLocalOrdinals,
                result.ImplicatedEnglishOrdinals,
                result.SemanticReviewCases,
                result.ContextOnlyProblems,
                result.CandidateCountKnown,
                result.Reasons,
                result.ReviewCases,
                EnglishValidation = Validation(result.English),
                LocalValidation = Validation(result.Localized),
                EnglishProblems = result.English.Problems,
                LocalProblems = result.Localized.Problems
            });
            NewPair next = new(prior.Field, prior.Language, result.Kind.ToString(), result.PotentialLocalOrdinals.Length, result.SemanticReviewCases, result.ContextOnlyProblems, result.CandidateCountKnown, diagnostic, prior.Kind, prior.PotentialRows, prior.ReviewCases);
            pairs.Add(next);
            foreach (int ordinal in result.ImplicatedEnglishOrdinals)
            {
                englishRows.Add((prior.Field, ordinal));
            }
            string sharedCases = JsonSerializer.Serialize(result.ReviewCases.Select(review => new { review.Kind, review.EnglishPositions, review.LocalPositions, review.EnglishOrdinals, review.LocalOrdinals }), Json);
            string[] problemSignature = result.English.Problems.Concat(result.Localized.Problems).Select(Normalize).Order(StringComparer.Ordinal).ToArray();
            string scriptPair = prior.Field + "|" + result.English.JsmSha256 + "|" + result.Localized.JsmSha256;
            string referenceScope = result.English.Problems.Concat(result.Localized.Problems).Any(IsReferenceSpecific) ? "|" + prior.Language : "";
            string issueKey = scriptPair + "|" + string.Join('\n', problemSignature) + "|" + sharedCases + referenceScope;
            bundles.Add(new(prior.Field, prior.Language, scriptPair, Hash(Encoding.UTF8.GetBytes(issueKey)), next.PotentialRows, next.ReviewCases, next.CountKnown, diagnostic, result.ReviewCases.Select(review => review.Id).ToArray(), result.ReviewCases.Select(review => review.Kind).Distinct().Order(StringComparer.Ordinal).ToArray(), prior.Kind == "Incomplete", !result.English.ScriptValidated || !result.Localized.ScriptValidated || !result.English.BankValidated || !result.Localized.BankValidated || !result.English.StaticReferencesValidated || !result.Localized.StaticReferencesValidated));
            if (pairs.Count % 100 == 0 || pairs.Count == oldPairs.Length)
            {
                progress($"Refined {pairs.Count}/{oldPairs.Length} pairs; {prepared.Count} verified cached banks");
            }
        }
        cancellation.ThrowIfCancellationRequested();
        var reviewGroups = bundles.GroupBy(member => member.IssueFingerprint).Select(group => new
        {
            Id = group.Key,
            Field = group.First().Field,
            ScriptPair = group.First().ScriptPair,
            TechnicalBlocked = group.Any(member => member.TechnicalBlocked),
            PairCount = group.Count(),
            PotentialRows = group.Sum(member => member.PotentialRows),
            ReviewCases = group.Sum(member => member.ReviewCases),
            UnknownCandidatePairs = group.Count(member => !member.CountKnown),
            CaseKinds = group.SelectMany(member => member.CaseKinds).Distinct().Order(StringComparer.Ordinal).ToArray(),
            Members = group.OrderBy(member => member.Language, StringComparer.Ordinal).ToArray()
        }).OrderByDescending(group => group.PotentialRows).ThenBy(group => group.Field, StringComparer.Ordinal).ToArray();
        var technicalGroups = technical.GroupBy(member => member.IssueFingerprint).Select(group => new
        {
            Id = group.Key,
            Category = "TechnicalRepair",
            Field = group.First().Field,
            ScriptSha256 = group.First().ScriptSha256,
            Issue = group.First().Issue,
            BankCount = group.Count(),
            Members = group.OrderBy(member => member.Language, StringComparer.Ordinal).ToArray()
        }).OrderByDescending(group => group.BankCount).ThenBy(group => group.Field, StringComparer.Ordinal).ToArray();
        Write(Path.Combine(output, "grouped-review.json"), new
        {
            SourceReport = sourceReport,
            Explanation = "Groups reuse exact mechanical issue evidence only. Member references remain language-specific. No canonical row counts or semantic meanings are deduplicated; technical repairs and semantic/context review are separate queues.",
            ScriptPairGroups = bundles.Select(member => member.ScriptPair).Distinct().Count(),
            ExactIssueBundles = reviewGroups.Length,
            OriginallyIncompleteScriptPairGroups = bundles.Where(member => member.OriginallyIncomplete).Select(member => member.ScriptPair).Distinct().Count(),
            OriginallyIncompleteIssueBundles = bundles.Where(member => member.OriginallyIncomplete).Select(member => member.IssueFingerprint).Distinct().Count(),
            OriginallyIncompleteMembers = bundles.Where(member => member.OriginallyIncomplete).ToArray(),
            TechnicalRepairGroups = technicalGroups,
            BlockedAlignmentGroups = reviewGroups.Where(group => group.TechnicalBlocked).ToArray(),
            AlignmentAndContextGroups = reviewGroups.Where(group => !group.TechnicalBlocked && group.ReviewCases > 0).ToArray()
        });
        var oldSubset = oldPairs.Where(pair => pair.Kind == "Incomplete").ToArray();
        var newSubset = pairs.Where(pair => pair.OriginalKind == "Incomplete").ToArray();
        string[] errors = original.TryGetProperty("Errors", out JsonElement originalErrors) ? originalErrors.EnumerateArray().Select(value => value.GetString() ?? "").ToArray() : [];
        bool complete = errors.Length == 0 && pairs.All(pair => pair.CountKnown && pair.Kind != "Incomplete");
        string summary = $"Cached refinement: {oldPairs.Sum(pair => pair.PotentialRows)} -> {pairs.Sum(pair => pair.PotentialRows)} candidate rows; {oldPairs.Sum(pair => pair.ReviewCases)} -> {pairs.Sum(pair => pair.ReviewCases)} review cases. Original {oldSubset.Length} incomplete pairs: {oldSubset.Sum(pair => pair.PotentialRows)} -> {newSubset.Sum(pair => pair.PotentialRows)} candidate rows; {oldSubset.Sum(pair => pair.ReviewCases)} -> {newSubset.Sum(pair => pair.ReviewCases)} review cases. Coverage {(complete ? "complete" : "INCOMPLETE")}. No semantic mappings approved.";
        const string limitations = "Cached original bytes are length/SHA256 verified and decoded again, without game archive access. Exact executable identity, valid bank coverage and static references can prove ordinal correspondence while runtime presentation/choice contexts remain incomplete. Original incomplete pairs are not finalized translations. Malformed headers, missing resources, decoder errors and unknown counts remain explicit; divergent suffixes are never resynchronized. Candidate counts sum language-specific rows; semantic meanings are not deduplicated. Source location graphs and field names are references to the original audit, not recomputed evidence.";
        Write(Path.Combine(output, "report.json"), new
        {
            SchemaVersion = 2,
            Edition = original.GetProperty("Edition").GetString(),
            CanonicalLanguage = "en",
            SourceReport = sourceReport,
            SourceReportSha256 = Hash(sourceBytes),
            CorpusFingerprint = original.GetProperty("CorpusFingerprint").GetString(),
            Parser = typeof(FieldCompatibilityAudit).Assembly.GetName().ToString(),
            ParserAssemblySha256 = Hash(File.ReadAllBytes(typeof(FieldCompatibilityAudit).Assembly.Location)),
            BaselineRows = original.GetProperty("BaselineRows").GetInt32(),
            PotentialRows = pairs.Sum(pair => pair.PotentialRows),
            EnglishRowsImplicated = englishRows.Count,
            ReviewCases = pairs.Sum(pair => pair.ReviewCases),
            Comparison = new
            {
                All = new { Before = Totals(oldPairs), After = Totals(pairs) },
                OriginallyIncomplete = new { Before = Totals(oldSubset), After = Totals(newSubset) }
            },
            PerLanguage = pairs.GroupBy(pair => pair.Language).Select(group => new { Language = group.Key, Compared = group.Count(), PotentialRows = group.Sum(pair => pair.PotentialRows), ReviewCases = group.Sum(pair => pair.ReviewCases), ContextProblems = group.Sum(pair => pair.ContextProblems), Incomplete = group.Count(pair => pair.Kind == "Incomplete"), UnknownCandidatePairs = group.Count(pair => !pair.CountKnown) }).ToArray(),
            Pairs = pairs,
            Errors = errors,
            LocationGraphsSource = sourceReport,
            GroupedReview = "grouped-review.json",
            ExactIssueBundles = reviewGroups.Length,
            OriginallyIncompleteScriptPairGroups = bundles.Where(member => member.OriginallyIncomplete).Select(member => member.ScriptPair).Distinct().Count(),
            OriginallyIncompleteIssueBundles = bundles.Where(member => member.OriginallyIncomplete).Select(member => member.IssueFingerprint).Distinct().Count(),
            OriginallyIncompleteMembers = bundles.Where(member => member.OriginallyIncomplete).ToArray(),
            TechnicalRepairGroups = technicalGroups.Length,
            Complete = complete,
            ElapsedSeconds = timer.Elapsed.TotalSeconds,
            Parallelism = 1,
            Limitations = limitations
        });
        StringBuilder human = new();
        human.AppendLine(summary).AppendLine().AppendLine(limitations).AppendLine();
        human.AppendLine($"Verified cached banks: {prepared.Count}. Script-pair groups: {bundles.Select(member => member.ScriptPair).Distinct().Count()}. Exact case/issue bundles: {reviewGroups.Length}. Technical repair groups: {technicalGroups.Length}.");
        human.AppendLine().AppendLine("| Scope | Pairs | Candidate rows before | Candidate rows after | Review cases before | Review cases after | Unknown counts after |");
        human.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        human.AppendLine($"| All | {pairs.Count} | {oldPairs.Sum(pair => pair.PotentialRows)} | {pairs.Sum(pair => pair.PotentialRows)} | {oldPairs.Sum(pair => pair.ReviewCases)} | {pairs.Sum(pair => pair.ReviewCases)} | {pairs.Count(pair => !pair.CountKnown)} |");
        human.AppendLine($"| Originally incomplete | {newSubset.Length} | {oldSubset.Sum(pair => pair.PotentialRows)} | {newSubset.Sum(pair => pair.PotentialRows)} | {oldSubset.Sum(pair => pair.ReviewCases)} | {newSubset.Sum(pair => pair.ReviewCases)} | {newSubset.Count(pair => !pair.CountKnown)} |");
        human.AppendLine().AppendLine($"Original audit: [{Path.GetFileName(input)}]({new Uri(sourceReport).AbsoluteUri}). Group members and prioritization: [grouped-review.json](grouped-review.json). Pair diagnostics reference original banks, graph-derived field names and resources without copying their contents.");
        File.WriteAllText(Path.Combine(output, "summary.md"), human.ToString(), new UTF8Encoding(false));
        cancellation.ThrowIfCancellationRequested();
        File.Delete(marker);
        return new(complete, summary);

        string SourceBank(string field, string language) => Path.Combine(input, "banks", field, language, "bank.json");

        PreparedFieldCompatibility Prepare(string field, string language)
        {
            if (prepared.TryGetValue((field, language), out PreparedFieldCompatibility? existing))
            {
                return existing;
            }
            cancellation.ThrowIfCancellationRequested();
            ValidateIdentity(field, language);
            using JsonDocument document = JsonDocument.Parse(ReadJsonBytes(SourceBank(field, language)));
            JsonElement bank = document.RootElement;
            if (bank.GetProperty("Field").GetString() != field || bank.GetProperty("Language").GetString() != language)
            {
                throw new InvalidDataException("Cached bank identity does not match its comparison.");
            }
            string directory = Path.Combine(input, "banks", field, language);
            byte[]? script = ReadVerified(directory, "original.jsm", bank.GetProperty("Script"), manifest);
            byte[]? text = ReadVerified(directory, "original.msd", bank.GetProperty("Text"), manifest);
            List<string> problems = bank.GetProperty("Errors").EnumerateArray().Select(value => value.GetString() ?? "").Where(value => !value.StartsWith("MSD ordinal ", StringComparison.Ordinal) && !value.StartsWith("MSD:", StringComparison.Ordinal)).ToList();
            FieldAuditDecodedBank? decoded = null;
            if (text is not null)
            {
                try
                {
                    decoded = FieldAuditDecoder.Decode(text, language, field);
                    problems.AddRange(decoded.Rows.Where(row => row.Error is not null).Select(row => $"MSD ordinal {row.Ordinal}: {row.Error}"));
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or InvalidOperationException or FormatException or IndexOutOfRangeException or OverflowException or NotSupportedException)
                {
                    problems.Add($"MSD: {exception.GetType().Name}: {exception.Message}");
                }
            }
            if (script is null && !problems.Contains("Missing JSM."))
            {
                problems.Add("Missing JSM.");
            }
            if (text is null && !problems.Contains("Missing MSD."))
            {
                problems.Add("Missing MSD.");
            }
            FieldCompatibilityText[]? rows = decoded is { Complete: true } ? decoded.Rows.Select(row => new FieldCompatibilityText(row.Ordinal, row.Text!, row.Tokens)).ToArray() : null;
            string identity = bank.GetProperty("Script").ValueKind == JsonValueKind.Object ? bank.GetProperty("Script").GetProperty("Identity").GetString()! : $"missing:field/{field}/{language}";
            PreparedFieldCompatibility value = FieldCompatibilityAudit.Prepare(new(field, language, identity, script, rows, problems, decoded?.Rows.Length));
            prepared.Add((field, language), value);
            if (technicalSeen.Add((field, language)))
            {
                foreach (string problem in value.Inventory.Problems.Where(problem => !problem.StartsWith("Unresolved ", StringComparison.Ordinal)).Distinct())
                {
                    bool specific = IsReferenceSpecific(problem);
                    string scope = specific ? language + "|" + (bank.GetProperty("Text").ValueKind == JsonValueKind.Object ? bank.GetProperty("Text").GetProperty("Sha256").GetString() : "missing") : "shared-script";
                    string key = field + "|" + value.Inventory.JsmSha256 + "|" + scope + "|" + Normalize(problem);
                    technical.Add(new(field, language, value.Inventory.JsmSha256, problem, Hash(Encoding.UTF8.GetBytes(key)), SourceBank(field, language)));
                }
            }
            return value;
        }
    }

    private static object[] CompactContexts(IEnumerable<FieldCompatibilityContext> contexts, string bankReference) => contexts.Select(context => (object)new
    {
        context.Position,
        context.Opcode,
        context.Operands,
        context.MessageId,
        context.Dynamic,
        context.PresentationStateFingerprint,
        CandidateRange = context.CandidateOrdinals.IsEmpty ? null : new { First = context.CandidateOrdinals[0], Count = context.CandidateOrdinals.Length },
        BankOrdinal = context.Row?.Ordinal,
        BankReference = bankReference
    }).ToArray();

    private static byte[] ReadJsonBytes(string path)
    {
        const int maximumBytes = 64 * 1024 * 1024;
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > maximumBytes)
        {
            throw new InvalidDataException("Cached JSON exceeds the 64 MiB input limit: " + path);
        }
        using MemoryStream bytes = new();
        byte[] buffer = new byte[64 * 1024];
        int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            if (bytes.Length + count > maximumBytes)
            {
                throw new InvalidDataException("Cached JSON exceeds the 64 MiB input limit: " + path);
            }
            bytes.Write(buffer, 0, count);
        }
        return bytes.ToArray();
    }

    private static byte[]? ReadVerified(string directory, string name, JsonElement metadata, IReadOnlyDictionary<string, PhysicalMember> manifest)
    {
        string path = Path.Combine(directory, name);
        if (metadata.ValueKind == JsonValueKind.Null)
        {
            if (File.Exists(path))
            {
                throw new InvalidDataException("Unexpected cached resource without metadata: " + path);
            }
            return null;
        }
        using FileStream stream = File.OpenRead(path);
        string identity = metadata.GetProperty("Identity").GetString() ?? throw new InvalidDataException("Cached resource metadata lacks an identity.");
        if (!manifest.TryGetValue(identity, out PhysicalMember? expected) || expected.Length != metadata.GetProperty("Length").GetInt32() || !expected.Sha256.Equals(metadata.GetProperty("Sha256").GetString(), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Cached resource metadata does not match the source physical manifest: " + path);
        }
        byte[] bytes = RemasteredFieldAudit.ReadBounded(stream);
        if (bytes.Length != metadata.GetProperty("Length").GetInt32() || !Hash(bytes).Equals(metadata.GetProperty("Sha256").GetString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Cached resource length or SHA256 mismatch: " + path);
        }
        return bytes;
    }

    private static void ValidateIdentity(string field, string language)
    {
        string stem = field.Split('.')[0];
        if (field.Length == 0 || field.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')) || Regex.IsMatch(stem, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || !Languages.Contains(language))
        {
            throw new InvalidDataException("Unsupported cached field/language identity.");
        }
    }

    private static bool IsReferenceSpecific(string problem) => !problem.StartsWith("Unresolved ", StringComparison.Ordinal) && (problem.Contains("MSD", StringComparison.Ordinal) || problem.Contains("text", StringComparison.OrdinalIgnoreCase) || problem.Contains("bank", StringComparison.OrdinalIgnoreCase));
    private static string Normalize(string problem) => Regex.Replace(problem, @"candidate bound is \d+ bank rows", "candidate bound is indexed bank rows", RegexOptions.CultureInvariant);
    private static object Validation(FieldCompatibilityInventory inventory) => new { inventory.ScriptValidated, inventory.BankValidated, inventory.StaticReferencesValidated, inventory.BankCountKnown, inventory.DeclaredTextCount };
    private static object Totals(IEnumerable<OldPair> source) => new { Pairs = source.Count(), PotentialRows = source.Sum(pair => pair.PotentialRows), ReviewCases = source.Sum(pair => pair.ReviewCases), UnknownCandidatePairs = source.Count(pair => !pair.CountKnown), Incomplete = source.Count(pair => pair.Kind == "Incomplete") };
    private static object Totals(IEnumerable<NewPair> source) => new { Pairs = source.Count(), PotentialRows = source.Sum(pair => pair.PotentialRows), ReviewCases = source.Sum(pair => pair.ReviewCases), UnknownCandidatePairs = source.Count(pair => !pair.CountKnown), Incomplete = source.Count(pair => pair.Kind == "Incomplete") };
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void Write(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream stream = File.Create(path);
        JsonSerializer.Serialize(stream, value, Json);
    }

    private sealed record PhysicalMember(string Identity, int Length, string Sha256);
    private sealed record OldPair(string Field, string Language, string Kind, int PotentialRows, int ReviewCases, int ContextProblems, bool CountKnown, string Diagnostic);
    private sealed record NewPair(string Field, string Language, string Kind, int PotentialRows, int ReviewCases, int ContextProblems, bool CountKnown, string Diagnostic, string OriginalKind, int OriginalPotentialRows, int OriginalReviewCases);
    private sealed record BundleMember(string Field, string Language, string ScriptPair, string IssueFingerprint, int PotentialRows, int ReviewCases, bool CountKnown, string Diagnostic, string[] CaseIds, string[] CaseKinds, bool OriginallyIncomplete, bool TechnicalBlocked);
    private sealed record TechnicalMember(string Field, string Language, string? ScriptSha256, string Issue, string IssueFingerprint, string BankReference);
}
