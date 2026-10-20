using System.Globalization;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed record DialoguePlacedRun(DialogueRun Run, float X);
internal sealed record DialogueVisualLine(IReadOnlyList<DialoguePlacedRun> Runs, float Width, int SourceLineIndex, bool IsChoice);
internal sealed record DialogueVisualPage(string? Speaker, IReadOnlyList<DialogueVisualLine> Lines, float Width, float Height, bool HasChoices, float SpeakerScale = 1);

/// <summary>Presentation pages never alter native line numbers, dialogue text or script completion.</summary>
internal static class DialogueLayout
{
    internal const float ScreenPadding = 8;
    internal const float SpeakerGap = 3;
    internal const float ChoicePointerGap = 2;

    internal static float ChoiceIndent(float lineHeight) => lineHeight * 1.5f + ChoicePointerGap;

    internal static float MeasureRun(DialogueRun run, Func<string, float> measure, float lineHeight)
    {
        return run.IsButton ? ButtonPrompt.Advance(measure(run.Text), lineHeight) : measure(run.Text);
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
        foreach (var sourcePage in sourcePages)
        {
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
                result.Add(new DialogueVisualPage(speaker, chunk, actualWidth, headingHeight + chunk.Count * lineHeight, chunk.Any(line => line.IsChoice), speakerScale));
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
                elements.Add(run with { Text = enumerator.GetTextElement() });
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
                    run = run with { Text = run.Text + elements[cursor++].Text };
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
        runs[first] = runs[first] with { Text = runs[first].Text[1..] };
        return source with { Runs = runs.Where(run => run.Text.Length > 0).ToArray() };
    }
}
