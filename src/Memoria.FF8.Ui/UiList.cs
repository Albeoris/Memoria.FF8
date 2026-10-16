namespace Memoria.FF8.Ui;

public sealed class UiList : UiElement
{
    private int _selectedIndex;
    private int _scrollOffset;
    private float _rowHeight = 28;
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
    public int VisibleRows => Orientation == UiOrientation.Horizontal ? Math.Max(1, Items.Count) : Math.Max(1, (int)Math.Floor(Math.Max(0, Bounds.Height) / RowHeight));

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
        get => Math.Clamp(_scrollOffset, 0, Math.Max(0, Items.Count - VisibleRows));
        set => _scrollOffset = Math.Clamp(value, 0, Math.Max(0, Items.Count - VisibleRows));
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

    public override UiSize Measure(UiSize available)
    {
        float width = Items.Select((item, index) => item.Length * 10f + TextInset + 4 + (index < Images.Count && Images[index].HasValue ? RowHeight : 0) + (index < TrailingImages.Count && TrailingImages[index].HasValue ? RowHeight : 0)).DefaultIfEmpty(0).Max();
        return Orientation == UiOrientation.Horizontal
            ? ResolveDesiredSize(width * Items.Count, RowHeight, available)
            : ResolveDesiredSize(width, Items.Count * RowHeight, available);
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
        int rows = horizontal ? Items.Count : Math.Min(Items.Count - offset, (int)Math.Ceiling(Math.Max(0, Bounds.Height) / RowHeight));
        for (int index = offset; index < offset + rows; index++)
        {
            UiRect row = horizontal
                ? new UiRect(Bounds.X + index * Bounds.Width / Items.Count, Bounds.Y, Bounds.Width / Items.Count, Bounds.Height)
                : new UiRect(Bounds.X, Bounds.Y + (index - offset) * RowHeight, Bounds.Width, RowHeight);
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

            float imageWidth = 0;
            if (index < Images.Count && Images[index] is { } image)
            {
                imageWidth = Math.Min(RowHeight, Math.Max(0, row.Width - TextInset - 4));
                canvas.Image(image, new UiRect(row.X + TextInset, row.Y, imageWidth, row.Height).Inset(2));
            }
            float trailingWidth = 0;
            if (index < TrailingImages.Count && TrailingImages[index] is { } trailing)
            {
                trailingWidth = Math.Min(RowHeight, Math.Max(0, row.Width - TextInset - 4 - imageWidth));
                canvas.Image(trailing, new UiRect(row.Right - 4 - trailingWidth, row.Y, trailingWidth, row.Height).Inset(2));
            }
            bool enabled = Enabled && (index >= EnabledItems.Count || EnabledItems[index]);
            canvas.Text(Items[index], FontSize > 0 ? FontSize : theme.FontSize, new UiRect(row.X + TextInset + imageWidth, row.Y, Math.Max(0, row.Width - TextInset - 4 - imageWidth - trailingWidth), row.Height), enabled ? theme.Foreground : theme.Muted, UiAlignment.Center);
        }

        if (ShowScrollbar && Items.Count > VisibleRows && Bounds.Width > 3)
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

        int index = Orientation == UiOrientation.Horizontal
            ? Math.Min(Items.Count - 1, (int)((x - Bounds.X) * Items.Count / Bounds.Width))
            : ScrollOffset + (int)((y - Bounds.Y) / RowHeight);
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
}
