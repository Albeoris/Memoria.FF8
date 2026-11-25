using System.Text.RegularExpressions;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Localization;

internal sealed record TranslationReviewDiagnostic(string Rule, string Message, bool Fixable, bool Fixed);
internal sealed record TranslationReviewResult(string Target, IReadOnlyList<TranslationReviewDiagnostic> Diagnostics);

internal static partial class TranslationReviewRules
{
    public static TranslationReviewResult Review(string target, string? englishReference, string category, string key, bool fix, IReadOnlyDictionary<string, string>? characterNames = null, bool choiceBound = false, bool choiceMapping = false, IReadOnlyDictionary<string, string>? partyAliases = null)
    {
        ReviewState state = new(target, fix);
        if (target.Length == 0 || target == LocalizationValue.EmptyTarget)
        {
            return new(target, state.Diagnostics);
        }

        bool field = category == "Fields";
        ReviewTagSyntax(state, englishReference, category, key);

        bool editorial = !field || !IsInvalidPresentation(state.Target, category, key);
        string? contract = englishReference;
        if (editorial && field && englishReference is not null)
        {
            ReviewTerminalEnds(state, englishReference);
            contract = ReviewSpeakers(state, englishReference, characterNames, partyAliases);
            ReviewChoices(state, englishReference, choiceMapping);
        }
        ReviewControlContract(state, contract, category, key);

        if (editorial && englishReference is not null)
        {
            ReviewSpacing(state, englishReference, field, choiceBound);
            if (field)
            {
                ReviewQuotes(state, englishReference, characterNames, choiceBound);
            }
        }
        return new(fix ? state.Target : target, state.Diagnostics);
    }

    private readonly record struct Edit(int Start, int Length, string Value);
    private sealed class ReviewState(string target, bool fix)
    {
        public string Target { get; private set; } = target;
        public List<TranslationReviewDiagnostic> Diagnostics { get; } = [];

        public void Report(string rule, string message, bool fixable = false)
        {
            Diagnostics.Add(new(rule, message, fixable, fix && fixable));
        }

        public void Apply(IEnumerable<Edit> edits)
        {
            Target = ApplyEdits(Target, edits);
        }
    }

    private static string ApplyEdits(string text, IEnumerable<Edit> edits)
    {
        foreach (Edit edit in edits.OrderByDescending(edit => edit.Start))
        {
            text = string.Concat(text.AsSpan(0, edit.Start), edit.Value, text.AsSpan(edit.Start + edit.Length));
        }
        return text;
    }

    [GeneratedRegex(@"\{(?:Line|Next|Page)\}|\r\n|\r|\n", RegexOptions.CultureInvariant)]
    private static partial Regex LineBreaks();
}
