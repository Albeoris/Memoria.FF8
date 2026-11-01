using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private static readonly UiColor JunctionIncreaseColor = new(110, 220, 135);
    private static readonly UiColor JunctionDecreaseColor = new(245, 90, 90);

    private sealed class JunctionLockIcon : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            UiColor gray = new(128, 128, 128);
            float scale = Math.Min(1, Math.Min(Bounds.Width / 14, Bounds.Height / 19));
            float x = Bounds.X + 3;
            float y = Bounds.Y + (Bounds.Height - 18 * scale) / 2;
            canvas.Line(x + 3 * scale, y + 8 * scale, x + 3 * scale, y + 3 * scale, gray, 2 * scale);
            canvas.Line(x + 3 * scale, y + 3 * scale, x + 5 * scale, y + scale, gray, 2 * scale);
            canvas.Line(x + 5 * scale, y + scale, x + 9 * scale, y + scale, gray, 2 * scale);
            canvas.Line(x + 9 * scale, y + scale, x + 11 * scale, y + 3 * scale, gray, 2 * scale);
            canvas.Line(x + 11 * scale, y + 3 * scale, x + 11 * scale, y + 8 * scale, gray, 2 * scale);
            canvas.Fill(new UiRect(x, y + 7 * scale, 14 * scale, 11 * scale), gray);
        }
    }
}
