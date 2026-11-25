using System.Text.Json;
using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Mods.Localization;

public sealed record LocalizationReferenceSkip(string Category, string File, string Key, string Reason, IReadOnlyList<string> CandidateKeys);
public sealed record LocalizationReferenceCoverage(string Category, int Records, int Matched, int TextOnly, IReadOnlyDictionary<string, int> SkippedByReason);
public sealed record LocalizationReferenceEnrichment(LocalizationProject Project, string Language, GameDataEdition Edition, int Matched, int TextOnly, int Changed, IReadOnlyList<LocalizationReferenceCoverage> Coverage, IReadOnlyList<LocalizationReferenceSkip> Skips);

public static partial class LocalizationProjectBuilder
{
    /// <summary>Adds native reading columns to existing canonical rows without rebuilding their runtime bindings.</summary>
    public static LocalizationReferenceEnrichment EnrichReference(LocalizationProject project, string language, GameDataEdition edition, LocalizationSourceCatalog catalog, LocalizationSourceCatalog? companion = null, CancellationToken cancellation = default)
    {
        language = FieldLocalizationKey.CanonicalLanguage(language);
        if (project.Manifest.SchemaVersion != 2 || project.Manifest.Format != "json" || project.Manifest.MappingVersion != FieldCorrespondenceCatalog.Version)
        {
            throw new InvalidDataException("Reference enrichment requires a schema 2 JSON project using the current correspondence mapping.");
        }
        if (catalog.Entries.Count == 0 || catalog.Entries.Any(entry => entry.Language != language))
        {
            throw new InvalidDataException($"No usable native {language} catalog was loaded from {edition}.");
        }

        string label = "Reference [" + language + "]";
        Dictionary<string, string> languages = new(project.Manifest.ReferenceLanguages, StringComparer.Ordinal);
        if (languages.TryGetValue(label, out string? currentLanguage) && FieldLocalizationKey.CanonicalLanguage(currentLanguage) != language)
        {
            throw new InvalidDataException($"The existing {label} column identifies a different dataset.");
        }
        languages[label] = language;
        Dictionary<string, JsonElement> manifestData = new(project.Manifest.ExtensionData, StringComparer.Ordinal);
        Dictionary<string, JsonElement> editions = [];
        if (manifestData.TryGetValue(LocalizationProjectIO.ReferenceEditionsMember, out JsonElement previousEditions))
        {
            if (previousEditions.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"The manifest's {LocalizationProjectIO.ReferenceEditionsMember} member must be an object.");
            }
            editions = previousEditions.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        }
        editions[label] = JsonSerializer.SerializeToElement(edition.ToString());
        manifestData[LocalizationProjectIO.ReferenceEditionsMember] = JsonSerializer.SerializeToElement(editions);

        string baseLanguage = FieldLocalizationKey.CanonicalLanguage(project.Manifest.FieldScriptBaseLanguage);
        GameDataEdition? origin = ReferenceOrigin(project.Manifest, baseLanguage);
        LocalizationSourceCatalog displayCatalog = language == "en" ? catalog with { Entries = catalog.Entries.Concat(SystemTextCatalog.SourceEntries()).ToArray() } : catalog;
        ReferenceDatasetIndex reference = new(displayCatalog);
        ReferenceDatasetIndex? bridge = companion is null ? null : new(companion);
        Dictionary<string, string[]> nativeBanks = ReferenceBanks(displayCatalog.Entries);
        Dictionary<string, string[]> bridgeBanks = ReferenceBanks(companion?.Entries ?? []);
        Dictionary<string, string[]> projectBanks = project.Entries.Where(row => row.Category == "Fields")
            .GroupBy(row => row.File, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(row => ReferenceMetadata(row, "Source MSD SHA256")).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.Ordinal);
        List<LocalizationRecord> entries = new(project.Entries.Count);
        List<LocalizationReferenceSkip> skips = [];
        Dictionary<string, string[]> provenReadings = new(StringComparer.Ordinal);
        Dictionary<string, int> textOnly = new(StringComparer.Ordinal);
        int matched = 0;
        int changed = 0;

        foreach (LocalizationRecord row in project.Entries)
        {
            cancellation.ThrowIfCancellationRequested();
            ReferenceMatch match = row.Category switch
            {
                "Fields" => FieldReference(row, baseLanguage, language, origin, edition, reference, bridge, projectBanks, nativeBanks, bridgeBanks),
                "System" => SystemReference(row, language, reference),
                _ => NonFieldReference(row, baseLanguage, language, origin, edition, reference, bridge)
            };
            string? text = null;
            string? reason = match.Reason;
            if (reason is null)
            {
                if (match.Entries.Any(entry => !IsPresent(entry)))
                {
                    reason = "native-text-undecodable";
                }
                else if (match.Entries.All(entry => entry.Metadata.GetValueOrDefault("Native Null Pointer") == "true"))
                {
                    reason = "native-text-absent";
                }
                else
                {
                    try
                    {
                        text = string.Join("{Line}", match.Entries.Select(reference.DisplayText));
                        if (match.TextOnly && FieldMessageContext.TryParseTag(0, text, out _, out int prefixLength))
                        {
                            text = text[prefixLength..];
                        }
                    }
                    catch (InvalidDataException)
                    {
                        reason = "native-dictionary-unavailable";
                    }
                }
            }
            if (reason is null)
            {
                matched++;
                if (text is not null && language == baseLanguage && match.Entries.Count == 1 && text != match.Entries[0].Text &&
                    LocalizationReferenceProvenance.CreateBinding(row, match.Entries[0].Text, text) is { } sourceBinding)
                {
                    provenReadings.Add(row.Key, sourceBinding);
                }
                if (match.TextOnly)
                {
                    textOnly[row.Category] = textOnly.GetValueOrDefault(row.Category) + 1;
                }
            }
            else
            {
                skips.Add(new(row.Category, row.File, row.Key, reason, match.Keys));
            }

            bool present = row.ReferencePresence.TryGetValue(label, out bool existingPresence) ? existingPresence : row.References.ContainsKey(label);
            if (present == (reason is null) && row.References.GetValueOrDefault(label) == text && (row.ReferenceKeys.GetValueOrDefault(label) ?? []).SequenceEqual(match.Keys, StringComparer.Ordinal))
            {
                entries.Add(row);
                continue;
            }
            Dictionary<string, string?> values = new(row.References, StringComparer.Ordinal);
            Dictionary<string, bool> presence = new(row.ReferencePresence, StringComparer.Ordinal);
            if (reason is null)
            {
                values[label] = text;
                presence[label] = true;
            }
            else
            {
                values.Remove(label);
                presence.Remove(label);
            }
            Dictionary<string, IReadOnlyList<string>> keys = new(row.ReferenceKeys, StringComparer.Ordinal) { [label] = match.Keys };
            entries.Add(row with { References = values, ReferencePresence = presence, ReferenceKeys = keys });
            changed++;
        }

        LocalizationReferenceCoverage[] coverage = entries.GroupBy(row => row.Category, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal).Select(group =>
        {
            Dictionary<string, int> reasons = skips.Where(skip => skip.Category == group.Key).GroupBy(skip => skip.Reason, StringComparer.Ordinal).OrderBy(reason => reason.Key, StringComparer.Ordinal).ToDictionary(reason => reason.Key, reason => reason.Count(), StringComparer.Ordinal);
            return new LocalizationReferenceCoverage(group.Key, group.Count(), group.Count() - reasons.Values.Sum(), textOnly.GetValueOrDefault(group.Key), reasons);
        }).ToArray();
        WriteReferenceProvenance(manifestData, label, provenReadings);
        return new(project with { Manifest = project.Manifest with { ReferenceLanguages = languages, ExtensionData = manifestData }, Entries = entries }, language, edition, matched, textOnly.Values.Sum(), changed, coverage, skips);
    }

    private static void WriteReferenceProvenance(Dictionary<string, JsonElement> manifestData, string label, Dictionary<string, string[]> readings)
    {
        string member = LocalizationReferenceProvenance.ManifestMember;
        if (!manifestData.TryGetValue(member, out JsonElement previous))
        {
            if (readings.Count != 0)
            {
                manifestData[member] = JsonSerializer.SerializeToElement(new Dictionary<string, Dictionary<string, string[]>>(StringComparer.Ordinal) { [label] = readings });
            }
            return;
        }
        if (previous.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"The manifest's {member} member must be an object.");
        }
        Dictionary<string, JsonElement> columns = previous.EnumerateObject().ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        if (readings.Count != 0)
        {
            columns[label] = JsonSerializer.SerializeToElement(readings);
        }
        else
        {
            columns.Remove(label);
        }
        manifestData[member] = JsonSerializer.SerializeToElement(columns);
    }

    private static GameDataEdition? ReferenceOrigin(LocalizationManifest manifest, string baseLanguage)
    {
        if (Enum.TryParse(manifest.SourceEdition, out GameDataEdition sourceEdition))
        {
            return sourceEdition;
        }
        string[] editions = manifest.CompatibleVariants.Where(profile => profile.EndsWith(":" + baseLanguage, StringComparison.OrdinalIgnoreCase))
            .Select(profile => profile[..profile.IndexOf(':')]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return editions.Length == 1 && Enum.TryParse(editions[0], out sourceEdition) ? sourceEdition : null;
    }

    private static Dictionary<string, string[]> ReferenceBanks(IEnumerable<LocalizationSourceEntry> entries) =>
        entries.Where(entry => entry.Category == "Fields").GroupBy(entry => entry.File, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Metadata.GetValueOrDefault("Source MSD SHA256")).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.Ordinal);

    private static ReferenceMatch FieldReference(LocalizationRecord row, string baseLanguage, string language, GameDataEdition? origin, GameDataEdition edition, ReferenceDatasetIndex reference, ReferenceDatasetIndex? companion, Dictionary<string, string[]> projectBanks, Dictionary<string, string[]> nativeBanks, Dictionary<string, string[]> bridgeBanks)
    {
        int contextStart = row.Key.IndexOf("_ctx_", StringComparison.Ordinal);
        string baseKey = contextStart < 0 ? row.Key : row.Key[..contextStart];
        string suffix = contextStart < 0 ? "" : row.Key[contextStart..];
        string? originHash = ReferenceMetadata(row, "Source MSD SHA256") ?? SingleBank(projectBanks, row.File);
        string? nativeHash = SingleBank(nativeBanks, row.File);
        string canonical = baseKey;
        if (edition == GameDataEdition.Classic && baseLanguage != "en")
        {
            return ReferenceMatch.Skip("english-edition-bridge-unavailable");
        }
        if (language == baseLanguage)
        {
            if (baseLanguage == "en" && origin is GameDataEdition from && from != edition)
            {
                if (nativeHash is null)
                {
                    return ReferenceMatch.Skip("native-resource-missing");
                }
                if (!FieldCorrespondenceCatalog.TryGetEnglishEditionKey(baseKey, from, originHash, edition, nativeHash, out string? projected))
                {
                    return ReferenceMatch.Skip("english-edition-key-unverified");
                }
                canonical = projected;
            }
            else if (originHash is not null && !StringComparer.OrdinalIgnoreCase.Equals(originHash, nativeHash) && nativeHash is not null)
            {
                return ReferenceMatch.Skip("origin-binding-mismatch");
            }
            else if (origin is null && originHash is null)
            {
                return ReferenceMatch.Skip("origin-edition-unverified");
            }
        }
        else if (baseLanguage == "en" && origin == GameDataEdition.Classic)
        {
            if (!FieldCorrespondenceCatalog.TryGetEnglishEditionKey(baseKey, GameDataEdition.Classic, originHash, GameDataEdition.Remastered, SingleBank(bridgeBanks, row.File), out string? projected))
            {
                return ReferenceMatch.Skip("english-edition-key-unverified");
            }
            canonical = projected;
        }

        if (language != baseLanguage && origin == edition && companion is not null && !MatchesReferenceOrigin(row, FindReference(companion, row, row.Key)))
        {
            return ReferenceMatch.Skip("origin-binding-mismatch");
        }

        IReadOnlyList<string> candidates = language == baseLanguage ? [canonical] : FieldCorrespondenceCatalog.GetEquivalents(canonical, language);
        if (candidates.Count == 0)
        {
            return ReferenceMatch.Skip("no-reviewed-counterpart");
        }

        List<LocalizationSourceEntry> matches = [];
        List<string> keys = [];
        foreach (string candidate in candidates)
        {
            LocalizationSourceEntry? entry = FindReference(reference, row, candidate + suffix);
            if (entry is null && suffix.Length != 0)
            {
                // The exporter derives every presentation variant from the same MSD slot. A unique mapped
                // slot can supply the reading even when the localized script uses a different layout.
                bool verifiedOrigin = origin == edition && companion is not null || origin == GameDataEdition.Classic && baseLanguage == "en" && edition == GameDataEdition.Remastered;
                if (language != baseLanguage && verifiedOrigin && candidates.Count == 1 && FindReference(reference, row, candidate) is { } nativeBase)
                {
                    return new([nativeBase], [nativeBase.Key], TextOnly: true);
                }
                return ReferenceMatch.Skip("presentation-context-unverified", candidates);
            }
            keys.Add(candidate + suffix);
            if (entry is not null)
            {
                matches.Add(entry);
            }
        }
        if (matches.Count != candidates.Count)
        {
            return ReferenceMatch.Skip("native-key-missing", keys);
        }
        if (language == baseLanguage && origin == edition && !MatchesReferenceOrigin(row, matches[0]))
        {
            return ReferenceMatch.Skip("origin-binding-mismatch", keys);
        }
        return new(matches, keys);
    }

    private static ReferenceMatch SystemReference(LocalizationRecord row, string language, ReferenceDatasetIndex reference)
    {
        if (language != "en")
        {
            return ReferenceMatch.Skip("authored-interface-language-unavailable");
        }
        LocalizationSourceEntry? entry = FindReference(reference, row, "$en_" + WithoutLanguagePrefix(row.Key));
        return entry is null ? ReferenceMatch.Skip("authored-interface-key-missing") : new([entry], [entry.Key]);
    }

    private static ReferenceMatch NonFieldReference(LocalizationRecord row, string baseLanguage, string language, GameDataEdition? origin, GameDataEdition edition, ReferenceDatasetIndex reference, ReferenceDatasetIndex? companion)
    {
        LocalizationSourceEntry[] matches = reference.CommonKeys.TryGetValue(WithoutLanguagePrefix(row.Key), out var candidates) ? candidates.Where(entry => entry.Category == row.Category && entry.File == row.File).ToArray() : [];
        if (matches.Length == 0)
        {
            return ReferenceMatch.Skip("native-key-missing");
        }
        if (matches.Length > 1)
        {
            return ReferenceMatch.Skip("ambiguous-native-key", matches.Select(entry => entry.Key).ToArray());
        }

        LocalizationSourceEntry native = matches[0];
        if (origin == edition && !MatchesReferenceOrigin(row, language == baseLanguage ? native : companion is null ? null : FindReference(companion, row, row.Key)))
        {
            return ReferenceMatch.Skip("origin-binding-mismatch", [native.Key]);
        }
        if (origin != edition)
        {
            LocalizationSourceEntry? editionEntry = language == baseLanguage ? native : companion is null ? null : FindReference(companion, row, row.Key);
            string? stored = ReferenceMetadata(row, "Native Source Fingerprint") ?? ReferenceMetadata(row, "Source SHA256");
            string? selected = editionEntry?.Metadata.GetValueOrDefault("Native Source Fingerprint") ?? editionEntry?.Metadata.GetValueOrDefault("Source SHA256");
            bool fingerprintMatches = stored is not null && StringComparer.OrdinalIgnoreCase.Equals(stored, selected);
            bool reviewed = FieldCorrespondenceCatalog.IsReviewedEditionTextEquivalent(row.Key, origin ?? edition, stored, edition, selected);
            if (editionEntry is null || !fingerprintMatches && !reviewed && !MatchesReferenceText(row, editionEntry))
            {
                return ReferenceMatch.Skip("non-field-edition-key-unverified", [native.Key]);
            }
        }
        return new([native], [native.Key]);
    }

    private static string? SingleBank(Dictionary<string, string[]> banks, string file) => banks.TryGetValue(file, out string[]? hashes) && hashes.Length == 1 ? hashes[0] : null;

    private static LocalizationSourceEntry? FindReference(ReferenceDatasetIndex index, LocalizationRecord row, string key) =>
        index.Exact.TryGetValue(key, out LocalizationSourceEntry? entry) && entry.Category == row.Category && entry.File == row.File ? entry : null;

    private static string? ReferenceMetadata(LocalizationRecord row, string name) =>
        row.ExtensionData.TryGetValue("sourceContext", out JsonElement context) && context.ValueKind == JsonValueKind.Object && context.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool MatchesReferenceOrigin(LocalizationRecord row, LocalizationSourceEntry? entry)
    {
        if (entry is null)
        {
            return false;
        }
        if (LocalizationReferenceProvenance.SourceHash(row) is string sourceHash && IsPresent(entry) && !MatchesReferenceText(row, entry, sourceHash))
        {
            return false;
        }
        if (row.Category != "Fields")
        {
            return true;
        }
        return new[] { "Source MSD SHA256", "JSM SHA256" }.All(name =>
            ReferenceMetadata(row, name) is not string expected || StringComparer.OrdinalIgnoreCase.Equals(expected, entry.Metadata.GetValueOrDefault(name)));
    }

    private static bool MatchesReferenceText(LocalizationRecord row, LocalizationSourceEntry entry, string? sourceHash = null)
    {
        sourceHash ??= LocalizationReferenceProvenance.SourceHash(row);
        return sourceHash is not null && StringComparer.OrdinalIgnoreCase.Equals(sourceHash, LocalizationProjectIO.FingerprintPlayerSource(row, entry.Text));
    }

    private sealed record ReferenceMatch(IReadOnlyList<LocalizationSourceEntry> Entries, IReadOnlyList<string> Keys, string? Reason = null, bool TextOnly = false)
    {
        public static ReferenceMatch Skip(string reason, IReadOnlyList<string>? keys = null) => new([], keys ?? [], reason);
    }
}
