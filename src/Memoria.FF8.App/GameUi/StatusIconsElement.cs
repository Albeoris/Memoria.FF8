using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal sealed class StatusIconsElement(uint statuses, float size) : UiElement
{
    internal IReadOnlyList<UiImageSource> Images { get; } = SavedStatusArtwork.Images(statuses);

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        float x = Bounds.X;
        float y = Bounds.Y;
        foreach (UiImageSource image in Images)
        {
            if (x + size > Bounds.Right && x > Bounds.X)
            {
                x = Bounds.X;
                y += size + 4;
            }
            if (y + size > Bounds.Bottom)
            {
                break;
            }
            canvas.Image(image, new UiRect(x, y, size, size));
            x += size + 4;
        }
    }
}
