using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.GameUi;

internal interface IBattleMessageCanvas
{
    void BattleMessage(DialogueDocument document, float size, UiRect bounds, double seconds);
}

internal sealed partial class MonoGameUiCanvas
{
    private sealed record BattleMessageLayout(DialogueVisualLine[] Lines, float LineHeight);
    private readonly Dictionary<(DialogueDocument Document, float Size, float Width, string Family), BattleMessageLayout> battleMessageLayouts = [];

    public void BattleMessage(DialogueDocument document, float size, UiRect bounds, double seconds)
    {
        if (document.Binding is { } binding && InspectText is not null)
        {
            Rectangle visible = Rectangle.Intersect(RectangleFor(bounds), CurrentClip);
            if (visible.Width > 0 && visible.Height > 0)
            {
                InspectText(binding, new UiRect(visible.X, visible.Y, visible.Width, visible.Height));
            }
        }
        if (UseNativeFonts)
        {
            DrawNativeBattleMessage(document, size, bounds, seconds);
            return;
        }
        var key = (document, size, bounds.Width, text.FontFamily);
        if (!battleMessageLayouts.TryGetValue(key, out BattleMessageLayout? layout))
        {
            using SystemTextMeasurer measure = new(size, text.FontFamily);
            layout = new(document.Pages.SelectMany(page => page.Lines).SelectMany(line => DialogueLayout.Wrap(line, bounds.Width, measure.Measure, false, measure.LineHeight)).ToArray(), measure.LineHeight);
            if (battleMessageLayouts.Count >= 32)
            {
                battleMessageLayouts.Clear();
            }
            battleMessageLayouts.Add(key, layout);
        }
        Rectangle clip = Rectangle.Intersect(RectangleFor(bounds), CurrentClip);
        float y = bounds.Y + Math.Max(0, (bounds.Height - layout.Lines.Length * layout.LineHeight) / 2);
        foreach (DialogueVisualLine line in layout.Lines)
        {
            if (y >= bounds.Bottom)
            {
                break;
            }
            foreach (DialoguePlacedRun placed in line.Runs)
            {
                Color color = placed.Run.Color * (placed.Run.Blink ? DialogueBlink.Opacity(seconds) : 1);
                float x = Math.Max(0, (bounds.Width - line.Width) / 2) + placed.X;
                if (placed.Run.IsButton)
                {
                    ButtonPrompt.Draw(batch, pixel, text, placed.Run.Text, size, new Vector2(bounds.X + x, y), layout.LineHeight, color.A / 255f, text.FontFamily, clip, this);
                }
                else
                {
                    text.DrawWrappedClipped(batch, placed.Run.Text, size, new Vector2(bounds.X + x, y), Math.Max(1, bounds.Width - x), color, clip);
                }
            }
            y += layout.LineHeight;
        }
    }

    private void DrawNativeBattleMessage(DialogueDocument document, float size, UiRect bounds, double seconds)
    {
        float y = bounds.Y;
        float lineHeight = size;
        foreach (DialogueLine line in document.Pages.FirstOrDefault()?.Lines ?? [])
        {
            float x = bounds.X;
            foreach (DialogueRun run in line.Runs)
            {
                Color color = run.Color * (run.Blink ? DialogueBlink.Opacity(seconds) : 1);
                UiRect runBounds = new(x, y, Math.Max(1, bounds.Right - x), Math.Max(1, bounds.Bottom - y));
                if (run.IsButton)
                {
                    ButtonPrompt.Draw(batch, pixel, text, run.Text, size, new Vector2(x, y), lineHeight, color.A / 255f, text.FontFamily, Rectangle.Intersect(RectangleFor(bounds), CurrentClip), this);
                }
                else
                {
                    DrawNativeText(run.Text, size, runBounds, new UiColor(color.R, color.G, color.B, color.A), UiAlignment.Start, NativeBattleTextContext);
                }
                x += NativeTextAdvance(run.Text, size, NativeBattleTextContext);
            }
            y += lineHeight;
        }
    }
}
