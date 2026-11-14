using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal readonly record struct BattleHudPartyRow(float Top, float Height, float VitalHeight, float StatusOffset, float StatusTextScale, float GaugeOffset);

internal readonly record struct BattleHudTargetList(UiRect Panel, float RowHeight, float ContentOffset, int Start, int VisibleCount, int TotalCount)
{
    public bool Scrolls => TotalCount > 4;
    public int End => Start + VisibleCount;
    public UiRect Caption => new(Panel.X, Panel.Y - 12, Panel.Width, ContentOffset + 12);
    public UiRect PreviousPage => new(Panel.X + 12, Panel.Bottom - 29, 36, 24);
    public UiRect NextPage => new(Panel.Right - 48, Panel.Bottom - 29, 36, 24);
    public UiRect Row(int index) => new(Panel.X, Panel.Y + ContentOffset + (index - Start) * RowHeight, Panel.Width, RowHeight);

    public int HitIndex(BattleHudLayout viewport, float x, float y)
    {
        for (int index = Start; index < End; index++)
        {
            UiRect row = Row(index);
            if (viewport.Rect(row.X, row.Y, row.Width, row.Height).Contains(x, y))
            {
                return index;
            }
        }
        return -1;
    }
}

internal readonly record struct BattleHudLayout(UiRect Bounds)
{
    public const float CommandX = 30;
    public const float CommandRowHeight = 28;
    public const int VisibleCommandRows = 4;
    public const float PartyLeft = 496;
    public const float PartyTop = 528;
    public const float PartyRowHeight = 38;
    private const float PartyWidth = 438;

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

    public static BattleHudPartyRow PartyRow(int index, int count, float textScale)
    {
        if (count <= 3)
        {
            return new(PartyTop + index * PartyRowHeight, PartyRowHeight, 30, 25, textScale, 11);
        }
        if (count == 4)
        {
            const float height = 3 * PartyRowHeight / 4;
            return new(PartyTop + index * height, height, 16.5f, 16.5f, 1, 5);
        }
        // Reserve the top of the HUD for the battle message even with eleven rows.
        float rowTextScale = Math.Min(1.65f, textScale);
        float statusHeight = Math.Max(12, 11 * rowTextScale);
        float vitalHeight = Math.Max(26, 20 * rowTextScale);
        float rowHeight = vitalHeight + statusHeight;
        return new(660 - (count - index) * rowHeight, rowHeight, vitalHeight, vitalHeight, rowTextScale, 11);
    }

    public UiRect PartyRowBounds(BattleHudPartyRow row) => Rect(PartyLeft, row.Top, PartyWidth, row.Height);

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

    public BattleHudTargetList MeasureTargetList(IUiCanvas canvas, UiTheme theme, string[] names, float textScale, float helpSpace, int previousStart, int selectedIndex)
    {
        if (names.Length <= 4)
        {
            var (panel, _, nativeRowHeight, offset) = MeasureTargets(canvas, theme, names, 1, textScale);
            return new(panel, nativeRowHeight, offset, 0, names.Length, names.Length);
        }

        const float bottom = 660;
        const float top = 108;
        const float footerHeight = 34;
        float fontSize = 24 * Scale * textScale;
        float captionSize = UiTheme.DefaultCaptionFontSize * Scale * textScale;
        float contentOffset = Math.Max(27, Math.Max(theme.CaptionContentHeight(captionSize), captionSize * 0.75f) / Scale);
        float rowHeight = Math.Max(39, canvas.MeasureText("Mg", fontSize, float.PositiveInfinity).Height / Scale + Math.Max(12, 11 * textScale));
        int visible = Math.Clamp((int)((bottom - top - helpSpace - contentOffset - footerHeight) / rowHeight), 1, Math.Min(7, names.Length));
        int start = Math.Clamp(previousStart, 0, names.Length - visible);
        if (selectedIndex >= 0)
        {
            start = Math.Clamp(start, Math.Max(0, selectedIndex - visible + 1), Math.Min(selectedIndex, names.Length - visible));
        }
        float height = contentOffset + visible * rowHeight + footerHeight;
        return new(new UiRect(54, bottom - height, 442, height), rowHeight, contentOffset, start, visible, names.Length);
    }

    public void DrawVitals(IUiCanvas canvas, string name, long hp, long maximumHp, float y, float textScale, UiColor nameColor, UiColor hpColor, int? mp = null, int maximumMp = 0, float? compactHeight = null)
    {
        float fontSize = compactHeight is float cap ? Math.Min(22 * textScale, cap) * Scale : 22 * Scale * textScale;
        float height = compactHeight ?? 30;
        UiColor white = new(255, 255, 255);
        if (compactHeight.HasValue)
        {
            canvas.Text(name, fontSize, Rect(520, y, 150, height), nameColor, UiAlignment.Start, UiTextFlow.SingleLine);
        }
        else
        {
            canvas.Text(name, fontSize, Rect(520, y, 150, height), nameColor);
        }
        canvas.AlignedText(hp.ToString(), fontSize, Rect(674, y, 56, height), hpColor, UiAlignment.End);
        canvas.Text("/", fontSize, Rect(738, y, 14, height), white);
        canvas.Text(maximumHp.ToString(), fontSize, Rect(760, y, 64, height), white);
        if (mp is int currentMp)
        {
            canvas.AlignedText($"MP {currentMp}/{maximumMp}", Math.Min(13 * textScale, 15) * Scale, Rect(674, y + 23, 150, 15), new UiColor(120, 227, 255), UiAlignment.End);
        }
    }

    public UiRect AtbGauge(float rowY, float offset = 11) => Rect(830, rowY + offset, 104, 12);

    public void DrawCommandPointer(IUiCanvas canvas, UiTheme theme, float columnX, float rowY)
    {
        theme.Pointer(canvas, Left + (columnX + 8) * Scale, Top + (rowY + 5) * Scale, 10 * Scale);
    }

    public static UiColor Modulate(UiColor color, int shade)
    {
        return new((byte)Math.Min(255, color.R * shade / 128), (byte)Math.Min(255, color.G * shade / 128), (byte)Math.Min(255, color.B * shade / 128), color.A);
    }
}
