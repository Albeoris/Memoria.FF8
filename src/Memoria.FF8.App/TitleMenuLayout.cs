using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal sealed record TitleMenuLayout(UiRect[] Choices, UiRect Hint, float ChoiceFontSize, float HintFontSize)
{
    public static TitleMenuLayout Create(UiRect bounds, float textScale)
    {
        if (!float.IsFinite(textScale) || textScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(textScale));
        }
        float top = 0.63f - 0.08f * Math.Clamp(textScale - 1, 0, 1);
        float rowHeight = Math.Min(bounds.Height * 0.074f * Math.Max(1, textScale), bounds.Height * (0.86f - top) / 3);
        UiRect[] choices = Enumerable.Range(0, 3).Select(index => new UiRect(bounds.X + bounds.Width * 0.05f, bounds.Y + bounds.Height * top + rowHeight * index, bounds.Width * 0.9f, rowHeight)).ToArray();
        UiRect hint = new(bounds.X + bounds.Width * 0.03f, bounds.Y + bounds.Height * 0.89f, bounds.Width * 0.94f, bounds.Height * 0.09f);
        return new TitleMenuLayout(choices, hint, Math.Min(bounds.Height * 0.044f, bounds.Width * 0.06f) * textScale, Math.Min(bounds.Height * 0.02f, bounds.Width * 0.024f) * textScale);
    }
}
