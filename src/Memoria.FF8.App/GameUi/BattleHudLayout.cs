using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal readonly record struct BattleHudLayout(UiRect Bounds)
{
    public const float CommandX = 30;
    public const float CommandRowHeight = 28;
    public const int VisibleCommandRows = 4;
    public const float PartyTop = 528;
    public const float PartyRowHeight = 38;

    public float Scale => Math.Min(Bounds.Width / 960, Bounds.Height / 672);
    public float Left => Bounds.X + (Bounds.Width - 960 * Scale) / 2;
    public float Top => Bounds.Y + (Bounds.Height - 672 * Scale) / 2;

    public UiRect Rect(float x, float y, float width, float height)
    {
        return new(Left + x * Scale, Top + y * Scale, width * Scale, height * Scale);
    }

    public UiRect MessagePanel => Rect(240, 12, 480, 84);
    public UiRect MessageText => Rect(256, 24, 448, 60);

    public UiRect ResultRect(float x, float y, float width, float height)
    {
        return Rect(x * 2.5f, y * 3, width * 2.5f, height * 3);
    }

    public static int CommandStart(int selected, int count)
    {
        return Math.Clamp(selected - 1, 0, Math.Max(0, count - VisibleCommandRows));
    }

    public static UiRect CommandPanel(float width, int rows, float captionContentHeight, float extraHeight = 0)
    {
        float height = 20 + Math.Max(1, Math.Min(VisibleCommandRows, rows)) * CommandRowHeight + Math.Max(0, captionContentHeight - 14) + extraHeight;
        return new(CommandX, Math.Min(516, 660 - height), width, height);
    }

    public (UiRect Panel, float ColumnWidth, float RowHeight, float ContentOffset) MeasureTargets(IUiCanvas canvas, UiTheme theme, IEnumerable<string> names, int columns, float textScale)
    {
        float scale = Scale;
        float fontSize = 24 * scale * textScale;
        float nameWidth = names.Select(name => canvas.MeasureText(name, fontSize, 4096 * scale).Width / scale).DefaultIfEmpty(0).Max();
        float captionSize = UiTheme.DefaultCaptionFontSize * scale * textScale;
        float columnWidth = Math.Min(Math.Max(324, 6 * MathF.Floor((nameWidth + 54) / 6)), 442f / columns);
        float rowHeight = Math.Max(39, canvas.MeasureText("Mg", fontSize, float.PositiveInfinity).Height / scale + 6);
        float contentOffset = Math.Max(27, Math.Max(theme.CaptionContentHeight(captionSize), captionSize * 0.75f) / scale);
        float height = 4 * rowHeight + contentOffset + 12;
        float y = Math.Min(609 - 4 * rowHeight, 660 - height);
        return (new UiRect(54, y, columnWidth * columns, height), columnWidth, rowHeight, contentOffset);
    }

    public void DrawVitals(IUiCanvas canvas, string name, long hp, long maximumHp, float y, float textScale, UiColor nameColor, UiColor hpColor, int? mp = null, int maximumMp = 0)
    {
        float fontSize = 22 * Scale * textScale;
        UiColor white = new(255, 255, 255);
        canvas.Text(name, fontSize, Rect(520, y, 150, 30), nameColor);
        canvas.AlignedText(hp.ToString(), fontSize, Rect(674, y, 56, 30), hpColor, UiAlignment.End);
        canvas.Text("/", fontSize, Rect(738, y, 14, 30), white);
        canvas.Text(maximumHp.ToString(), fontSize, Rect(760, y, 64, 30), white);
        if (mp is int currentMp)
        {
            canvas.AlignedText($"MP {currentMp}/{maximumMp}", Math.Min(13 * textScale, 15) * Scale, Rect(674, y + 23, 150, 15), new UiColor(120, 227, 255), UiAlignment.End);
        }
    }

    public UiRect AtbGauge(float rowY) => Rect(830, rowY + 11, 104, 12);

    public void DrawCommandPointer(IUiCanvas canvas, UiTheme theme, float columnX, float rowY)
    {
        theme.Pointer(canvas, Left + (columnX + 8) * Scale, Top + (rowY + 5) * Scale, 10 * Scale);
    }

    public static UiColor Modulate(UiColor color, int shade)
    {
        return new((byte)Math.Min(255, color.R * shade / 128), (byte)Math.Min(255, color.G * shade / 128), (byte)Math.Min(255, color.B * shade / 128), color.A);
    }
}
