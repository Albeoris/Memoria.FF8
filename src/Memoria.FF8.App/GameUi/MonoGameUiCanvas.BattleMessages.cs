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
            layout = new(document.Pages.SelectMany(page => page.Lines).SelectMany(line => DialogueLayout.Wrap(line, bounds.Width, measure.Measure, false)).ToArray(), measure.LineHeight);
            if (battleMessageLayouts.Count >= 32)
            {
                battleMessageLayouts.Clear();
            }
            battleMessageLayouts.Add(key, layout);
        }
        Rectangle clip = Rectangle.Intersect(RectangleFor(bounds), CurrentClip);
        float y = bounds.Y;
        foreach (DialogueVisualLine line in layout.Lines)
        {
            if (y >= bounds.Bottom)
            {
                break;
            }
            foreach (DialoguePlacedRun placed in line.Runs)
            {
                Color color = placed.Run.Color * (placed.Run.Blink ? DialogueBlink.Opacity(seconds) : 1);
                text.DrawWrappedClipped(batch, placed.Run.Text, size, new Vector2(bounds.X + placed.X, y), Math.Max(1, bounds.Width - placed.X), color, clip);
            }
            y += layout.LineHeight;
        }
    }
}
