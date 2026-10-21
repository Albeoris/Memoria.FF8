using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

/// <summary>Preserves input prompts alongside ordinary UI text until layout and drawing.</summary>
internal static class ButtonPromptText
{
    private const char Open = '\uF000';
    private const char Close = '\uF001';

    internal static string Mark(string label)
    {
        return label.Length == 0 || Contains(label) ? label : $"{Open}{label}{Close}";
    }

    internal static bool Contains(string value)
    {
        return value.Contains(Open) || value.Contains(Close);
    }

    internal static string PlainText(string value)
    {
        return value.Replace(Open.ToString(), "").Replace(Close.ToString(), "");
    }

    internal static IEnumerable<DialogueRun> Runs(string value)
    {
        int cursor = 0;
        while (cursor < value.Length)
        {
            int open = value.IndexOf(Open, cursor);
            int close = open < 0 ? -1 : value.IndexOf(Close, open + 1);
            if (open < 0 || close < 0)
            {
                string remainder = PlainText(value[cursor..]);
                if (remainder.Length > 0)
                {
                    yield return new DialogueRun(remainder, Color.White);
                }
                yield break;
            }
            if (open > cursor)
            {
                yield return new DialogueRun(PlainText(value[cursor..open]), Color.White);
            }
            if (close > open + 1)
            {
                yield return new DialogueRun(value[(open + 1)..close], Color.White, IsButton: true);
            }
            cursor = close + 1;
        }
    }

    internal static DialogueVisualLine[] Lines(string value, float size, float width, string? family)
    {
        using SystemTextMeasurer measurer = SystemTextLayout.CreateMeasurer(size, family);
        List<DialogueVisualLine> lines = [];
        string[] source = value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int index = 0; index < source.Length; index++)
        {
            lines.AddRange(DialogueLayout.Wrap(new DialogueLine(Runs(source[index]).ToArray(), index), Math.Max(1, width), measurer.Measure, false, measurer.LineHeight));
        }
        return lines.ToArray();
    }

    internal static UiSize Measure(string value, float size, float width, string? family)
    {
        if (value.Length == 0 || size <= 0)
        {
            return new UiSize(0, 0);
        }
        DialogueVisualLine[] lines = Lines(value, size, width, family);
        return new UiSize(lines.Select(line => line.Width).DefaultIfEmpty(0).Max(), lines.Length * SystemTextLayout.LineHeight(size, family));
    }

    internal static void Draw(IUiCanvas canvas, string value, float size, UiRect bounds, UiColor color, string? family, UiAlignment alignment, UiTextOverflow overflow)
    {
        if (value.Length == 0 || size <= 0 || bounds.IsEmpty)
        {
            return;
        }
        UiTextViewport viewport = overflow.Layout(value, size, bounds, family, width => Measure(value, size, width, family));
        DialogueVisualLine[] lines = Lines(value, size, viewport.WrapWidth, family);
        using SystemTextMeasurer measurer = SystemTextLayout.CreateMeasurer(size, family);
        float y = bounds.Y - viewport.OffsetY;
        if (alignment is UiAlignment.Center or UiAlignment.End)
        {
            y += Math.Max(0, bounds.Height - viewport.Content.Height) * (alignment == UiAlignment.Center ? 0.5f : 1);
        }
        canvas.PushClip(bounds);
        try
        {
            foreach (DialogueVisualLine line in lines)
            {
                foreach (DialoguePlacedRun placed in line.Runs)
                {
                    float x = bounds.X + placed.X - viewport.OffsetX;
                    float width = DialogueLayout.MeasureRun(placed.Run, measurer.Measure, measurer.LineHeight);
                    UiRect runBounds = new(x, y, Math.Max(1, MathF.Ceiling(width) + 2), measurer.LineHeight);
                    if (placed.Run.IsButton)
                    {
                        ButtonPrompt.DrawCanvas(canvas, placed.Run.Text, size, runBounds, color.A / 255f, family);
                    }
                    else
                    {
                        canvas.Text(placed.Run.Text, size, runBounds, color, family);
                    }
                }
                y += measurer.LineHeight;
            }
        }
        finally
        {
            canvas.PopClip();
        }
    }
}
