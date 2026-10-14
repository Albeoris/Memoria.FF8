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

    public int VisibleRows => Math.Max(1, (int)Math.Floor(Math.Max(0, Bounds.Height) / RowHeight));

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

    public override UiSize Measure(UiSize available)
    {
        float width = Items.Select(item => item.Length * 10f + 27).DefaultIfEmpty(0).Max();
        return ResolveDesiredSize(width, Items.Count * RowHeight, available);
    }

    public override void Arrange(UiRect bounds)
    {
        base.Arrange(bounds);
        EnsureSelectionVisible();
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        int offset = ScrollOffset;
        int rows = Math.Min(Items.Count - offset, (int)Math.Ceiling(Math.Max(0, Bounds.Height) / RowHeight));
        for (int index = offset; index < offset + rows; index++)
        {
            UiRect row = new(Bounds.X, Bounds.Y + (index - offset) * RowHeight, Bounds.Width, RowHeight);
            if (ShowSelection && index == SelectedIndex)
            {
                canvas.Fill(row.Inset(1), theme.Highlight);
                if (IsFocused && Enabled)
                {
                    theme.Pointer(canvas, row.X + 5, row.Y + (RowHeight - 10) / 2);
                }
            }

            canvas.Text(Items[index], FontSize > 0 ? FontSize : theme.FontSize, new UiRect(row.X + 23, row.Y + 3, Math.Max(0, row.Width - 27), row.Height), Enabled ? theme.Foreground : theme.Muted);
        }

        if (Items.Count > VisibleRows && Bounds.Width > 3)
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
            case UiKey.Up:
                SelectedIndex = previous - 1;
                break;
            case UiKey.Down:
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

        int index = ScrollOffset + (int)((y - Bounds.Y) / RowHeight);
        if (index >= Items.Count)
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
