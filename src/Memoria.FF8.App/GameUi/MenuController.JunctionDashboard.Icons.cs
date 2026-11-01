using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private static readonly UiColor JunctionIncreaseColor = new(110, 220, 135);
    private static readonly UiColor JunctionDecreaseColor = new(245, 90, 90);

    private UiElement JunctionEffectLabel(bool elemental, int index, string name, float size)
    {
        int count = elemental ? 8 : 13;
        if ((uint)index >= (uint)count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        UiGrid identity = new() { Gap = 3 };
        identity.RowDefinitions.Add(UiGridLength.Star());
        identity.ColumnDefinitions.Add(UiGridLength.Pixel(18 * TextScale));
        identity.ColumnDefinitions.Add(UiGridLength.Star());
        UiImage icon = new()
        {
            Id = (elemental ? "elemental" : "status") + "-icon-" + index,
            Source = new UiImageSource(UiImageKind.Icon, (elemental ? 288 : 272) + index)
        };
        Place(identity, icon, 0);
        Place(identity, JunctionLabel(name, size), 0, 1);
        return identity;
    }

    private sealed class JunctionLockIcon : UiElement
    {
        protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
        {
            UiColor gray = new(128, 128, 128);
            float scale = Math.Min(1, Math.Min(Bounds.Width / 16, Bounds.Height / 20));
            float x = Bounds.X + Math.Min(3, Math.Max(0, Bounds.Width - 16 * scale));
            float y = Bounds.Y + (Bounds.Height - 20 * scale) / 2;
            canvas.Line(x + 4 * scale, y + 9 * scale, x + 4 * scale, y + 5 * scale, gray, 2 * scale);
            canvas.Line(x + 12 * scale, y + 9 * scale, x + 12 * scale, y + 5 * scale, gray, 2 * scale);
            for (int segment = 0; segment < 12; segment++)
            {
                float start = MathF.PI + segment * MathF.PI / 12;
                float end = MathF.PI + (segment + 1) * MathF.PI / 12;
                canvas.Line(x + (8 + 4 * MathF.Cos(start)) * scale, y + (5 + 4 * MathF.Sin(start)) * scale, x + (8 + 4 * MathF.Cos(end)) * scale, y + (5 + 4 * MathF.Sin(end)) * scale, gray, 2 * scale);
            }
            canvas.Fill(new UiRect(x + 2 * scale, y + 8 * scale, 12 * scale, scale), gray);
            canvas.Fill(new UiRect(x + scale, y + 9 * scale, 14 * scale, 3 * scale), gray);
            canvas.Fill(new UiRect(x + scale, y + 12 * scale, 6 * scale, 4 * scale), gray);
            canvas.Fill(new UiRect(x + 9 * scale, y + 12 * scale, 6 * scale, 4 * scale), gray);
            canvas.Fill(new UiRect(x + scale, y + 16 * scale, 14 * scale, 3 * scale), gray);
            canvas.Fill(new UiRect(x + 2 * scale, y + 19 * scale, 12 * scale, scale), gray);
        }
    }
}
