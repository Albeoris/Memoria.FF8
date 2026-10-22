namespace Memoria.FF8.Ui;

public sealed class UiList : UiElement
{
    private int _selectedIndex;
    private int _scrollOffset;
    private float _rowHeight = 28;
    private int _pageSize;
    private float _secondaryWidth = 0.35f;
    private IList<string> _items = new List<string>();
    private RowMeasurement[] _measurements = [];
    private float[] _rowHeights = [];
    private float[] _textHeights = [];
    private bool _hasTextMeasurements;
    private bool _hasMeasuredRows;

    private readonly record struct RowMeasurement(string Text, string? Secondary, float PrimaryWidth, float SecondaryWidth, float FontSize);

    public UiList()
    {
        Focusable = true;
    }

    public IList<string> Items
    {
        get => _items;
        set
        {
            _items = value ?? throw new ArgumentNullException(nameof(value));
            EnsureSelectionVisible();
        }
    }

    public float RowHeight
    {
        get => _rowHeight;
        set
        {
            if (!float.IsFinite(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _rowHeight = value;
            EnsureSelectionVisible();
        }
    }

    public UiOrientation Orientation { get; set; } = UiOrientation.Vertical;
    public bool Paginated { get; set; }
    public bool FitPageRows { get; set; }
    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _pageSize = value;
            EnsureSelectionVisible();
        }
    }

    public int VisibleRows
    {
        get
        {
            if (Orientation == UiOrientation.Horizontal)
            {
                return Math.Max(1, Items.Count);
            }

            if (HasMeasuredRows)
            {
                return CountRows(UsesPagination ? PageStarts()[CurrentPage] : ScrollOffset, true);
            }

            if (UsesPagination)
            {
                return FitPageRows && PageSize > 0 && ContentHeight > 0 ? PageSize : Math.Max(0, (int)Math.Floor(ContentHeight / RowHeight));
            }

            return Math.Max(1, (int)Math.Floor(Math.Max(0, Bounds.Height) / RowHeight));
        }
    }

    /// <summary>The page capacity, limited to full visible rows unless FitPageRows fits the requested PageSize.</summary>
    public int EffectivePageSize => HasMeasuredRows && UsesPagination ? CountRows(PageStarts()[CurrentPage], true) : Math.Max(1, PageSize > 0 ? Math.Min(PageSize, VisibleRows) : VisibleRows);
    public int PageCount => Items.Count == 0 ? 0 : HasMeasuredRows && UsesPagination ? PageStarts().Count : (Items.Count - 1) / EffectivePageSize + 1;
    /// <summary>The zero-based page containing the selection.</summary>
    public int CurrentPage
    {
        get => SelectedIndex < 0 ? 0 : HasMeasuredRows && UsesPagination ? PageStarts().FindLastIndex(start => start <= SelectedIndex) : SelectedIndex / EffectivePageSize;
        set
        {
            int page = Math.Clamp(value, 0, Math.Max(0, PageCount - 1));
            if (HasMeasuredRows && UsesPagination)
            {
                List<int> starts = PageStarts();
                int rowInPage = Math.Max(0, SelectedIndex) - starts[CurrentPage];
                int start = starts[page];
                SelectedIndex = start + Math.Min(rowInPage, CountRows(start, true) - 1);
                return;
            }

            int row = Math.Max(0, SelectedIndex) % EffectivePageSize;
            SelectedIndex = page * EffectivePageSize + row;
        }
    }

    /// <summary>Expands vertical rows to fit the renderer's wrapped text measurements.</summary>
    public bool AutoRowHeight { get; set; } = true;
    public UiTextFlow TextFlow { get; set; } = UiTextFlow.Auto;
    public UiAlignment SecondaryAlignment { get; set; } = UiAlignment.Start;
    /// <summary>Sizes the shared secondary column from the renderer's measurements of all item values.</summary>
    public bool AutoSecondaryWidth { get; set; }
    /// <summary>Places the shared trailing image gutter before the secondary column when that column is present.</summary>
    public bool TrailingImagesBeforeSecondary { get; set; }
    private bool HasMeasuredRows => AutoRowHeight && Orientation == UiOrientation.Vertical && _hasMeasuredRows && _rowHeights.Length == Items.Count;
    private bool UsesPagination => Paginated && Orientation == UiOrientation.Vertical;
    private float FooterHeight => RequiresPagination() ? Math.Min(24, Math.Max(0, Bounds.Height)) : 0;

    private bool RequiresPagination()
    {
        if (!UsesPagination || Items.Count == 0)
        {
            return false;
        }
        if (PageSize > 0 && Items.Count > PageSize)
        {
            return true;
        }

        // Test the full viewport first so the footer cannot create its own extra page.
        float available = Math.Max(0, Bounds.Height);
        double minimumHeight = FitPageRows && PageSize > 0 ? (double)available / PageSize : RowHeight;
        bool measured = AutoRowHeight && _hasTextMeasurements && _textHeights.Length == Items.Count;
        double height = FitPageRows && PageSize > 0 ? (double)available * Items.Count / PageSize : (double)RowHeight * Items.Count;
        for (int index = 0; index < Items.Count; index++)
        {
            height += measured ? Math.Max(0, _textHeights[index] - minimumHeight) : 0;
            if (height > available)
            {
                return true;
            }
        }
        return false;
    }

    private float ContentHeight => Math.Max(0, Bounds.Height - FooterHeight);
    private float RenderedRowHeight => UsesPagination && FitPageRows && PageSize > 0 ? ContentHeight / PageSize : RowHeight;
    private float SinglePageRowHeight => UsesPagination && FitPageRows && PageSize > 0 ? Math.Max(0, Bounds.Height) / PageSize : RowHeight;

    public int SelectedIndex
    {
        get => Items.Count == 0 ? -1 : Math.Clamp(_selectedIndex, 0, Items.Count - 1);
        set
        {
            _selectedIndex = Items.Count == 0 ? 0 : Math.Clamp(value, 0, Items.Count - 1);
            EnsureSelectionVisible();
        }
    }

    public int ScrollOffset
    {
        get => UsesPagination ? HasMeasuredRows ? PageStarts()[CurrentPage] : CurrentPage * EffectivePageSize : Math.Clamp(_scrollOffset, 0, MaximumScrollOffset());
        set
        {
            if (UsesPagination)
            {
                CurrentPage = HasMeasuredRows ? PageStarts().FindLastIndex(start => start <= Math.Max(0, value)) : Math.Max(0, value) / EffectivePageSize;
            }
            else
            {
                _scrollOffset = Math.Clamp(value, 0, MaximumScrollOffset());
            }
        }
    }

    public Action<int>? SelectionChanged { get; set; }
    public float FontSize { get; set; }
    public Action<int>? Confirmed { get; set; }
    public bool ShowSelection { get; set; } = true;
    public bool HighlightSelection { get; set; } = true;
    public bool ShowScrollbar { get; set; } = true;
    public float PointerSize { get; set; } = 10;
    public float TextInset { get; set; } = 23;
    public IList<bool> EnabledItems { get; set; } = new List<bool>();
    public IList<UiImageSource?> Images { get; set; } = new List<UiImageSource?>();
    public IList<UiImageSource?> LeadingImages { get; set; } = new List<UiImageSource?>();
    public IList<UiImageSource?> TrailingImages { get; set; } = new List<UiImageSource?>();
    public IList<string> SecondaryItems { get; set; } = new List<string>();
    /// <summary>The fraction of the text area reserved for the secondary column.</summary>
    public float SecondaryWidth
    {
        get => _secondaryWidth;
        set
        {
            if (!float.IsFinite(value) || value < 0 || value > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _secondaryWidth = value;
        }
    }

    public override UiSize Measure(UiSize available)
    {
        float imageWidth = Images.Take(Items.Count).Any(image => image.HasValue) ? RowHeight : 0;
        float leadingWidth = LeadingImages.Take(Items.Count).Any(image => image.HasValue) ? RowHeight : 0;
        float trailingWidth = TrailingImages.Take(Items.Count).Any(image => image.HasValue) ? RowHeight : 0;
        float width = Items.Select((item, index) => (item.Length + (index < SecondaryItems.Count ? SecondaryItems[index].Length : 0)) * 10f + TextInset + 4 + leadingWidth + imageWidth + trailingWidth).DefaultIfEmpty(0).Max();
        return Orientation == UiOrientation.Horizontal
            ? ResolveDesiredSize(width * Items.Count, RowHeight, available)
            : ResolveDesiredSize(width, (UsesPagination && PageSize > 0 ? Math.Min(Items.Count, PageSize) : Items.Count) * RowHeight + (UsesPagination && PageSize > 0 && Items.Count > PageSize ? 24 : 0), available);
    }

    public override void Arrange(UiRect bounds)
    {
        if (bounds.Width != Bounds.Width || bounds.Height != Bounds.Height)
        {
            _hasMeasuredRows = false;
            _hasTextMeasurements = false;
            _measurements = [];
        }
        base.Arrange(bounds);
        EnsureSelectionVisible();
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        float rowWidth = Orientation == UiOrientation.Horizontal && Items.Count > 0 ? Bounds.Width / Items.Count : Bounds.Width;
        ListColumns columns = MeasureColumns(canvas, theme, rowWidth);
        MeasureRows(canvas, theme, columns);
        EnsureSelectionVisible();
        foreach ((int index, UiRect row) in VisibleRowBounds())
        {
            canvas.PushClip(row.Intersect(new UiRect(Bounds.X, Bounds.Y, Bounds.Width, UsesPagination ? ContentHeight : Bounds.Height)));
            try
            {
                theme.RowSeparator(canvas, row);
                if (ShowSelection && index == SelectedIndex)
                {
                    if (HighlightSelection || theme.AlwaysHighlightSelection)
                    {
                        theme.Selection(canvas, row.Inset(1), Orientation == UiOrientation.Horizontal);
                    }
                }

                bool hasSecondary = index < SecondaryItems.Count;
                RowColumns rowColumns = ColumnsForRow(columns, hasSecondary);
                if (index < LeadingImages.Count && LeadingImages[index] is { } leading)
                {
                    canvas.Image(leading, new UiRect(row.X + columns.TextInset, row.Y, columns.LeadingWidth, row.Height).Inset(2));
                }
                if (index < Images.Count && Images[index] is { } image)
                {
                    canvas.Image(image, new UiRect(row.X + columns.TextInset + columns.LeadingWidth, row.Y, columns.ImageWidth, row.Height).Inset(2));
                }
                if (index < TrailingImages.Count && TrailingImages[index] is { } trailing)
                {
                    canvas.Image(trailing, new UiRect(row.X + rowColumns.TrailingLeft, row.Y, columns.TrailingWidth, row.Height).Inset(2));
                }
                bool enabled = Enabled && (index >= EnabledItems.Count || EnabledItems[index]);
                float textLeft = row.X + columns.TextInset + columns.LeadingWidth + columns.ImageWidth;
                float secondaryLeft = row.X + rowColumns.SecondaryLeft;
                float textRight = row.X + rowColumns.SecondaryRight;
                UiTextFlow textFlow = TextFlow == UiTextFlow.Auto ? AutoRowHeight && Orientation == UiOrientation.Vertical ? UiTextFlow.Wrap : UiTextFlow.SingleLine : TextFlow;
                canvas.Text(Items[index], FontSize > 0 ? FontSize : theme.FontSize, new UiRect(textLeft, row.Y, rowColumns.PrimaryWidth, row.Height), enabled ? theme.Foreground : theme.Muted, UiAlignment.Center, textFlow);
                if (hasSecondary)
                {
                    UiRect secondaryClip = new(secondaryLeft, row.Y, Math.Max(0, textRight - secondaryLeft), row.Height);
                    UiRect secondaryBounds = new(secondaryLeft, row.Y, textFlow == UiTextFlow.Wrap ? Math.Max(secondaryClip.Width, columns.SecondaryWrapWidth) : secondaryClip.Width, row.Height);
                    if (SecondaryAlignment is UiAlignment.End or UiAlignment.Center)
                    {
                        UiSize? measured = TryMeasureText(canvas, SecondaryItems[index], FontSize > 0 ? FontSize : theme.FontSize, textFlow == UiTextFlow.SingleLine ? float.PositiveInfinity : secondaryBounds.Width);
                        if (measured.HasValue)
                        {
                            float width = Math.Min(secondaryClip.Width, measured.Value.Width);
                            float shift = (secondaryClip.Width - width) * (SecondaryAlignment == UiAlignment.End ? 1 : 0.5f);
                            // Visible glyph bounds can be narrower than the advances used for wrapping.
                            // Keep the original wrap width when shifting the visible text into alignment.
                            secondaryBounds = new UiRect(secondaryBounds.X + shift, row.Y, secondaryBounds.Width, row.Height);
                            if (textFlow == UiTextFlow.SingleLine)
                            {
                                secondaryBounds = new UiRect(secondaryBounds.X, row.Y, Math.Max(0, secondaryClip.Right - secondaryBounds.X), row.Height);
                            }
                        }
                    }
                    canvas.PushClip(secondaryClip);
                    try
                    {
                        canvas.Text(SecondaryItems[index], FontSize > 0 ? FontSize : theme.FontSize, secondaryBounds, enabled ? theme.Foreground : theme.Muted, UiAlignment.Center, textFlow);
                    }
                    finally
                    {
                        canvas.PopClip();
                    }
                }
            }
            finally
            {
                canvas.PopClip();
            }
        }

        if (UsesPagination && PageCount > 1)
        {
            DrawPageArrow(canvas, theme, false);
            DrawPageArrow(canvas, theme, true);
            string pageText = $"P.{CurrentPage + 1} / {PageCount}";
            float fontSize = Math.Min(FontSize > 0 ? FontSize : theme.FontSize, FooterHeight * 0.7f);
            float textWidth = Math.Min(Math.Max(0, Bounds.Width - PageArrowBounds(false).Width * 2), pageText.Length * fontSize * 0.65f);
            canvas.Text(pageText, fontSize, new UiRect(Bounds.X + (Bounds.Width - textWidth) / 2, Bounds.Bottom - FooterHeight, textWidth, FooterHeight), Enabled ? theme.Foreground : theme.Muted, UiAlignment.Center);
        }
        else if (!UsesPagination && ShowScrollbar && Items.Count > VisibleRows && Bounds.Width > 3)
        {
            float totalHeight = HasMeasuredRows ? _rowHeights.Sum() : Items.Count * RowHeight;
            float offsetHeight = HasMeasuredRows ? _rowHeights.Take(ScrollOffset).Sum() : ScrollOffset * RowHeight;
            float maximumOffsetHeight = HasMeasuredRows ? _rowHeights.Take(MaximumScrollOffset()).Sum() : Math.Max(1, Items.Count - VisibleRows) * RowHeight;
            float thumbHeight = HasMeasuredRows ? Math.Min(Bounds.Height, Math.Max(3, Bounds.Height * Math.Min(Bounds.Height, totalHeight) / totalHeight)) : Math.Max(3, Bounds.Height * VisibleRows / Items.Count);
            float top = Bounds.Y + (Bounds.Height - thumbHeight) * offsetHeight / Math.Max(1, maximumOffsetHeight);
            canvas.Fill(new UiRect(Bounds.Right - 3, top, 2, thumbHeight), theme.Muted);
        }
    }

    protected override void DrawPointer(IUiCanvas canvas, UiTheme theme)
    {
        if (!ShowSelection || SelectedIndex < 0)
        {
            return;
        }

        foreach ((int index, UiRect row) in VisibleRowBounds())
        {
            if (index == SelectedIndex)
            {
                ListColumns columns = MeasureColumns(canvas, theme, row.Width);
                float pointerSize = Math.Min(PointerSize, row.Height);
                if (columns.TextInset > 0)
                {
                    pointerSize = Math.Min(pointerSize, Math.Max(0, columns.TextInset - 4) / 1.5f);
                }
                theme.PointerBefore(canvas, row.X + columns.TextInset, row.Y + (row.Height - pointerSize) / 2, pointerSize);
                return;
            }
        }
    }

    public override bool Key(UiKey key)
    {
        if (!Enabled || Items.Count == 0)
        {
            return false;
        }

        int previous = SelectedIndex;
        switch (key)
        {
            case UiKey.Left when UsesPagination:
            case UiKey.PageUp when UsesPagination:
                CurrentPage--;
                break;
            case UiKey.Right when UsesPagination:
            case UiKey.PageDown when UsesPagination:
                CurrentPage++;
                break;
            case UiKey.Up when Orientation == UiOrientation.Vertical:
            case UiKey.Left when Orientation == UiOrientation.Horizontal:
                SelectedIndex = previous - 1;
                break;
            case UiKey.Down when Orientation == UiOrientation.Vertical:
            case UiKey.Right when Orientation == UiOrientation.Horizontal:
                SelectedIndex = previous + 1;
                break;
            case UiKey.PageUp:
                SelectedIndex = previous - VisibleRows;
                break;
            case UiKey.PageDown:
                SelectedIndex = previous + VisibleRows;
                break;
            case UiKey.Confirm:
                Confirmed?.Invoke(previous);
                return true;
            default:
                return false;
        }

        if (previous != SelectedIndex)
        {
            SelectionChanged?.Invoke(SelectedIndex);
        }

        return true;
    }

    public override void Pointer(float x, float y)
    {
        if (!Enabled || !Bounds.Contains(x, y))
        {
            return;
        }

        if (UsesPagination && y >= Bounds.Y + ContentHeight)
        {
            if (PageCount > 1 && PageArrowBounds(false).Contains(x, y))
            {
                Key(UiKey.Left);
            }
            else if (PageCount > 1 && PageArrowBounds(true).Contains(x, y))
            {
                Key(UiKey.Right);
            }

            return;
        }

        int index = -1;
        foreach ((int candidate, UiRect bounds) in VisibleRowBounds())
        {
            if (bounds.Contains(x, y))
            {
                index = candidate;
                break;
            }
        }
        if (index < 0)
        {
            return;
        }

        int previous = SelectedIndex;
        SelectedIndex = index;
        if (previous != SelectedIndex)
        {
            SelectionChanged?.Invoke(SelectedIndex);
        }

        Confirmed?.Invoke(SelectedIndex);
    }

    private void EnsureSelectionVisible()
    {
        if (UsesPagination)
        {
            return;
        }

        int selected = SelectedIndex;
        if (selected < 0)
        {
            _scrollOffset = 0;
            return;
        }

        _scrollOffset = ScrollOffset;
        if (selected < _scrollOffset)
        {
            _scrollOffset = selected;
        }
        else if (selected >= _scrollOffset + VisibleRows)
        {
            if (HasMeasuredRows)
            {
                while (_scrollOffset < selected && selected >= _scrollOffset + CountRows(_scrollOffset, true))
                {
                    _scrollOffset++;
                }
            }
            else
            {
                _scrollOffset = selected - VisibleRows + 1;
            }
        }
    }

    private float HeightAt(int index)
    {
        return HasMeasuredRows ? _rowHeights[index] : RenderedRowHeight;
    }

    private int CountRows(int start, bool fullOnly)
    {
        float height = 0;
        int count = 0;
        float available = UsesPagination ? ContentHeight : Bounds.Height;
        int limit = UsesPagination && PageSize > 0 ? PageSize : int.MaxValue;
        for (int index = start; index < Items.Count && count < limit; index++)
        {
            float next = HeightAt(index);
            if (height >= available || fullOnly && height + next > available && count > 0)
            {
                break;
            }
            height += next;
            count++;
        }
        return Math.Max(1, count);
    }

    private List<int> PageStarts()
    {
        List<int> starts = [0];
        for (int start = 0; start < Items.Count;)
        {
            start += CountRows(start, true);
            if (start < Items.Count)
            {
                starts.Add(start);
            }
        }
        return starts;
    }

    private int MaximumScrollOffset()
    {
        if (Orientation == UiOrientation.Horizontal)
        {
            return 0;
        }
        if (!HasMeasuredRows)
        {
            int visible = Math.Max(1, (int)Math.Floor(Math.Max(0, Bounds.Height) / RowHeight));
            return Math.Max(0, Items.Count - visible);
        }
        float height = 0;
        int start = Items.Count;
        while (start > 0 && (height + HeightAt(start - 1) <= Bounds.Height || start == Items.Count))
        {
            height += HeightAt(--start);
        }
        return start;
    }

    private IEnumerable<(int Index, UiRect Bounds)> VisibleRowBounds()
    {
        if (Orientation == UiOrientation.Horizontal)
        {
            for (int index = 0; index < Items.Count; index++)
            {
                yield return (index, new UiRect(Bounds.X + index * Bounds.Width / Items.Count, Bounds.Y, Bounds.Width / Items.Count, Bounds.Height));
            }
            yield break;
        }
        int offset = ScrollOffset;
        int count = HasMeasuredRows ? CountRows(offset, UsesPagination) : UsesPagination ? Math.Min(VisibleRows, EffectivePageSize) : (int)Math.Ceiling(Math.Max(0, Bounds.Height) / RenderedRowHeight);
        float y = Bounds.Y;
        for (int index = offset; index < Math.Min(Items.Count, offset + count); index++)
        {
            float height = HeightAt(index);
            yield return (index, new UiRect(Bounds.X, y, Bounds.Width, height));
            y += height;
        }
    }

    private readonly record struct ListColumns(float TextInset, float TextRight, float LeadingWidth, float ImageWidth, float TrailingWidth, float SecondaryWidth, float SecondaryWrapWidth);
    private readonly record struct RowColumns(float PrimaryWidth, float SecondaryLeft, float SecondaryRight, float TrailingLeft);

    private ListColumns MeasureColumns(IUiCanvas canvas, UiTheme theme, float rowWidth)
    {
        // Keep wrapping independent of the footer: shrinking a page must not widen its text columns.
        float rowHeight = SinglePageRowHeight;
        float textRight = Math.Max(TextInset, rowWidth - 4);
        float available = Math.Max(0, textRight - TextInset);
        float trailingWidth = TrailingImages.Take(Items.Count).Any(image => image.HasValue) ? Math.Min(rowHeight, available) : 0;
        float imageWidth = Images.Take(Items.Count).Any(image => image.HasValue) ? Math.Min(rowHeight, available - trailingWidth) : 0;
        float leadingWidth = LeadingImages.Take(Items.Count).Any(image => image.HasValue) ? Math.Min(rowHeight, Math.Max(0, available - trailingWidth - imageWidth)) : 0;
        float inset = Math.Max(0, TextInset);
        bool constrained = inset + leadingWidth + imageWidth + trailingWidth > rowWidth * 0.5f;
        if (constrained)
        {
            inset = Math.Min(inset, rowWidth * 0.12f);
            imageWidth = Math.Min(imageWidth, rowWidth * 0.16f);
            leadingWidth = Math.Min(leadingWidth, rowWidth * 0.12f);
            trailingWidth = Math.Min(trailingWidth, rowWidth * 0.16f);
            textRight = Math.Max(inset, rowWidth - 4);
            available = Math.Max(0, textRight - inset);
        }
        float secondaryWidth = (available - trailingWidth) * SecondaryWidth;
        float secondaryWrapWidth = 0;
        if (AutoSecondaryWidth)
        {
            float maximumWidth = Math.Max(0, available - trailingWidth - leadingWidth - imageWidth);
            float measuredWidth = 0;
            bool measuredAll = true;
            foreach (string secondary in SecondaryItems.Take(Items.Count))
            {
                UiSize? measured = TryMeasureText(canvas, secondary, FontSize > 0 ? FontSize : theme.FontSize, TextFlow == UiTextFlow.SingleLine ? float.PositiveInfinity : maximumWidth);
                if (!measured.HasValue)
                {
                    measuredAll = false;
                    break;
                }
                measuredWidth = Math.Max(measuredWidth, measured.Value.Width);
            }
            if (measuredAll)
            {
                secondaryWidth = Math.Clamp(measuredWidth, 0, maximumWidth);
                secondaryWrapWidth = maximumWidth;
            }
        }
        if (constrained)
        {
            secondaryWidth = Math.Min(secondaryWidth, Math.Max(0, available - leadingWidth - imageWidth - trailingWidth - rowWidth * 0.4f));
            secondaryWrapWidth = 0;
        }
        return new ListColumns(inset, textRight, leadingWidth, imageWidth, trailingWidth, secondaryWidth, secondaryWrapWidth);
    }

    private RowColumns ColumnsForRow(ListColumns columns, bool hasSecondary)
    {
        float textLeft = columns.TextInset + columns.LeadingWidth + columns.ImageWidth;
        float secondaryRight = columns.TextRight - columns.TrailingWidth;
        float secondaryLeft = Math.Max(textLeft, secondaryRight - columns.SecondaryWidth);
        float trailingLeft = secondaryRight;
        float primaryRight = hasSecondary ? secondaryLeft : secondaryRight;
        if (hasSecondary && TrailingImagesBeforeSecondary)
        {
            secondaryRight = columns.TextRight;
            secondaryLeft = Math.Max(textLeft + columns.TrailingWidth, secondaryRight - columns.SecondaryWidth);
            trailingLeft = secondaryLeft - columns.TrailingWidth;
            primaryRight = trailingLeft;
        }
        if (hasSecondary && AutoSecondaryWidth && (!TrailingImagesBeforeSecondary || columns.TrailingWidth == 0))
        {
            float gap = Math.Max(4, FontSize * 0.2f);
            primaryRight -= Math.Min(gap, Math.Max(0, primaryRight - textLeft));
        }
        return new RowColumns(Math.Max(0, primaryRight - textLeft), secondaryLeft, secondaryRight, trailingLeft);
    }

    private void MeasureRows(IUiCanvas canvas, UiTheme theme, ListColumns columns)
    {
        if (!AutoRowHeight || Orientation == UiOrientation.Horizontal)
        {
            return;
        }
        MeasureTextRows(canvas, theme, columns, ref _measurements, ref _textHeights);
        _hasTextMeasurements = true;
        if (_rowHeights.Length != Items.Count)
        {
            _rowHeights = new float[Items.Count];
        }
        float minimumHeight = RenderedRowHeight;
        bool expanded = false;
        for (int index = 0; index < Items.Count; index++)
        {
            _rowHeights[index] = Math.Max(minimumHeight, _textHeights[index]);
            expanded |= _rowHeights[index] > minimumHeight;
        }
        _hasMeasuredRows = expanded;
    }

    private void MeasureTextRows(IUiCanvas canvas, UiTheme theme, ListColumns columns, ref RowMeasurement[] measurements, ref float[] textHeights)
    {
        if (measurements.Length != Items.Count)
        {
            measurements = new RowMeasurement[Items.Count];
            textHeights = new float[Items.Count];
        }
        for (int index = 0; index < Items.Count; index++)
        {
            bool secondary = index < SecondaryItems.Count;
            RowColumns rowColumns = ColumnsForRow(columns, secondary);
            float primaryWidth = TextFlow == UiTextFlow.SingleLine ? float.PositiveInfinity : rowColumns.PrimaryWidth;
            float secondaryWidth = TextFlow == UiTextFlow.SingleLine ? float.PositiveInfinity : Math.Max(columns.SecondaryWrapWidth, rowColumns.SecondaryRight - rowColumns.SecondaryLeft);
            RowMeasurement measurement = new(Items[index], secondary ? SecondaryItems[index] : null, primaryWidth, secondary ? secondaryWidth : 0, FontSize > 0 ? FontSize : theme.FontSize);
            if (measurements[index] != measurement)
            {
                UiSize? primary = TryMeasureText(canvas, measurement.Text, measurement.FontSize, measurement.PrimaryWidth);
                UiSize? value = measurement.Secondary is null ? null : TryMeasureText(canvas, measurement.Secondary, measurement.FontSize, measurement.SecondaryWidth);
                textHeights[index] = Math.Max(primary?.Height ?? 0, value?.Height ?? 0);
                if (primary.HasValue && (measurement.Secondary is null || value.HasValue))
                {
                    measurements[index] = measurement;
                }
            }
        }
    }

    private static UiSize? TryMeasureText(IUiCanvas canvas, string text, float fontSize, float width)
    {
        if (width <= 0 || text.Length == 0)
        {
            return new UiSize(0, 0);
        }
        try
        {
            return canvas.MeasureText(text, fontSize, width);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private UiRect PageArrowBounds(bool next)
    {
        float width = Math.Min(48, Bounds.Width / 2);
        return new UiRect(next ? Bounds.Right - width : Bounds.X, Bounds.Bottom - FooterHeight, width, FooterHeight);
    }

    private void DrawPageArrow(IUiCanvas canvas, UiTheme theme, bool next)
    {
        UiRect bounds = PageArrowBounds(next);
        bool enabled = Enabled && (next ? CurrentPage < PageCount - 1 : CurrentPage > 0);
        UiColor color = enabled ? theme.Foreground : theme.Muted;
        float direction = next ? 1 : -1;
        float centerX = bounds.X + bounds.Width / 2;
        float centerY = bounds.Y + bounds.Height / 2;
        float size = Math.Min(6, Math.Min(bounds.Width, bounds.Height) / 3);
        canvas.Line(centerX - direction * size / 2, centerY - size, centerX + direction * size / 2, centerY, color, 2);
        canvas.Line(centerX + direction * size / 2, centerY, centerX - direction * size / 2, centerY + size, color, 2);
    }
}
