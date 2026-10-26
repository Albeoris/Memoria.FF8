using System.Collections.Immutable;

namespace Memoria.FF8.Assets.Localization;

public sealed record FieldUniversallyUnusedEvidence(string Field, string Rule, bool Complete, ImmutableArray<string> Languages, ImmutableArray<int> Ordinals, string? Uncertainty);

/// <summary>Excludes bank ordinals only after every supported language proves they have no script reference.</summary>
public static class FieldUniversallyUnused
{
    private const string Rule = "AllRequiredLanguagesStaticallyUnreferenced";
    private static readonly string[] SupportedLanguages = ["en", "fr", "de", "it", "es", "ja"];

    public static FieldUniversallyUnusedEvidence Find(IReadOnlyList<FieldCompatibilityInventory> inventories, IReadOnlyList<string> requiredLanguages)
    {
        ArgumentNullException.ThrowIfNull(inventories);
        ArgumentNullException.ThrowIfNull(requiredLanguages);
        HashSet<string> required = new(StringComparer.OrdinalIgnoreCase);
        foreach (string language in requiredLanguages)
        {
            if (string.IsNullOrWhiteSpace(language) || !required.Add(language))
            {
                throw new ArgumentException("Required languages must be nonempty and distinct.", nameof(requiredLanguages));
            }
        }
        if (!required.SetEquals(SupportedLanguages))
        {
            throw new ArgumentException("Universal exclusion requires all six supported languages: en, fr, de, it, es, ja.", nameof(requiredLanguages));
        }

        Dictionary<string, FieldCompatibilityInventory> byLanguage = new(StringComparer.OrdinalIgnoreCase);
        string? field = null;
        foreach (FieldCompatibilityInventory inventory in inventories)
        {
            if (inventory is null || string.IsNullOrWhiteSpace(inventory.Field) || string.IsNullOrWhiteSpace(inventory.Language) || !required.Contains(inventory.Language))
            {
                throw new ArgumentException("Inventories must name a field and a required language.", nameof(inventories));
            }
            field ??= inventory.Field;
            if (!StringComparer.Ordinal.Equals(field, inventory.Field) || !byLanguage.TryAdd(inventory.Language, inventory))
            {
                throw new ArgumentException("Inventories must describe one field with one inventory per language.", nameof(inventories));
            }
        }

        ImmutableArray<string> languages = SupportedLanguages.ToImmutableArray();
        string[] missing = SupportedLanguages.Where(language => !byLanguage.ContainsKey(language)).ToArray();
        if (missing.Length > 0)
        {
            return Incomplete("Required language inventory missing: " + string.Join(", ", missing) + ".");
        }
        HashSet<int> universe = [];
        HashSet<int> referenced = [];
        foreach (string language in SupportedLanguages)
        {
            FieldCompatibilityInventory inventory = byLanguage[language];
            if (!inventory.ScriptValidated || !inventory.BankValidated || !inventory.StaticReferencesValidated || !inventory.BankCountKnown || inventory.DeclaredTextCount is not int count || count < 0)
            {
                return Incomplete("Script, bank, or static reference validation is incomplete for " + language + ".");
            }
            if (inventory.Bank.IsDefault || inventory.Bank.Length != count || inventory.Bank.Where((row, index) => row.Ordinal != index).Any())
            {
                return Incomplete("The validated bank ordinal range is inconsistent for " + language + ".");
            }
            if (inventory.Contexts.IsDefault || inventory.ReferencedOrdinals.IsDefault || inventory.Contexts.Any(context => context.IsMessage && (context.MessageId is not int id || id < 0 || id >= count)) || inventory.ReferencedOrdinals.Any(id => id < 0 || id >= count))
            {
                return Incomplete("A script text reference cannot be bounded for " + language + ".");
            }
            universe.UnionWith(Enumerable.Range(0, count));
            referenced.UnionWith(inventory.ReferencedOrdinals);
            referenced.UnionWith(inventory.Contexts.Where(context => context.IsMessage).Select(context => context.MessageId!.Value));
        }
        return new(field!, Rule, true, languages, universe.Except(referenced).Order().ToImmutableArray(), null);

        FieldUniversallyUnusedEvidence Incomplete(string reason)
        {
            return new(field ?? string.Empty, Rule, false, languages, [], reason);
        }
    }

    public static FieldCompatibilityResult Apply(FieldCompatibilityResult result, FieldUniversallyUnusedEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(evidence);
        if (!StringComparer.Ordinal.Equals(result.English.Field, evidence.Field) || !StringComparer.Ordinal.Equals(result.Localized.Field, evidence.Field) || evidence.Languages.IsDefault || !evidence.Languages.Contains(result.English.Language, StringComparer.OrdinalIgnoreCase) || !evidence.Languages.Contains(result.Localized.Language, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Evidence must cover the result field and both languages.", nameof(evidence));
        }
        if (!evidence.Complete)
        {
            return result with { UniversallyUnusedEvidence = evidence };
        }
        if (evidence.Rule != Rule || evidence.Uncertainty is not null || evidence.Ordinals.IsDefault || evidence.Ordinals.Any(ordinal => ordinal < 0) || evidence.Ordinals.Distinct().Count() != evidence.Ordinals.Length || evidence.Languages.Length != SupportedLanguages.Length || !new HashSet<string>(evidence.Languages, StringComparer.OrdinalIgnoreCase).SetEquals(SupportedLanguages))
        {
            throw new ArgumentException("Complete evidence must use the universal exclusion rule and cover all six languages.", nameof(evidence));
        }
        HashSet<int> unused = evidence.Ordinals.ToHashSet();
        foreach (FieldCompatibilityInventory inventory in new[] { result.English, result.Localized })
        {
            if (!inventory.ScriptValidated || !inventory.BankValidated || !inventory.StaticReferencesValidated || !inventory.BankCountKnown || inventory.DeclaredTextCount is not int count || count < 0 || inventory.Bank.IsDefault || inventory.Bank.Length != count || inventory.Bank.Where((row, index) => row.Ordinal != index).Any())
            {
                throw new ArgumentException("Complete evidence requires a validated script, bank, and static references on both result inventories.", nameof(evidence));
            }
            if (inventory.Contexts.IsDefault || inventory.ReferencedOrdinals.IsDefault || inventory.Contexts.Any(context => context.IsMessage && (context.MessageId is not int id || id < 0 || id >= count || unused.Contains(id))) || inventory.ReferencedOrdinals.Any(id => id < 0 || id >= count || unused.Contains(id)))
            {
                throw new ArgumentException("Complete evidence conflicts with a result text reference or an unresolved message ID.", nameof(evidence));
            }
        }
        ImmutableArray<FieldCompatibilityReviewCase> cases = result.ReviewCases.Select(review => review with
        {
            EnglishOrdinals = review.EnglishOrdinals.Where(ordinal => !unused.Contains(ordinal)).ToImmutableArray(),
            LocalOrdinals = review.LocalOrdinals.Where(ordinal => !unused.Contains(ordinal)).ToImmutableArray()
        }).Where(review => !review.Kind.StartsWith("Unmatched", StringComparison.Ordinal) || !review.EnglishOrdinals.IsEmpty || !review.LocalOrdinals.IsEmpty).ToImmutableArray();
        return result with
        {
            PotentialLocalOrdinals = result.PotentialLocalOrdinals.Where(ordinal => !unused.Contains(ordinal)).ToImmutableArray(),
            ImplicatedEnglishOrdinals = result.ImplicatedEnglishOrdinals.Where(ordinal => !unused.Contains(ordinal)).ToImmutableArray(),
            ReviewCases = cases,
            SemanticReviewCases = cases.Length,
            UniversallyUnusedEvidence = evidence
        };
    }
}
