using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal readonly record struct BattleHudPartyRow(float Top, float Height, float VitalHeight, float StatusOffset, float StatusTextScale, float GaugeOffset);

internal readonly record struct BattleHudPartyContent(string Name, string CurrentHp, string MaximumHp, float StatusWidth);

internal readonly record struct BattleHudPartyList(UiRect Panel, int StartRow, int VisibleRows, int Count, float ColumnWidth, float VitalLineHeight, float StatusHeight, float FontSize, float GaugeWidth, bool StackedVitals)
{
    private const int RowsPerColumn = 6;
    private const float ColumnGap = 14;
    private const float Padding = 12;
    private const float FooterHeight = 28;

    public int Rows => Math.Min(Count, RowsPerColumn);
    public int EndRow => StartRow + VisibleRows;
    public bool Scrolls => VisibleRows < Rows;
    public bool TwoColumns => Count > RowsPerColumn;
    public float VitalHeight => VitalLineHeight * (StackedVitals ? 2 : 1);
    public UiRect PreviousPage => new(Panel.X + Padding, Panel.Bottom - FooterHeight - 4, 44, FooterHeight);
    public UiRect NextPage => new(Panel.Right - Padding - 44, PreviousPage.Y, 44, FooterHeight);

    public IEnumerable<int> VisibleIndices()
    {
        for (int index = 0; index < Count; index++)
        {
            if (index % RowsPerColumn >= StartRow && index % RowsPerColumn < EndRow)
            {
                yield return index;
            }
        }
    }

    public UiRect Row(int index)
    {
        float height = VitalHeight + StatusHeight;
        float x = Panel.X + index / RowsPerColumn * (ColumnWidth + ColumnGap);
        return new(x, Panel.Y + Padding + (index % RowsPerColumn - StartRow) * height, ColumnWidth, height);
    }

    public static BattleHudPartyList Measure(IUiCanvas canvas, BattleHudLayout layout, IReadOnlyList<BattleHudPartyContent> content, float textScale, int previousRow, int selectedIndex)
    {
        const float maximumHeight = 264;
        const float bottom = 660;
        int columns = content.Count > RowsPerColumn ? 2 : 1;
        int rows = Math.Min(content.Count, RowsPerColumn);
        float fontSize = 18 * textScale;
        float lineHeight = Math.Max(22, canvas.MeasureText("Mg", fontSize * layout.Scale, float.PositiveInfinity).Height / layout.Scale + 2);
        float statusHeight = Math.Max(12, canvas.MeasureText("Mg", 11 * textScale * layout.Scale, float.PositiveInfinity).Height / layout.Scale);
        float nameWidth = content.Max(member => Measure(member.Name));
        float hpWidth = content.Max(member => Measure(member.CurrentHp) + Measure(member.MaximumHp)) + 4;
        float statusWidth = content.Max(member => member.StatusWidth) + 16;
        float gaugeWidth = columns == 2 ? 66 : 104;
        float minimumWidth = columns == 2 ? 260 : 380;
        float maximumWidth = columns == 2 ? 310 : 438;
        float columnWidth = Math.Clamp(Math.Max(nameWidth + hpWidth + gaugeWidth + 50, statusWidth), minimumWidth, maximumWidth);
        bool stacked = hpWidth + gaugeWidth + 50 + Math.Min(nameWidth, fontSize * 2) > columnWidth;
        float rowHeight = lineHeight * (stacked ? 2 : 1) + statusHeight;
        int visibleRows = Math.Clamp((int)((maximumHeight - 2 * Padding) / rowHeight), 1, rows);
        if (visibleRows < rows)
        {
            visibleRows = Math.Max(1, (int)((maximumHeight - 2 * Padding - FooterHeight) / rowHeight));
        }
        int start = BattleHudGridWindow.Reveal(previousRow, rows, visibleRows, selectedIndex < 0 ? -1 : selectedIndex % RowsPerColumn);
        float width = columns * columnWidth + (columns - 1) * ColumnGap;
        float height = 2 * Padding + visibleRows * rowHeight + (visibleRows < rows ? FooterHeight : 0);
        return new(new(layout.ViewportRight - 26 - width, bottom - height, width, height), start, visibleRows, content.Count, columnWidth, lineHeight, statusHeight, fontSize, gaugeWidth, stacked);

        float Measure(string text) => canvas.MeasureText(text, fontSize * layout.Scale, float.PositiveInfinity).Width / layout.Scale;
    }
}

internal static class BattleHudGridWindow
{
    public static int Reveal(int previous, int count, int capacity, int selected)
    {
        int limit = Math.Max(0, count - capacity);
        int start = Math.Clamp(previous, 0, limit);
        return selected < 0 ? start : Math.Clamp(start, Math.Max(0, selected - capacity + 1), Math.Min(selected, limit));
    }
}

internal readonly record struct BattleHudTargetList(UiRect Panel, BattleDrawLayout Grid, float ColumnWidth, float RowHeight, float ContentOffset, float StatusHeight, int StartRow, int VisibleRows)
{
    public bool Expanded => Grid.Count > 4;
    public int ContentRows => Grid.ColumnRows(0);
    public bool Scrolls => VisibleRows < ContentRows;
    public int EndRow => StartRow + VisibleRows;
    public UiRect Caption => new(Panel.X, Panel.Y - 12, Panel.Width, ContentOffset + 12);
    public UiRect PreviousPage => new(Panel.X + 12, Panel.Bottom - 29, 36, 24);
    public UiRect NextPage => new(Panel.Right - 48, Panel.Bottom - 29, 36, 24);

    public IEnumerable<int> VisibleIndices()
    {
        for (int column = 0; column < Grid.Columns; column++)
        {
            for (int row = StartRow; row < Math.Min(EndRow, Grid.ColumnRows(column)); row++)
            {
                yield return Grid.GetIndex(column, row);
            }
        }
    }

    public UiRect Cell(int index)
    {
        (int column, int row) = Grid.GetCell(index);
        return new(Panel.X + column * ColumnWidth, Panel.Y + ContentOffset + (row - StartRow) * RowHeight, ColumnWidth, RowHeight);
    }

    public int HitIndex(BattleHudLayout viewport, float x, float y)
    {
        UiRect content = viewport.Rect(Panel.X, Panel.Y + ContentOffset, Panel.Width, VisibleRows * RowHeight);
        if (!content.Contains(x, y))
        {
            return -1;
        }
        int column = (int)((x - content.X) / (viewport.Scale * ColumnWidth));
        int row = (int)((y - content.Y) / (viewport.Scale * RowHeight));
        return row < VisibleRows ? Grid.GetIndex(column, StartRow + row) : -1;
    }
}

internal readonly record struct BattleHudLayout(UiRect Bounds)
{
    public const float CompactTargetStatusOffset = 25;
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

    public float ViewportRight => (Bounds.Right - Left) / Scale;

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
        const float height = 3 * PartyRowHeight / 4;
        return new(PartyTop + index * height, height, 18, 18, 1, 5);
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

    public BattleHudTargetList MeasureTargetList(IUiCanvas canvas, UiTheme theme, string[] names, float textScale, float helpSpace, float maximumWidth, int previousRow, int selectedIndex, BattleTargetResistanceRow[]? hintRows = null)
    {
        BattleDrawLayout grid = BattleDrawLayout.Calculate(names.Length);
        float scale = Scale;
        float fontSize = 24 * scale * textScale;
        if (names.Length <= 4)
        {
            var (panel, width, rowHeight, offset) = MeasureTargets(canvas, theme, names, 1, textScale);
            float fitted = Math.Min(width, maximumWidth);
            ArrangeHints(fitted - 42, CompactTargetStatusOffset);
            return new(panel with { Width = fitted }, grid, fitted, rowHeight, offset, 0, 0, grid.Rows);
        }

        const float bottom = 660;
        const float top = 108;
        const float footerHeight = 34;
        float captionSize = UiTheme.DefaultCaptionFontSize * scale * textScale;
        float naturalWidth = names.Max(name => canvas.MeasureText(name, fontSize, float.PositiveInfinity).Width / scale);
        float columnWidth = Math.Min(Math.Max(158, naturalWidth + 42), maximumWidth / grid.Columns);
        float contentOffset = Math.Max(27, Math.Max(theme.CaptionContentHeight(captionSize), captionSize * 0.75f) / scale);
        float statusHeight = Math.Max(12, canvas.MeasureText("Mg", 11 * scale * textScale, float.PositiveInfinity).Height / scale);
        float textWidth = columnWidth - 42;
        float available = bottom - top - helpSpace - contentOffset;
        float contentHeight = Math.Max(canvas.MeasureText("Mg", fontSize, float.PositiveInfinity).Height, names.Max(name => canvas.MeasureText(name, fontSize, textWidth * scale).Height)) / scale;
        float expandedRowHeight = Math.Max(39, contentHeight + statusHeight + 4);
        ArrangeHints(textWidth, expandedRowHeight - statusHeight);
        int visibleRows = Math.Clamp((int)((available - 12) / expandedRowHeight), 1, grid.Rows);
        if (visibleRows < grid.ColumnRows(0))
        {
            visibleRows = Math.Max(1, (int)((available - footerHeight) / expandedRowHeight));
        }
        int selectedRow = selectedIndex < 0 ? -1 : grid.GetCell(selectedIndex).Row;
        int start = BattleHudGridWindow.Reveal(previousRow, grid.ColumnRows(0), visibleRows, selectedRow);
        float height = contentOffset + visibleRows * expandedRowHeight + (visibleRows < grid.ColumnRows(0) ? footerHeight : 12);
        return new(new UiRect(54, bottom - height, columnWidth * grid.Columns, height), grid, columnWidth, expandedRowHeight, contentOffset, statusHeight, start, visibleRows);

        void ArrangeHints(float width, float height)
        {
            if (hintRows is not null)
            {
                BattleTargetResistanceRow.Arrange(canvas, hintRows, names, fontSize, scale, width, height);
            }
        }
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
