using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Places original page objects in mmag coordinates; only the paper is procedural.</summary>
internal sealed class MagazinePageElement(NativeMagazineLayout layout, string? fontFamily) : UiElement
{
    public override void Arrange(UiRect bounds)
    {
        Bounds = bounds;
        Children.Clear();
        float scaleX = bounds.Width / layout.Width;
        float scaleY = bounds.Height / layout.Height;
        UiRect Map(float x, float y, float width, float height)
        {
            return new UiRect(bounds.X + x * scaleX, bounds.Y + y * scaleY, width * scaleX, height * scaleY);
        }
        foreach (NativeMagazineImage image in layout.Images)
        {
            UiImage artwork = new() { Id = $"magazine-image-{image.ImageId}", Source = new UiImageSource(UiImageKind.Magazine, image.ImageId) };
            artwork.Arrange(Map(image.X, image.Y, image.Width, image.Height));
            Children.Add(artwork);
        }
        foreach (NativeMagazineIcon icon in layout.Icons)
        {
            UiImage artwork = new() { Source = new UiImageSource(UiImageKind.Icon, icon.IconId, icon.Palette) };
            artwork.Arrange(Map(icon.X, icon.Y, 12, 12));
            Children.Add(artwork);
        }
        foreach (NativeMagazineButton button in layout.Buttons)
        {
            UiImage artwork = new() { Source = new UiImageSource(UiImageKind.MagazineButton, button.ButtonBit) };
            artwork.Arrange(Map(button.X, button.Y, 12, 12));
            Children.Add(artwork);
        }
        for (int index = 0; index < layout.Texts.Length; index++)
        {
            NativeMagazineText text = layout.Texts[index];
            string[] lines = text.Text.Replace("\r", "").Split('\n').Select(line => line.TrimEnd()).ToArray();
            float size = text.FontSize * scaleY;
            float[] widths = new float[lines.Length];
            if (text.RightAligned)
            {
                for (int line = 0; line < lines.Length; line++)
                {
                    float advance = SystemTextLayout.MeasureAdvance(lines[line], size, fontFamily);
                    MagazineLine number = new(lines[line], size, fontFamily) { Id = $"magazine-text-{index}-{line}" };
                    number.Arrange(new UiRect(bounds.X + text.X * scaleX - advance, bounds.Y + (text.Y + line * text.LineHeight) * scaleY, advance + 2, text.LineHeight * scaleY));
                    Children.Add(number);
                }
                continue;
            }
            for (int line = 0; line < lines.Length; line++)
            {
                float y = text.Y + line * text.LineHeight;
                float right = layout.Width - 4;
                foreach (NativeMagazineImage image in layout.Images)
                {
                    if (image.X > text.X && y < image.Y + image.Height && y + text.FontSize > image.Y)
                    {
                        right = Math.Min(right, image.X - 4);
                    }
                }
                foreach (NativeMagazineText other in layout.Texts)
                {
                    int lineCount = other.Text.Count(character => character == '\n') + 1;
                    if (other.X > text.X && y < other.Y + lineCount * other.LineHeight && y + text.FontSize > other.Y)
                    {
                        float otherLeft = other.X;
                        if (other.RightAligned)
                        {
                            otherLeft -= SystemTextLayout.MeasureAdvance(other.Text, other.FontSize * scaleY, fontFamily) / scaleX;
                        }
                        right = Math.Min(right, otherLeft - 2);
                    }
                }
                widths[line] = Math.Max(1, right - text.X) * scaleX;
                float advance = SystemTextLayout.MeasureAdvance(lines[line], text.FontSize * scaleY, fontFamily);
                float available = Math.Max(1, widths[line] - 2);
                if (advance > available)
                {
                    size = Math.Min(size, text.FontSize * scaleY * available / advance * 0.99f);
                }
            }
            for (int line = 0; line < lines.Length; line++)
            {
                if (string.IsNullOrEmpty(lines[line]))
                {
                    continue;
                }
                MagazineLine label = new(lines[line], size, fontFamily) { Id = $"magazine-text-{index}-{line}" };
                label.Arrange(new UiRect(bounds.X + text.X * scaleX, bounds.Y + (text.Y + line * text.LineHeight) * scaleY, widths[line], text.LineHeight * scaleY));
                Children.Add(label);
            }
        }
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        canvas.Paper(Bounds, new UiColor(layout.PaperColor.Red, layout.PaperColor.Green, layout.PaperColor.Blue));
        if (layout.Tint is { } tint)
        {
            UiRect overlay = new(Bounds.X + tint.X * Bounds.Width / layout.Width, Bounds.Y + tint.Y * Bounds.Height / layout.Height, tint.Width * Bounds.Width / layout.Width, tint.Height * Bounds.Height / layout.Height);
            canvas.Fill(overlay.Intersect(Bounds), new UiColor(tint.Red, tint.Green, tint.Blue, 128));
        }
        canvas.Line(Bounds.X, Bounds.Y, Bounds.Right - 1, Bounds.Y, theme.BorderLight, 2);
        canvas.Line(Bounds.X, Bounds.Y, Bounds.X, Bounds.Bottom - 1, theme.BorderLight, 2);
        canvas.Line(Bounds.X, Bounds.Bottom - 2, Bounds.Right - 1, Bounds.Bottom - 2, theme.BorderDark, 2);
        canvas.Line(Bounds.Right - 2, Bounds.Y, Bounds.Right - 2, Bounds.Bottom - 1, theme.BorderDark, 2);
    }

    private sealed class MagazineLine(string text, float fontSize, string? fontFamily) : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            canvas.Text(text, fontSize, Bounds, theme.Foreground, fontFamily);
        }
    }
}
