using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Mods.Localization;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Localization;

internal static partial class TranslationReviewRules
{
    private readonly record struct QuoteSpan(int First, int Last, bool Safe)
    {
        public bool HasText => First <= Last;
    }

    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "вв", "гг", "гл", "др", "ед", "им", "коп", "млн", "млрд", "напр", "ном", "обл", "ок", "пр", "рис", "руб", "св", "см", "ср", "ст", "стр", "тыс", "ул", "экз"
    };

    private static void ReviewQuotes(ReviewState state, string source, IReadOnlyDictionary<string, string>? characterNames, bool choiceBound)
    {
        string target = state.Target;
        IReadOnlyList<FieldSpeakerLabels.Page> sourcePages = FieldSpeakerLabels.Read(source);
        IReadOnlyList<FieldSpeakerLabels.Page> targetPages = FieldSpeakerLabels.Read(target);
        if (sourcePages.Count != targetPages.Count)
        {
            return;
        }
        if (!TryPromptEnd(source, target, choiceBound, out int sourceEnd) || !TryPromptEnd(target, source, choiceBound, out int targetEnd))
        {
            return;
        }

        QuoteSpan[] sourceSpans = new QuoteSpan[sourcePages.Count];
        QuoteSpan[] targetSpans = new QuoteSpan[targetPages.Count];
        for (int index = 0; index < sourcePages.Count; index++)
        {
            FieldSpeakerLabels.Page sourcePage = sourcePages[index];
            FieldSpeakerLabels.Page targetPage = targetPages[index];
            FieldSpeakerLabels.Label? sourceLabel = sourcePage.FirstLine;
            FieldSpeakerLabels.Label? targetLabel = targetPage.FirstLine;
            bool sourceSpeaker = IsProvenSpeaker(sourceLabel);
            bool targetSpeaker = sourceSpeaker && targetLabel is not null;
            bool targetSpeakerProven = !targetSpeaker || IsProvenSpeaker(targetLabel) || FieldSpeakerLabels.TaggedCharacter(targetLabel!.Text.TrimEnd(':')) is not null
                || targetLabel.Text.Equals(sourceLabel!.Text, StringComparison.OrdinalIgnoreCase)
                || characterNames?.TryGetValue(sourceLabel.Text.TrimEnd(':'), out string? attested) == true && targetLabel.Text.TrimEnd(':') == attested;
            sourceSpans[index] = ReadQuoteSpan(source, sourceSpeaker ? sourceLabel!.DialogueStart : sourcePage.Start, Math.Min(sourceEnd, sourcePage.Start + sourcePage.Length), true);
            targetSpans[index] = ReadQuoteSpan(target, targetSpeaker ? targetLabel!.DialogueStart : targetPage.Start, Math.Min(targetEnd, targetPage.Start + targetPage.Length), targetSpeakerProven);
            if (index > 0 && targetSpans[index].HasText && target[targetSpans[index].First] is '.' or ',')
            {
                targetSpans[index] = targetSpans[index] with { Safe = false };
                targetSpans[index - 1] = targetSpans[index - 1] with { Safe = false };
            }
        }

        List<Edit> edits = [];
        for (int start = 0; start < sourceSpans.Length; start++)
        {
            QuoteSpan first = sourceSpans[start];
            if (!first.HasText || !FieldSpeakerLabels.IsOpeningQuote(source[first.First]))
            {
                continue;
            }
            for (int end = start; end < sourceSpans.Length; end++)
            {
                QuoteSpan last = sourceSpans[end];
                if (!last.HasText || !EnclosesText(source, first.First, last.Last))
                {
                    continue;
                }
                QuoteSpan firstTarget = targetSpans[start];
                QuoteSpan lastTarget = targetSpans[end];
                if (start > 0 && !EndsUtteranceAtPageBoundary(target, targetSpans[start - 1], firstTarget))
                {
                    firstTarget = firstTarget with { Safe = false };
                }
                if (end + 1 < targetSpans.Length && !EndsUtteranceAtPageBoundary(target, lastTarget, targetSpans[end + 1]))
                {
                    lastTarget = lastTarget with { Safe = false };
                }
                ReviewQuoteSpan(state, firstTarget, lastTarget, edits);
                start = end;
                break;
            }
        }
        state.Apply(edits);
    }

    private static bool EndsUtteranceAtPageBoundary(string text, QuoteSpan previous, QuoteSpan next)
    {
        if (!previous.HasText || !next.HasText)
        {
            return true;
        }
        ReadOnlySpan<char> previousText = text.AsSpan(previous.First, previous.Last - previous.First + 1);
        if (previousText.Count('(') > previousText.Count(')') || previousText.Count('[') > previousText.Count(']'))
        {
            return false;
        }
        int end = previous.Last;
        while (end > previous.First && (IsClosingQuote(text[end]) || text[end] is ')' or ']'))
        {
            end--;
        }
        if (text[end] is not ('.' or '!' or '?' or '…'))
        {
            return false;
        }
        for (int index = next.First; index <= next.Last; index++)
        {
            if (text[index] == '{')
            {
                // A leading content control can expand to a full name with either script.
                return true;
            }
            if (char.IsLetterOrDigit(text[index]))
            {
                return !char.IsLower(text[index]);
            }
        }
        return true;
    }

    private static QuoteSpan ReadQuoteSpan(string text, int start, int stop, bool safe)
    {
        if (start >= stop)
        {
            return new(start, start - 1, safe);
        }
        string segment = text[start..stop];
        Match opening = QuotePrefix().Match(segment);
        Match closing = QuoteSuffix().Match(segment, opening.Length);
        return new(start + opening.Length, start + closing.Index - 1, safe);
    }

    private static void ReviewQuoteSpan(ReviewState state, QuoteSpan first, QuoteSpan last, List<Edit> edits)
    {
        if (!first.HasText || !last.HasText)
        {
            return;
        }
        int start = first.First;
        int end = last.Last;
        string target = state.Target;
        bool externalPeriod = end > start && target[end] == '.' && IsClosingQuote(target[end - 1]);
        if (externalPeriod)
        {
            end--;
        }
        bool hasOpen = FieldSpeakerLabels.IsOpeningQuote(target[start]);
        bool hasClose = IsClosingQuote(target[end]);
        bool movePeriod = !externalPeriod && IsSentencePeriod(target, start, hasClose ? end - 1 : end);
        if (hasOpen && hasClose && EnclosesText(target, start, end) && target[start] == '«' && target[end] == '»' && !movePeriod)
        {
            return;
        }

        bool safe = first.Safe && last.Safe && start < end && !target.AsSpan(start, end - start + 1).Contains("{Invalid}", StringComparison.Ordinal)
            && (!hasOpen || !hasClose || EnclosesText(target, start, end))
            && (!hasOpen || hasClose || IsUnmatchedOpening(target, start, end))
            && (!hasClose || hasOpen || IsUnmatchedClosing(target, start, end));
        int interiorStart = start + (hasOpen ? 1 : 0);
        int interiorEnd = end - (hasClose ? 1 : 0);
        if (interiorEnd < interiorStart)
        {
            return;
        }

        // Preserve existing inner quotations. A Russian inner guillemet pair would need editorial handling.
        safe &= !target.AsSpan(interiorStart, interiorEnd - interiorStart + 1).ContainsAny('«', '»');
        safe &= ContainsRussian().Match(target, interiorStart, interiorEnd - interiorStart + 1).Success;
        if (!safe)
        {
            state.Report("dialogue-quotes", "Verify the reference's enclosing quotation at the corresponding dialogue boundary.");
            return;
        }

        if (target[start] != '«')
        {
            edits.Add(new(start, hasOpen ? 1 : 0, "«"));
        }
        if (movePeriod)
        {
            edits.Add(new(hasClose ? end - 1 : end, hasClose ? 2 : 1, "»."));
        }
        else if (target[end] != '»')
        {
            edits.Add(new(hasClose ? end : end + 1, hasClose ? 1 : 0, "»"));
        }
        state.Report("dialogue-quotes", "Use Russian guillemets at the enclosing dialogue boundary.", true);
    }

    private static bool IsSentencePeriod(string text, int start, int end)
    {
        if (end <= start || text[end] != '.' || text[end - 1] is '.' or '…')
        {
            return false;
        }
        string before = text[start..end];
        Match word = FinalDialogueWord().Match(before);
        return word.Success && word.Value.Any(char.IsLower) && !Abbreviations.Contains(word.Value) || FinalDynamicTag().IsMatch(before);
    }

    private static bool TryPromptEnd(string text, string counterpart, bool choiceBound, out int end)
    {
        end = text.Length;
        FieldMessageContext.TryParseTag(0, text, out FieldMessageContext? context, out int length);
        if (context?.Choices is { } choices)
        {
            return TryChoiceOffset(text, length, choices.FirstLine, out end);
        }
        FieldMessageContext.TryParseTag(0, counterpart, out FieldMessageContext? otherContext, out int otherLength);
        if (otherContext?.Choices is { } otherChoices && LocalizationCatalog.CountAuthoredLines(text[length..]) == LocalizationCatalog.CountAuthoredLines(counterpart[otherLength..]))
        {
            return TryChoiceOffset(text, length, otherChoices.FirstLine, out end);
        }
        return !choiceBound && otherContext?.Choices is null && !text.Contains("{Option ", StringComparison.Ordinal);
    }

    private static bool TryChoiceOffset(string text, int start, int firstLine, out int end)
    {
        end = start;
        if (firstLine == 0)
        {
            return true;
        }
        MatchCollection lines = LineBreaks().Matches(text, start);
        if (firstLine < 0 || firstLine > lines.Count)
        {
            return false;
        }
        end = lines[firstLine - 1].Index;
        return true;
    }

    private static bool EnclosesText(string text, int start, int end)
    {
        char open = text[start];
        char close = ClosingQuote(open);
        if (close == '\0' || text[end] != close || start >= end)
        {
            return false;
        }
        return FindClose(text, start, end, open, close) == end;
    }

    private static bool IsUnmatchedOpening(string text, int start, int end)
    {
        return FindClose(text, start, end, text[start], ClosingQuote(text[start])) < 0;
    }

    private static bool IsUnmatchedClosing(string text, int start, int end)
    {
        char close = text[end];
        if (close == '"')
        {
            return text.AsSpan(start, end - start + 1).Count('"') == 1;
        }
        char open = close switch
        {
            '»' => '«',
            '”' => '“',
            '“' => '”',
            _ => '\0'
        };
        return open != '\0' && !text.AsSpan(start, end - start).Contains(open)
            && (close != '“' || !text.AsSpan(start, end - start).Contains('„'));
    }

    private static int FindClose(string text, int start, int end, char open, char close)
    {
        int depth = 1;
        for (int index = start + 1; index <= end; index++)
        {
            if (text[index] == close)
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
            else if (text[index] == open)
            {
                depth++;
            }
        }
        return -1;
    }

    private static bool IsClosingQuote(char character)
    {
        return character is '"' or '”' or '»' or '“';
    }

    private static char ClosingQuote(char character)
    {
        return character switch
        {
            '"' => '"',
            '“' or '‟' => '”',
            '”' => '“',
            '«' => '»',
            '„' => '“',
            _ => '\0'
        };
    }

    [GeneratedRegex(@"\A(?:[ \t\r\n]|\{(?:Color [^{}\r\n]+|Pause [0-9]+|Speed [0-9]+|Speaker|Line)\})*", RegexOptions.CultureInvariant)]
    private static partial Regex QuotePrefix();

    [GeneratedRegex(@"(?:[ \t]|\{(?:Color [^{}\r\n]+|Pause [0-9]+|Speed [0-9]+|End)\})*\z", RegexOptions.CultureInvariant)]
    private static partial Regex QuoteSuffix();

    [GeneratedRegex(@"\p{IsCyrillic}", RegexOptions.CultureInvariant)]
    private static partial Regex ContainsRussian();

    [GeneratedRegex(@"(?<![\p{L}.])\p{IsCyrillic}{2,}\z", RegexOptions.CultureInvariant)]
    private static partial Regex FinalDialogueWord();
}
