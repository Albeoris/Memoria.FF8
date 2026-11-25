using System.Text.RegularExpressions;
using Albeoris.Games.FF8.Jsm.Messages;
using Memoria.FF8.Mods.Localization;

namespace Memoria.FF8.Localization;

internal static partial class TranslationReviewRules
{
    private readonly record struct TextLine(int Start, int Length, bool PageStart);

    private static void ReviewSpacing(ReviewState state, string source, bool field, bool choiceBound)
    {
        string target = state.Target;
        int sourceEnd = source.Length;
        int targetEnd = target.Length;
        if (field && (!TryPromptEnd(source, target, choiceBound, out sourceEnd) || !TryPromptEnd(target, source, choiceBound, out targetEnd)))
        {
            return;
        }

        TextLine[] sourceLines = ReadLines(source, sourceEnd, field);
        TextLine[] targetLines = ReadLines(target, targetEnd, field);
        bool aligned = sourceLines.Length == targetLines.Length && sourceLines.Select(line => line.PageStart).SequenceEqual(targetLines.Select(line => line.PageStart));
        string sourcePrompt = source[..sourceEnd];
        List<Edit> edits = [];
        for (int index = 0; index < targetLines.Length; index++)
        {
            TextLine line = targetLines[index];
            string text = target.Substring(line.Start, line.Length);
            text = text[..QuoteSuffix().Match(text).Index];
            if (field && index > 0 && StrandedPunctuation().Match(text) is { Success: true } stranded)
            {
                ReviewStrandedPunctuation(state, source, sourceLines, targetLines, index, stranded, aligned, edits);
            }
            if (text.Contains('\t'))
            {
                continue;
            }
            string reference = aligned ? source.Substring(sourceLines[index].Start, sourceLines[index].Length) : sourcePrompt;
            if (field && aligned && IndentWord().Match(text) is { Success: true } indent && StartsSourceProse(reference))
            {
                edits.Add(new(line.Start, indent.Length, ""));
                state.Report("line-leading-space", $"Remove extra leading spaces on authored line {index + 1}.", true);
            }

            MatchCollection tags = BraceTokens().Matches(text);
            foreach (Match match in StrayPunctuationSpace().Matches(text))
            {
                if (tags.Any(tag => tag.Index < match.Index && tag.Index + tag.Length > match.Index)
                    || text[match.Index - 1] == '}' && !FinalDynamicTag().IsMatch(text[..match.Index]))
                {
                    continue;
                }
                char punctuation = match.Value[^1];
                bool safe = ReferencePunctuation(reference, punctuation) && !PaddedPunctuation().Matches(reference).Any(candidate => SamePunctuation(candidate.Value[^1], punctuation));
                if (safe)
                {
                    edits.Add(new(line.Start + match.Index, match.Length - 1, ""));
                }
                state.Report("punctuation-space", $"Check spacing before '{punctuation}' on authored line {index + 1}.", safe);
            }
        }
        state.Apply(edits);
    }

    private static void ReviewStrandedPunctuation(ReviewState state, string source, TextLine[] sourceLines, TextLine[] targetLines, int index, Match stranded, bool aligned, List<Edit> edits)
    {
        TextLine line = targetLines[index];
        TextLine previous = targetLines[index - 1];
        string previousText = state.Target.Substring(previous.Start, previous.Length);
        int boundary = QuoteSuffix().Match(previousText).Index;
        char punctuation = stranded.Groups[1].Value[0];
        bool safe = false;
        if (aligned && boundary > 0)
        {
            string original = source.Substring(sourceLines[index].Start, sourceLines[index].Length).TrimStart(' ');
            string originalPrevious = source.Substring(sourceLines[index - 1].Start, sourceLines[index - 1].Length);
            string previousVisible = previousText[..boundary];
            string originalVisible = originalPrevious[..QuoteSuffix().Match(originalPrevious).Index];
            Match dynamic = FinalDynamicTag().Match(previousVisible);
            bool attachesToWord = char.IsLetterOrDigit(previousVisible[^1]) || dynamic.Success && originalVisible.TrimEnd('"', '“', '”', '»', '.').EndsWith(dynamic.Value, StringComparison.Ordinal);
            bool matchingBoundary = !line.PageStart && punctuation == ',' || originalVisible.TrimEnd('"', '“', '”', '»').EndsWith(punctuation);
            safe = attachesToWord && matchingBoundary && StartsSourceProse(original);
        }
        if (safe)
        {
            edits.Add(new(previous.Start + boundary, 0, punctuation.ToString()));
            edits.Add(new(line.Start, stranded.Length, ""));
        }
        state.Report("line-stranded-punctuation", $"Punctuation '{punctuation}' begins authored line {index + 1}.", safe);
    }

    private static TextLine[] ReadLines(string text, int end, bool field)
    {
        int start = 0;
        if (field)
        {
            FieldMessageContext.TryParseTag(0, text, out _, out start);
        }
        if (start >= end)
        {
            return [];
        }

        List<TextLine> lines = [];
        bool pageStart = true;
        foreach (Match delimiter in LineBreaks().Matches(text, start))
        {
            if (delimiter.Index >= end)
            {
                break;
            }
            lines.Add(new(start, delimiter.Index - start, pageStart));
            pageStart = delimiter.Value is "{Page}" or "{Next}";
            start = delimiter.Index + delimiter.Length;
        }
        lines.Add(new(start, end - start, pageStart));
        return lines.ToArray();
    }

    private static bool StartsSourceProse(string line)
    {
        return line.Length > 0 && (char.IsLetterOrDigit(line[0]) || FieldSpeakerLabels.IsOpeningQuote(line[0]));
    }

    private static bool ReferencePunctuation(string source, char punctuation)
    {
        for (int index = 1; index < source.Length; index++)
        {
            if (SamePunctuation(source[index], punctuation) && (char.IsLetterOrDigit(source[index - 1]) || IsClosingQuote(source[index - 1])
                || source[index - 1] == '}' && FinalDynamicTag().IsMatch(source[..index]) || IsClosingQuote(punctuation) && source[index - 1] is '.' or '?' or '!')
                && (IsClosingQuote(punctuation) || index == source.Length - 1 || source[index + 1] is not ('.' or '?' or '!')))
            {
                return true;
            }
        }
        return false;
    }

    private static bool SamePunctuation(char original, char target)
    {
        return original == target || IsClosingQuote(target) && IsClosingQuote(original);
    }

    [GeneratedRegex(@"\A +(?=(?:«)?\p{IsCyrillic})", RegexOptions.CultureInvariant)]
    private static partial Regex IndentWord();

    [GeneratedRegex(@"(?<=[\p{IsCyrillic}»”}]) +(?:[,.;:?!](?![.?!…])|[»”])", RegexOptions.CultureInvariant)]
    private static partial Regex StrayPunctuationSpace();

    [GeneratedRegex(@"[\p{Zs}\t][,.;:?!»”“""]", RegexOptions.CultureInvariant)]
    private static partial Regex PaddedPunctuation();

    [GeneratedRegex(@"\A ?([,.;:?!])(?: (?=\p{IsCyrillic})|\z)", RegexOptions.CultureInvariant)]
    private static partial Regex StrandedPunctuation();

    [GeneratedRegex(@"\{(?:Char|Name|Var|Item) [^{}\r\n]+\}\z", RegexOptions.CultureInvariant)]
    private static partial Regex FinalDynamicTag();
}
