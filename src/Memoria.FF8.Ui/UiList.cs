namespace Memoria.FF8.Ui;

public sealed class UiList : UiElement
{
    private int _selectedIndex;
    private int _scrollOffset;
    private float _rowHeight = 28;
    private int _pageSize;
    private float _secondaryWidth = 0.35f;
    private IList<string> _items = new List<string>();

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

            if (UsesPagination)
            {
                return FitPageRows && PageSize > 0 && ContentHeight > 0 ? PageSize : Math.Max(0, (int)Math.Floor(ContentHeight / RowHeight));
            }

            return Math.Max(1, (int)Math.Floor(Math.Max(0, Bounds.Height) / RowHeight));
        }
    }

    /// <summary>The page capacity, limited to full visible rows unless FitPageRows fits the requested PageSize.</summary>
    public int EffectivePageSize => Math.Max(1, PageSize > 0 ? Math.Min(PageSize, VisibleRows) : VisibleRows);
    public int PageCount => Items.Count == 0 ? 0 : (Items.Count - 1) / EffectivePageSize + 1;
    /// <summary>The zero-based page containing the selection.</summary>
    public int CurrentPage
    {
        get => SelectedIndex < 0 ? 0 : SelectedIndex / EffectivePageSize;
        set
        {
            int page = Math.Clamp(value, 0, Math.Max(0, PageCount - 1));
            int row = Math.Max(0, SelectedIndex) % EffectivePageSize;
            SelectedIndex = page * EffectivePageSize + row;
        }
    }

    private bool UsesPagination => Paginated && Orientation == UiOrientation.Vertical;
    private float FooterHeight => UsesPagination ? Math.Min(24, Math.Max(0, Bounds.Height)) : 0;
    private float ContentHeight => Math.Max(0, Bounds.Height - FooterHeight);
    private float RenderedRowHeight => UsesPagination && FitPageRows && PageSize > 0 ? ContentHeight / PageSize : RowHeight;

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
        get => UsesPagination ? CurrentPage * EffectivePageSize : Math.Clamp(_scrollOffset, 0, Math.Max(0, Items.Count - VisibleRows));
        set
        {
            if (UsesPagination)
            {
                CurrentPage = Math.Max(0, value) / EffectivePageSize;
            }
            else
            {
                _scrollOffset = Math.Clamp(value, 0, Math.Max(0, Items.Count - VisibleRows));
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
        float width = Items.Select((item, index) => (item.Length + (index < SecondaryItems.Count ? SecondaryItems[index].Length : 0)) * 10f + TextInset + 4 + (index < Images.Count && Images[index].HasValue ? RowHeight : 0) + (index < TrailingImages.Count && TrailingImages[index].HasValue ? RowHeight : 0)).DefaultIfEmpty(0).Max();
        return Orientation == UiOrientation.Horizontal
            ? ResolveDesiredSize(width * Items.Count, RowHeight, available)
            : ResolveDesiredSize(width, (UsesPagination && PageSize > 0 ? Math.Min(Items.Count, PageSize) : Items.Count) * RowHeight + (UsesPagination ? 24 : 0), available);
    }

    public override void Arrange(UiRect bounds)
    {
        base.Arrange(bounds);
        EnsureSelectionVisible();
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        int offset = ScrollOffset;
        bool horizontal = Orientation == UiOrientation.Horizontal;
        float rowHeight = RenderedRowHeight;
        int rows = horizontal ? Items.Count : Math.Min(Items.Count - offset, UsesPagination ? Math.Min(VisibleRows, EffectivePageSize) : (int)Math.Ceiling(Math.Max(0, Bounds.Height) / rowHeight));
        bool reserveTrailingImage = TrailingImages.Take(Items.Count).Any(image => image.HasValue);
        for (int index = offset; index < offset + rows; index++)
        {
            UiRect row = horizontal
                ? new UiRect(Bounds.X + index * Bounds.Width / Items.Count, Bounds.Y, Bounds.Width / Items.Count, Bounds.Height)
                : new UiRect(Bounds.X, Bounds.Y + (index - offset) * rowHeight, Bounds.Width, rowHeight);
            if (ShowSelection && index == SelectedIndex)
            {
                if (HighlightSelection)
                {
                    canvas.Fill(row.Inset(1), theme.Highlight);
                }
                if (IsFocused && Enabled)
                {
                    theme.Pointer(canvas, row.X + 5, row.Y + (row.Height - PointerSize) / 2, PointerSize);
                }
            }

            float trailingWidth = reserveTrailingImage ? Math.Min(rowHeight, Math.Max(0, row.Width - TextInset - 4)) : 0;
            float imageWidth = 0;
            if (index < Images.Count && Images[index] is { } image)
            {
                imageWidth = Math.Min(rowHeight, Math.Max(0, row.Width - TextInset - 4 - trailingWidth));
                canvas.Image(image, new UiRect(row.X + TextInset, row.Y, imageWidth, row.Height).Inset(2));
            }
            if (index < TrailingImages.Count && TrailingImages[index] is { } trailing)
            {
                canvas.Image(trailing, new UiRect(row.Right - 4 - trailingWidth, row.Y, trailingWidth, row.Height).Inset(2));
            }
            bool enabled = Enabled && (index >= EnabledItems.Count || EnabledItems[index]);
            float textLeft = row.X + TextInset + imageWidth;
            float textRight = row.Right - 4 - trailingWidth;
            float secondaryLeft = row.X + TextInset + Math.Max(0, row.Width - TextInset - 4 - trailingWidth) * (1 - SecondaryWidth);
            bool hasSecondary = index < SecondaryItems.Count;
            canvas.Text(Items[index], FontSize > 0 ? FontSize : theme.FontSize, new UiRect(textLeft, row.Y, Math.Max(0, (hasSecondary ? Math.Min(secondaryLeft, textRight) : textRight) - textLeft), row.Height), enabled ? theme.Foreground : theme.Muted, UiAlignment.Center);
            if (hasSecondary)
            {
                canvas.Text(SecondaryItems[index], FontSize > 0 ? FontSize : theme.FontSize, new UiRect(secondaryLeft, row.Y, Math.Max(0, textRight - secondaryLeft), row.Height), enabled ? theme.Foreground : theme.Muted, UiAlignment.Center);
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
            float thumbHeight = Math.Max(3, Bounds.Height * VisibleRows / Items.Count);
            float top = Bounds.Y + (Bounds.Height - thumbHeight) * ScrollOffset / Math.Max(1, Items.Count - VisibleRows);
            canvas.Fill(new UiRect(Bounds.Right - 3, top, 2, thumbHeight), theme.Muted);
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

        int row = RenderedRowHeight > 0 ? (int)((y - Bounds.Y) / RenderedRowHeight) : -1;
        if (UsesPagination && (row < 0 || row >= Math.Min(VisibleRows, EffectivePageSize)))
        {
            return;
        }

        int index = Orientation == UiOrientation.Horizontal
            ? Math.Min(Items.Count - 1, (int)((x - Bounds.X) * Items.Count / Bounds.Width))
            : ScrollOffset + row;
        if (index < 0 || index >= Items.Count)
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
            _scrollOffset = selected - VisibleRows + 1;
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
