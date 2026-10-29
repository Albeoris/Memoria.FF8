using System.Globalization;
using Memoria.FF8.Events;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8;

internal sealed record DialoguePlacedRun(DialogueRun Run, float X);
internal sealed record DialogueVisualLine(IReadOnlyList<DialoguePlacedRun> Runs, float Width, int SourceLineIndex, bool IsChoice);
internal sealed record DialogueVisualPage(string? Speaker, IReadOnlyList<DialogueVisualLine> Lines, float Width, float Height, bool HasChoices, float SpeakerScale = 1, int SourcePageIndex = 0);

/// <summary>Presentation pages never alter native line numbers, dialogue text or script completion.</summary>
internal static class DialogueLayout
{
    internal static IReadOnlyList<DialogueVisualPage> CreateNative(DialogueDocument document, DialogueState dialogue, DialogueWindow? window, float lineHeight, Func<string, float> measure)
    {
        List<DialogueVisualPage> result = [];
        for (int pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
        {
            DialoguePage page = document.Pages[pageIndex];
            List<DialogueVisualLine> lines = [];
            foreach (DialogueLine line in page.Lines)
            {
                bool choice = dialogue.FirstChoice is int first && dialogue.LastChoice is int last && line.SourceLineIndex >= first && line.SourceLineIndex <= last;
                float x = choice ? ChoiceIndent(lineHeight) : 0;
                List<DialoguePlacedRun> runs = [];
                foreach (DialogueRun run in line.Runs)
                {
                    runs.Add(new(run, x));
                    x += MeasureRun(run, measure, lineHeight);
                }
                lines.Add(new(runs, x, line.SourceLineIndex, choice));
            }
            float width = window is { Width: > 0 } ? Math.Max(0, window.Width - DialoguePresentation.Padding * 2) : lines.Select(line => line.Width).DefaultIfEmpty(0).Max();
            float height = window is { Height: > 0 } ? Math.Max(0, window.Height - DialoguePresentation.Padding * 2) : lines.Count * lineHeight;
            result.Add(new(null, lines, width, height, lines.Any(line => line.IsChoice), SourcePageIndex: pageIndex));
        }
        return result;
    }

    internal const float ScreenPadding = 8;
    internal const float SpeakerGap = 3;
    internal const float ChoicePointerGap = 2;

    internal static float ChoiceIndent(float lineHeight) => lineHeight * 1.5f + ChoicePointerGap;

    internal static float MeasureRun(DialogueRun run, Func<string, float> measure, float lineHeight)
    {
        return run.IsButton ? ButtonPrompt.Measure(run.Text, measure, lineHeight) : measure(run.Text);
    }

    private static float MeasureRuns(IEnumerable<DialogueRun> runs, Func<string, float> measure, float lineHeight)
    {
        float width = 0;
        string plain = "";
        foreach (DialogueRun run in runs)
        {
            if (run.IsButton)
            {
                width += measure(plain) + MeasureRun(run, measure, lineHeight);
                plain = "";
            }
            else
            {
                plain += run.Text;
            }
        }
        return width + measure(plain);
    }

    public static IReadOnlyList<DialogueVisualPage> Create(DialogueDocument document, DialogueState dialogue, DialogueWindow? window, float lineHeight, Func<string, float> measure)
    {
        const float maximumWidth = 320 - 2 * ScreenPadding - 2 * DialoguePresentation.Padding;
        float requestedWidth = Math.Clamp((window?.Width ?? 0) - 2 * DialoguePresentation.Padding, 1, maximumWidth);
        List<DialogueVisualPage> result = [];
        var sourcePages = document.Pages.ToArray();
        bool IsChoice(DialogueLine line) => line.SourceLineIndex >= dialogue.FirstChoice && line.SourceLineIndex <= dialogue.LastChoice;
        var choices = sourcePages.SelectMany(page => page.Lines).Where(IsChoice).ToArray();
        if (choices.Length > 0 && choices.Any(choice => !sourcePages[^1].Lines.Contains(choice)))
        {
            // Keep every authored body page separate, but defer all answers to one final decision page.
            // A native choice closes the prompt, so no authored page may remain unread after accepting it.
            for (int index = 0; index < sourcePages.Length; index++)
            {
                sourcePages[index] = sourcePages[index] with { Lines = sourcePages[index].Lines.Where(line => !IsChoice(line)).ToArray() };
            }
            sourcePages[^1] = sourcePages[^1] with { Lines = sourcePages[^1].Lines.Concat(choices).ToArray() };
        }
        for (int sourcePageIndex = 0; sourcePageIndex < sourcePages.Length; sourcePageIndex++)
        {
            var sourcePage = sourcePages[sourcePageIndex];
            float naturalWidth = sourcePage.Lines.Select(line => MeasureRuns((IsChoice(line) ? line : RemoveQuoteAlignmentSpace(line)).Runs, measure, lineHeight) + (line.IsQuoteStart ? 0 : measure(line.HangingIndent))).DefaultIfEmpty(0).Max();
            float width = Math.Min(maximumWidth, Math.Max(requestedWidth, naturalWidth));
            string? speaker = sourcePage.Speaker;
            float speakerWidth = speaker is null ? 0 : measure(speaker);
            float speakerScale = speakerWidth <= maximumWidth ? 1 : maximumWidth / speakerWidth;
            float headingHeight = speaker is null ? 0 : lineHeight * speakerScale + SpeakerGap;
            int screenLines = Math.Max(1, (int)MathF.Floor((240 - 2 * ScreenPadding - 2 * DialoguePresentation.Padding - headingHeight) / lineHeight));
            int scriptLines = (int)MathF.Floor(((window?.Height ?? 0) - 2 * DialoguePresentation.Padding - headingHeight) / lineHeight);
            int capacity = window is null ? screenLines : Math.Max(1, Math.Min(screenLines, Math.Max(8, scriptLines)));
            List<DialogueVisualLine> lines = [];
            foreach (var line in sourcePage.Lines.Where(line => !line.IsSpeaker))
            {
                bool choice = IsChoice(line);
                lines.AddRange(Wrap(line, width, measure, choice, lineHeight));
            }
            if (lines.Count == 0)
            {
                lines.Add(new DialogueVisualLine([], 0, -1, false));
            }
            // A choice and everything after it remain together: accepting it ends the native prompt.
            int choiceStart = lines.FindIndex(line => line.IsChoice);
            int position = 0;
            while (position < lines.Count)
            {
                int count = Math.Min(capacity, lines.Count - position);
                if (choiceStart >= position && choiceStart < position + count)
                {
                    count = choiceStart > position && lines.Count - position > capacity ? choiceStart - position : lines.Count - position;
                }
                var chunk = lines.GetRange(position, count);
                float actualWidth = Math.Max(speakerWidth * speakerScale, chunk.Max(line => line.Width));
                result.Add(new DialogueVisualPage(speaker, chunk, actualWidth, headingHeight + chunk.Count * lineHeight, chunk.Any(line => line.IsChoice), speakerScale, sourcePageIndex));
                position += count;
            }
        }
        return result;
    }

    internal static IEnumerable<DialogueVisualLine> Wrap(DialogueLine source, float maximumWidth, Func<string, float> measure, bool choice, float lineHeight)
    {
        if (choice)
        {
            float x = ChoiceIndent(lineHeight);
            List<DialoguePlacedRun> runs = [];
            foreach (var run in source.Runs)
            {
                runs.Add(new DialoguePlacedRun(run, x));
                x += MeasureRun(run, measure, lineHeight);
            }
            yield return new DialogueVisualLine(runs, x, source.SourceLineIndex, true);
            yield break;
        }
        source = RemoveQuoteAlignmentSpace(source);
        // Split only at grapheme boundaries. Keep word/whitespace break opportunities where possible.
        List<DialogueRun> elements = [];
        foreach (var run in source.Runs)
        {
            if (run.IsButton)
            {
                elements.Add(run);
                continue;
            }
            var enumerator = StringInfo.GetTextElementEnumerator(run.Text);
            while (enumerator.MoveNext())
            {
                string element = enumerator.GetTextElement();
                elements.Add(run with { Text = LocalizationTextTrace.Enabled ? LocalizationTextComposition.Slice(run.Text, enumerator.ElementIndex, element.Length) : element });
            }
        }
        int position = 0;
        bool first = true;
        do
        {
            float indent = first && source.IsQuoteStart ? 0 : measure(source.HangingIndent);
            int end = position;
            int breakAt = -1;
            float completedWidth = 0;
            string candidate = "";
            while (end < elements.Count)
            {
                DialogueRun element = elements[end];
                string next = element.IsButton ? "" : candidate + element.Text;
                float nextWidth = element.IsButton ? completedWidth + measure(candidate) + MeasureRun(element, measure, lineHeight) : completedWidth + measure(next);
                if (end > position && indent + nextWidth > maximumWidth)
                {
                    break;
                }
                if (element.IsButton)
                {
                    completedWidth = nextWidth;
                }
                candidate = next;
                if (!element.IsButton && string.IsNullOrWhiteSpace(element.Text))
                {
                    breakAt = end;
                }
                end++;
            }
            if (end < elements.Count && breakAt > position)
            {
                end = breakAt;
            }
            List<DialoguePlacedRun> placed = [];
            float x = indent;
            int cursor = position;
            while (cursor < end)
            {
                var run = elements[cursor++];
                while (!run.IsButton && cursor < end && !elements[cursor].IsButton && elements[cursor].Color == run.Color && elements[cursor].Blink == run.Blink)
                {
                    string next = elements[cursor++].Text;
                    run = run with { Text = LocalizationTextTrace.Enabled ? LocalizationTextComposition.Concat(run.Text, next) : run.Text + next };
                }
                placed.Add(new DialoguePlacedRun(run, x));
                x += MeasureRun(run, measure, lineHeight);
            }
            yield return new DialogueVisualLine(placed, x, source.SourceLineIndex, false);
            position = end;
            while (position < elements.Count && string.IsNullOrWhiteSpace(elements[position].Text))
            {
                position++;
            }
            first = false;
        }
        while (position < elements.Count);
    }

    private static DialogueLine RemoveQuoteAlignmentSpace(DialogueLine source)
    {
        if (source.HangingIndent.Length == 0 || !source.Text.StartsWith(' '))
        {
            return source;
        }
        // One authored leading space supplies alignment already provided by the measured quote indent.
        // Preserve any additional spaces and other whitespace as intentional content.
        DialogueRun[] runs = source.Runs.ToArray();
        int first = Array.FindIndex(runs, run => run.Text.Length > 0);
        runs[first] = runs[first] with { Text = LocalizationTextComposition.Slice(runs[first].Text, 1, runs[first].Text.Length - 1) };
        return source with { Runs = runs.Where(run => run.Text.Length > 0).ToArray() };
    }
}
