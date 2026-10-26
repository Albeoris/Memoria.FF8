using System.Globalization;

namespace Memoria.FF8.Assets.Localization;

public static class FieldLocalizationKey
{
    public static string Create(string language, string field, int ordinal, int insertion = 0)
    {
        ValidateField(field);
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfNegative(insertion);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(insertion, 99);
        return "$" + CanonicalLanguage(language) + "_" + field + "_" + ordinal.ToString("D4", CultureInfo.InvariantCulture) + insertion.ToString("D2", CultureInfo.InvariantCulture);
    }

    internal static string CanonicalLanguage(string language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return language.ToLowerInvariant() switch
        {
            "en" => "en",
            "fr" => "fr",
            "de" => "de",
            "es" => "es",
            "it" => "it",
            "ja" or "jp" => "jp",
            _ => throw new ArgumentException("Unsupported field localization language.", nameof(language))
        };
    }

    internal static void ValidateField(string field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (field.Length == 0 || field.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new ArgumentException("Field identifiers must contain only ASCII letters, digits, underscores, and hyphens.", nameof(field));
        }
    }
}

/// <summary>Writes the complete initial same-ordinal key mapping, retaining every known bank slot.</summary>
public static class FieldLocalizationMapping
{
    private static readonly string[] Languages = ["en", "fr", "de", "es", "it", "jp"];

    public static int WriteCsv(TextWriter writer, string field, IReadOnlyList<FieldCompatibilityInventory> inventories, IReadOnlyList<FieldCompatibilityResult> comparisons, FieldUniversallyUnusedEvidence? unused = null, bool includeHeader = true, string? fieldName = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(inventories);
        ArgumentNullException.ThrowIfNull(comparisons);
        FieldLocalizationKey.ValidateField(field);
        Dictionary<string, FieldCompatibilityInventory> banks = new(StringComparer.Ordinal);
        foreach (FieldCompatibilityInventory inventory in inventories)
        {
            if (inventory is null || inventory.Field != field || inventory.DeclaredTextCount < 0 || inventory.BankCountKnown != (inventory.DeclaredTextCount is not null))
            {
                throw new ArgumentException("Inventories must describe the requested field with nonnegative bank counts.", nameof(inventories));
            }
            string language = FieldLocalizationKey.CanonicalLanguage(inventory.Language);
            if (!banks.TryAdd(language, inventory))
            {
                throw new ArgumentException("Inventories must have distinct languages, including Japanese aliases.", nameof(inventories));
            }
        }
        Dictionary<string, FieldCompatibilityResult> pairs = new(StringComparer.Ordinal);
        foreach (FieldCompatibilityResult comparison in comparisons)
        {
            if (comparison is null || comparison.English.Field != field || comparison.Localized.Field != field || FieldLocalizationKey.CanonicalLanguage(comparison.English.Language) != "en")
            {
                throw new ArgumentException("Comparisons must describe this field against English.", nameof(comparisons));
            }
            string language = FieldLocalizationKey.CanonicalLanguage(comparison.Localized.Language);
            if (language == "en" || !pairs.TryAdd(language, comparison) || !banks.TryGetValue("en", out FieldCompatibilityInventory? english) || !banks.TryGetValue(language, out FieldCompatibilityInventory? local) || !SameInventory(english, comparison.English) || !SameInventory(local, comparison.Localized))
            {
                throw new ArgumentException("Comparisons must be distinct and agree with supplied inventories.", nameof(comparisons));
            }
        }
        HashSet<int> unusedOrdinals = [];
        bool unusedKnown = unused?.Complete == true;
        if (unused is not null)
        {
            if (unused.Field != field)
            {
                throw new ArgumentException("Unused evidence must describe the requested field.", nameof(unused));
            }
            if (unused.Complete)
            {
                FieldCompatibilityInventory[] normalized = inventories.Select(inventory => inventory with { Language = FieldLocalizationKey.CanonicalLanguage(inventory.Language) == "jp" ? "ja" : FieldLocalizationKey.CanonicalLanguage(inventory.Language) }).ToArray();
                FieldUniversallyUnusedEvidence actual = FieldUniversallyUnused.Find(normalized, ["en", "fr", "de", "es", "it", "ja"]);
                if (!actual.Complete || unused.Rule != actual.Rule || unused.Uncertainty is not null || unused.Languages.IsDefault || unused.Languages.Length != Languages.Length || !unused.Languages.Select(FieldLocalizationKey.CanonicalLanguage).ToHashSet(StringComparer.Ordinal).SetEquals(Languages) || unused.Ordinals.IsDefault || unused.Ordinals.Distinct().Count() != unused.Ordinals.Length || unused.Ordinals.Except(actual.Ordinals).Any())
                {
                    throw new ArgumentException("Complete unused evidence must agree with the supplied six-language inventories.", nameof(unused));
                }
                unusedOrdinals.UnionWith(unused.Ordinals);
            }
        }
        int count = banks.Values.Where(bank => bank.BankCountKnown && bank.DeclaredTextCount is not null).Select(bank => bank.DeclaredTextCount!.Value).DefaultIfEmpty(0).Max();
        if (includeHeader)
        {
            WriteRow(writer, [.. Languages, "Field", "Ordinal", "Field Name", "fr Status", "de Status", "es Status", "it Status", "jp Status", "Unused Across Languages", .. Languages.Select(language => language + " Source Ordinal")]);
        }
        if (count == 0)
        {
            WriteRow(writer, [.. Languages.Select(_ => string.Empty), field, string.Empty, fieldName ?? string.Empty, .. Languages.Skip(1).Select(language => PlaceholderStatus(language)), unusedKnown ? "true" : "Unknown", .. Languages.Select(_ => string.Empty)]);
            return 1;
        }
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            List<string> values = Languages.Select(language => banks.TryGetValue(language, out FieldCompatibilityInventory? bank) && HasSlot(bank, ordinal) ? FieldLocalizationKey.Create(language, field, ordinal) : string.Empty).ToList();
            values.Add(field);
            values.Add(ordinal.ToString(CultureInfo.InvariantCulture));
            values.Add(fieldName ?? string.Empty);
            values.AddRange(Languages.Skip(1).Select(language => Status(language, ordinal)));
            values.Add(unusedKnown ? unusedOrdinals.Contains(ordinal) ? "true" : "false" : "Unknown");
            values.AddRange(Languages.Select(language => banks.TryGetValue(language, out FieldCompatibilityInventory? bank) && HasSlot(bank, ordinal) ? ordinal.ToString(CultureInfo.InvariantCulture) : string.Empty));
            WriteRow(writer, values);
        }
        return count;

        string PlaceholderStatus(string language)
        {
            if (!banks.TryGetValue(language, out FieldCompatibilityInventory? local))
            {
                return "AbsentLocalizedSlot";
            }
            if (!local.BankCountKnown || local.DeclaredTextCount is null)
            {
                return "UnknownBank";
            }
            if (!banks.ContainsKey("en"))
            {
                return "NoEnglishSlot";
            }
            return "EmptyBank";
        }

        string Status(string language, int ordinal)
        {
            if (unusedOrdinals.Contains(ordinal))
            {
                return "UnusedAllLanguages";
            }
            if (!banks.TryGetValue(language, out FieldCompatibilityInventory? local))
            {
                return "AbsentLocalizedSlot";
            }
            if (!local.BankCountKnown || local.DeclaredTextCount is null)
            {
                return "UnknownBank";
            }
            if (!HasSlot(local, ordinal))
            {
                return "AbsentLocalizedSlot";
            }
            if (!banks.TryGetValue("en", out FieldCompatibilityInventory? english))
            {
                return "NoEnglishSlot";
            }
            if (!english.BankCountKnown || english.DeclaredTextCount is null)
            {
                return "UnknownBank";
            }
            if (!HasSlot(english, ordinal))
            {
                return "NoEnglishSlot";
            }
            if (!Validated(english) || !Validated(local))
            {
                return "TechnicalUncertain";
            }
            if (!pairs.TryGetValue(language, out FieldCompatibilityResult? pair))
            {
                return "NotCompared";
            }
            if (pair.PotentialLocalOrdinals.Contains(ordinal) || pair.ImplicatedEnglishOrdinals.Contains(ordinal))
            {
                return "RequiresReview";
            }
            return pair.ProvenOrdinalMatches.Any(match => match.EnglishOrdinal == ordinal && match.LocalOrdinal == ordinal) ? "Proven" : "Unproven";
        }
    }

    private static bool HasSlot(FieldCompatibilityInventory inventory, int ordinal) => inventory.BankCountKnown && inventory.DeclaredTextCount is int count && ordinal < count;
    private static bool Validated(FieldCompatibilityInventory inventory) => inventory.ScriptValidated && inventory.BankValidated && inventory.StaticReferencesValidated;

    private static bool SameInventory(FieldCompatibilityInventory left, FieldCompatibilityInventory right)
    {
        if (left.Bank.IsDefault || right.Bank.IsDefault || left.ReferencedOrdinals.IsDefault || right.ReferencedOrdinals.IsDefault)
        {
            return false;
        }
        return left.ResourceIdentity == right.ResourceIdentity && left.JsmSha256 == right.JsmSha256 && left.JsmLength == right.JsmLength && left.ParserVersion == right.ParserVersion && left.BankCountKnown == right.BankCountKnown && left.DeclaredTextCount == right.DeclaredTextCount && left.ScriptValidated == right.ScriptValidated && left.BankValidated == right.BankValidated && left.StaticReferencesValidated == right.StaticReferencesValidated && left.Bank.Select(row => (row.Ordinal, row.Text)).SequenceEqual(right.Bank.Select(row => (row.Ordinal, row.Text))) && left.ReferencedOrdinals.SequenceEqual(right.ReferencedOrdinals);
    }

    private static void WriteRow(TextWriter writer, IEnumerable<string> values)
    {
        writer.WriteLine(string.Join(',', values.Select(value => value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value)));
    }
}
