using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8.GameUi;

internal sealed partial class MonoGameUiCanvas
{
    private Texture2D? paper;
    private UiColor paperColor;

    public void Paper(UiRect bounds, UiColor color)
    {
        if (bounds.IsEmpty)
        {
            return;
        }
        int width = Math.Max(1, (int)MathF.Ceiling(bounds.Width));
        int height = Math.Max(1, (int)MathF.Ceiling(bounds.Height));
        if (paper is null || paper.Width != width || paper.Height != height || paperColor != color)
        {
            paper?.Dispose();
            paper = new Texture2D(device, width, height);
            paper.SetData(UiPaper.CreatePixels(width, height, color).Select(ColorFor).ToArray());
            paperColor = color;
        }
        Rectangle clip = Rectangle.Intersect(RectangleFor(bounds), CurrentClip);
        SystemTextRenderer.DrawClipped(batch, paper, new Vector2(bounds.X, bounds.Y), Color.White, clip, 1);
    }
}
