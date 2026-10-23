using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Slots fronts deliberately contain no collectible-card identity or ranks.</summary>
public static class SlotsCardView
{
    public static UiRect CardBounds(UiRect bounds, int index, float spread)
    {
        float scale = Math.Min(bounds.Width / 960, bounds.Height / 672);
        int cell = SlotsCardController.Cell(index);
        return new UiRect(bounds.X + bounds.Width / 2 + ((cell % 3 - 1) * 152 * spread - 68) * scale, bounds.Y + bounds.Height / 2 + ((cell / 3 - 1) * 179.2f * spread - 81.6f) * scale, 136 * scale, 163.2f * scale);
    }

    public static void Draw(IUiCanvas canvas, UiTheme theme, UiRect bounds, SlotsCardController controller, Func<int, string> name, Func<int, int> repetitions, float textScale, string navigationHint, string confirmHint)
    {
        float scale = Math.Min(bounds.Width / 960, bounds.Height / 672);
        canvas.Fill(bounds, new UiColor(0, 0, 0, (byte)(115 * controller.Opacity)));
        UiRect heading = new(bounds.X, bounds.Y + (bounds.Height - 672 * scale) / 2 + 28 * scale, bounds.Width, 32 * scale);
        CenterText("SLOTS", 25 * scale * textScale, heading, theme.Foreground);
        // Draw the central card last while the group spreads from its compact stack.
        for (int draw = controller.Count - 1; draw >= 0; draw--)
        {
            int index = draw;
            UiRect card = CardBounds(bounds, index, controller.Spread);
            (bool faceUp, float width) = controller.PresentedCard(index);
            UiRect shown = new(card.X + card.Width * (1 - width) / 2, card.Y, card.Width * width, card.Height);
            if (!faceUp)
            {
                canvas.ImageStretched(new UiImageSource(UiImageKind.CardBack, 0), shown);
            }
            else
            {
                theme.Panel(canvas, shown);
                if (width > 0.35f)
                {
                    canvas.PushClip(shown);
                    try
                    {
                        UiRect title = new(shown.X + 8 * scale, shown.Y + shown.Height * 0.30f, Math.Max(1, shown.Width - 16 * scale), 44 * scale);
                        CenterText(name(index), 22 * scale * textScale, title, theme.Foreground);
                        CenterText($"×{repetitions(index)}", 26 * scale * textScale, new UiRect(shown.X + 8 * scale, shown.Y + shown.Height * 0.62f, Math.Max(1, shown.Width - 16 * scale), 36 * scale), theme.Foreground);
                    }
                    finally
                    {
                        canvas.PopClip();
                    }
                }
            }
            if (controller.Ready && controller.FocusIndex == index || controller.SelectedIndex == index)
            {
                theme.Pointer(canvas, card.X - 25 * scale, card.Y + card.Height / 2 - 10 * scale, 20 * scale);
            }
        }
        UiRect hints = new(bounds.X + 20 * scale, bounds.Y + (bounds.Height + 672 * scale) / 2 - 43 * scale, bounds.Width - 40 * scale, 30 * scale);
        CenterText($"{navigationHint}   {confirmHint}: Choose", 18 * scale * textScale, hints, theme.Foreground);
        void CenterText(string value, float size, UiRect area, UiColor color)
        {
            float measured = canvas.MeasureText(value, size, 4096 * scale).Width;
            float fitted = size * Math.Min(1, area.Width / Math.Max(1, measured));
            float width = canvas.MeasureText(value, fitted, 4096 * scale).Width;
            UiRect centered = new(area.X + Math.Max(0, (area.Width - width) / 2), area.Y, Math.Min(area.Width, width + scale), area.Height);
            canvas.Text(value, fitted, centered, color, UiAlignment.Center);
        }
    }
}
