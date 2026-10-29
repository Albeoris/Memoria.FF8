using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal interface INativeButtonTextMetrics
{
    bool UsesNativeButtonMetrics { get; }
    float NativeButtonAdvance(string value, float size);
    UiSize NativeButtonMetrics(string value, float size);
}

/// <summary>Preserves input prompts alongside ordinary UI text until layout and drawing.</summary>
internal static class ButtonPromptText
{
    private const char Open = '\uF000';
    private const char Close = '\uF001';

    internal static string Mark(string label)
    {
        return label.Length == 0 || Contains(label) ? label : LocalizationTextComposition.Concat(Open.ToString(), label, Close.ToString());
    }

    internal static string MarkController(Microsoft.Xna.Framework.Input.Buttons button, string label, ControllerPresentation presentation = ControllerPresentation.Generic)
    {
        int? id = button switch
        {
            Microsoft.Xna.Framework.Input.Buttons.LeftTrigger => 128,
            Microsoft.Xna.Framework.Input.Buttons.RightTrigger => 129,
            Microsoft.Xna.Framework.Input.Buttons.LeftShoulder => 130,
            Microsoft.Xna.Framework.Input.Buttons.RightShoulder => 131,
            Microsoft.Xna.Framework.Input.Buttons.Y => 132,
            Microsoft.Xna.Framework.Input.Buttons.B => 133,
            Microsoft.Xna.Framework.Input.Buttons.A => 134,
            Microsoft.Xna.Framework.Input.Buttons.X => 135,
            Microsoft.Xna.Framework.Input.Buttons.Back => 136,
            Microsoft.Xna.Framework.Input.Buttons.Start => 139,
            Microsoft.Xna.Framework.Input.Buttons.LeftStick => 137,
            Microsoft.Xna.Framework.Input.Buttons.RightStick => 138,
            Microsoft.Xna.Framework.Input.Buttons.DPadUp => 140,
            Microsoft.Xna.Framework.Input.Buttons.DPadDown => 141,
            Microsoft.Xna.Framework.Input.Buttons.DPadLeft => 142,
            Microsoft.Xna.Framework.Input.Buttons.DPadRight => 143,
            _ => null
        };
        return Mark(id is int icon ? LocalizationTextComposition.Concat(((char)('\uF100' + icon - 128)).ToString(), ((char)('\uF180' + (int)presentation)).ToString(), label) : label);
    }
    internal static bool Contains(string value)
    {
        return value.Contains(Open) || value.Contains(Close);
    }

    internal static string PlainText(string value)
    {
        bool Printable(char character) => character != Open && character != Close && character is not (>= '\uF100' and <= '\uF10F') && character is not (>= '\uF180' and <= '\uF184');
        if (!LocalizationTextTrace.Enabled || LocalizationTextTrace.Find(value) is null)
        {
            return new string(value.Where(Printable).ToArray());
        }
        List<string> pieces = [];
        int start = 0;
        for (int position = 0; position <= value.Length; position++)
        {
            if (position < value.Length && Printable(value[position]))
            {
                continue;
            }
            if (position > start)
            {
                pieces.Add(LocalizationTextComposition.Slice(value, start, position - start));
            }
            start = position + 1;
        }
        return LocalizationTextComposition.Concat(pieces.ToArray());
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
                string remainder = PlainText(LocalizationTextComposition.Slice(value, cursor, value.Length - cursor));
                if (remainder.Length > 0)
                {
                    yield return new DialogueRun(remainder, Color.White);
                }
                yield break;
            }
            if (open > cursor)
            {
                yield return new DialogueRun(PlainText(LocalizationTextComposition.Slice(value, cursor, open - cursor)), Color.White);
            }
            if (close > open + 1)
            {
                yield return new DialogueRun(LocalizationTextComposition.Slice(value, open + 1, close - open - 1), Color.White, IsButton: true);
            }
            cursor = close + 1;
        }
    }

    internal static DialogueVisualLine[] Lines(string value, float size, float width, string? family, Func<string, float>? measure = null, float? lineHeight = null)
    {
        using SystemTextMeasurer? measurer = measure is null || lineHeight is null ? SystemTextLayout.CreateMeasurer(size, family) : null;
        Func<string, float> actualMeasure = measure ?? (text => measurer!.Measure(text));
        float actualLineHeight = lineHeight ?? measurer!.LineHeight;
        List<DialogueVisualLine> lines = [];
        IReadOnlyList<string> source = SplitLines(value);
        for (int index = 0; index < source.Count; index++)
        {
            lines.AddRange(DialogueLayout.Wrap(new DialogueLine(Runs(source[index]).ToArray(), index), Math.Max(1, width), actualMeasure, false, actualLineHeight));
        }
        return lines.ToArray();
    }

    internal static IReadOnlyList<string> SplitLines(string value)
    {
        List<string> source = [];
        int cursor = 0;
        for (int position = 0; position < value.Length; position++)
        {
            if (value[position] is not ('\r' or '\n'))
            {
                continue;
            }
            source.Add(LocalizationTextComposition.Slice(value, cursor, position - cursor));
            if (value[position] == '\r' && position + 1 < value.Length && value[position + 1] == '\n')
            {
                position++;
            }
            cursor = position + 1;
        }
        source.Add(LocalizationTextComposition.Slice(value, cursor, value.Length - cursor));
        return source;
    }

    internal static UiSize Measure(string value, float size, float width, string? family, Func<string, float>? measure = null, float? lineHeight = null, Func<string, float>? paintWidth = null)
    {
        if (value.Length == 0 || size <= 0)
        {
            return new UiSize(0, 0);
        }
        DialogueVisualLine[] lines = Lines(value, size, width, family, measure, lineHeight);
        float measuredWidth = lines.Select(line => paintWidth is null ? line.Width : Math.Max(line.Width, line.Runs.Where(run => !run.Run.IsButton).Select(run => run.X + paintWidth(run.Run.Text)).DefaultIfEmpty(0).Max())).DefaultIfEmpty(0).Max();
        return new UiSize(measuredWidth, lines.Length * (lineHeight ?? SystemTextLayout.LineHeight(size, family)));
    }

    internal static void Draw(IUiCanvas canvas, string value, float size, UiRect bounds, UiColor color, string? family, UiAlignment alignment, UiTextOverflow overflow, UiTextFlow flow = UiTextFlow.Auto)
    {
        if (value.Length == 0 || size <= 0 || bounds.IsEmpty)
        {
            return;
        }
        INativeButtonTextMetrics? native = canvas is INativeButtonTextMetrics { UsesNativeButtonMetrics: true } metrics ? metrics : null;
        Func<string, float>? nativeMeasure = native is null ? null : text => native.NativeButtonAdvance(text, size);
        float? nativeHeight = native is null ? null : native.NativeButtonMetrics("", size).Height;
        UiTextViewport viewport = overflow.Layout(value, size, bounds, family, width => Measure(value, size, width, family, nativeMeasure, nativeHeight, native is null ? null : text => native.NativeButtonMetrics(text, size).Width), flow);
        DialogueVisualLine[] lines = Lines(value, size, viewport.WrapWidth, family, nativeMeasure, nativeHeight);
        using SystemTextMeasurer? measurer = native is null ? SystemTextLayout.CreateMeasurer(size, family) : null;
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
                    float lineHeight = nativeHeight ?? measurer!.LineHeight;
                    float width = DialogueLayout.MeasureRun(placed.Run, nativeMeasure ?? (text => measurer!.Measure(text)), lineHeight);
                    float paintWidth = native is not null && !placed.Run.IsButton ? Math.Max(width, native.NativeButtonMetrics(placed.Run.Text, size).Width) : width;
                    UiRect runBounds = new(x, y, Math.Max(1, MathF.Ceiling(paintWidth) + 2), lineHeight);
                    if (placed.Run.IsButton)
                    {
                        if (canvas is IUiTextFragmentInspector inspector)
                        {
                            inspector.InspectTextFragment(placed.Run.Text, runBounds, size * 0.82f, family);
                        }
                        ButtonPrompt.DrawCanvas(canvas, placed.Run.Text, size, runBounds, color.A / 255f, family);
                    }
                    else
                    {
                        canvas.Text(placed.Run.Text, size, runBounds, color, family);
                    }
                }
                y += nativeHeight ?? measurer!.LineHeight;
            }
        }
        finally
        {
            canvas.PopClip();
        }
    }
}
