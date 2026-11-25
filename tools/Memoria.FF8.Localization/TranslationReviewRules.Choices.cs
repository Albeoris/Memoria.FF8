using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Mods.Localization;

namespace Memoria.FF8.Localization;

internal static partial class TranslationReviewRules
{
    private static void ReviewChoices(ReviewState state, string source, bool choiceMapping)
    {
        if (!FieldMessageContext.TryParseTag(0, source, out FieldMessageContext? sourceContext, out _) || sourceContext?.Choices is not { } expected)
        {
            return;
        }

        string target = state.Target;
        FieldMessageContext.TryParseTag(0, target, out FieldMessageContext? targetContext, out _);
        FieldMessageChoices? translated = targetContext?.Choices;
        TextLine[] sourceLines = ReadLines(source, source.Length, field: true);
        TextLine[] targetLines = ReadLines(target, target.Length, field: true);
        long expectedCount = (long)expected.LastLine - expected.FirstLine + 1;
        if (expected.FirstLine < 0 || expected.LastLine >= sourceLines.Length || expectedCount <= 0)
        {
            return;
        }
        if (translated is null)
        {
            if (targetLines.Length < expectedCount)
            {
                state.Report("choice-layout", "The target has fewer lines than the reference's selectable options.");
            }
            else if (!choiceMapping)
            {
                state.Report("choice-layout", "The target needs a verified mapping for the reference's option lines, initial selection and cancellation.");
            }
            return;
        }
        if (!FieldMessageTargetSyntax.PreservesChoiceSelections(translated, expected.FirstLine, expected.LastLine, expected.BeginLine, expected.CancelLine))
        {
            state.Report("choice-layout", "The target changes the reference's initial or cancellation option identity.");
            return;
        }
        if (translated.FirstLine < 0 || translated.LastLine >= targetLines.Length || (long)translated.LastLine - translated.FirstLine + 1 != expectedCount)
        {
            state.Report("choice-layout", "The target's declared option lines do not preserve the reference's selectable options.");
            return;
        }
        if (expected.FirstLine > 0 && translated.FirstLine == 0)
        {
            state.Report("choice-layout", "The reference has a prompt before its selectable options. Verify where the translated prompt is presented.");
        }

        List<Edit> edits = [];
        bool hasRussianContext = ContainsRussian().IsMatch(target);
        for (int option = 0; option < expectedCount; option++)
        {
            TextLine originalLine = sourceLines[expected.FirstLine + option];
            TextLine targetLine = targetLines[translated.FirstLine + option];
            string sourceOption = source.Substring(originalLine.Start, originalLine.Length);
            string targetOption = target.Substring(targetLine.Start, targetLine.Length);
            string original = ChoiceText(sourceOption);
            string replacement = ChoiceText(targetOption);
            if (!string.Equals(original, replacement, StringComparison.Ordinal))
            {
                continue;
            }
            bool knownResponse = original.Equals("Yes", StringComparison.OrdinalIgnoreCase) || original.Equals("No", StringComparison.OrdinalIgnoreCase) || original.Equals("Yeah", StringComparison.OrdinalIgnoreCase);
            if (!knownResponse && (!EnglishOption().IsMatch(original) || EnglishWords().Matches(original).Count < 2))
            {
                continue;
            }

            string? translatedResponse = hasRussianContext ? TranslateSimpleResponse(original) : null;
            bool fixable = translatedResponse is not null && IsPlainChoice(sourceOption) && IsPlainChoice(targetOption);
            if (fixable)
            {
                int start = targetOption.IndexOf(original, StringComparison.Ordinal);
                edits.Add(new(targetLine.Start + start, original.Length, translatedResponse!));
            }
            state.Report("choice-untranslated", $"Option {option + 1} repeats the English response '{original}'.", fixable);
        }
        state.Apply(edits);
    }

    private static string ChoiceText(string text)
    {
        return BraceTokens().Replace(text, "").Trim();
    }

    private static bool IsPlainChoice(string text)
    {
        return BraceTokens().Matches(text).All(match => match.Value == "{End}" || ChoiceColorTag().IsMatch(match.Value));
    }

    private static string? TranslateSimpleResponse(string response)
    {
        return response switch
        {
            "Yes" => "Да",
            "YES" => "ДА",
            "yes" => "да",
            "No" => "Нет",
            "NO" => "НЕТ",
            "no" => "нет",
            _ => null
        };
    }

    [GeneratedRegex(@"\A(?=.*(?: [a-z]|['’][a-z]|\?\z))[ .…(]*[A-Za-z][A-Za-z0-9 .'’,!?()…:\-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex EnglishOption();

    [GeneratedRegex(@"[A-Za-z]+(?:['’\-][A-Za-z]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex EnglishWords();

    [GeneratedRegex(@"\A\{Color [A-Za-z0-9]+\}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ChoiceColorTag();
}
