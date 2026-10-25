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
}
