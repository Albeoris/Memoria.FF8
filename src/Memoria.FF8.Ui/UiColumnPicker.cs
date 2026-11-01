namespace Memoria.FF8.Ui;

/// <summary>A column-major list with measured name/quantity pairs and whole-column scrolling.</summary>
public sealed class UiColumnPicker : UiElement
{
    private sealed record Item(UiButton Button, UiLabel Name, UiLabel Quantity);
    private readonly List<Item> items = [];
    private float[] nameWidths = [];
    private float[] quantityWidths = [];
    private bool initialRevealed;

    public float RowHeight { get; set; } = 26;
    public float NameQuantityGap { get; set; } = 8;
    public float ColumnGap { get; set; } = 16;
    public int FirstVisibleColumn { get; set; }
    public int InitialIndex { get; set; }
    public int RowCount { get; private set; } = 1;
    public Action<int>? FirstColumnChanged { get; set; }

    public void Add(UiButton button, UiLabel name, UiLabel quantity)
    {
        items.Add(new Item(button, name, quantity));
        button.Children.Add(name);
        button.Children.Add(quantity);
        Children.Add(button);
    }

    public override void Arrange(UiRect bounds)
    {
        Bounds = bounds;
        RowCount = Math.Max(1, (int)(bounds.Height / Math.Max(1, RowHeight)));
        int columns = (items.Count + RowCount - 1) / RowCount;
        nameWidths = new float[columns];
        quantityWidths = new float[columns];
        for (int index = 0; index < items.Count; index++)
        {
            int column = index / RowCount;
            nameWidths[column] = Math.Max(nameWidths[column], items[index].Name.Measure(new UiSize(float.PositiveInfinity, RowHeight)).Width);
            quantityWidths[column] = Math.Max(quantityWidths[column], items[index].Quantity.Measure(new UiSize(float.PositiveInfinity, RowHeight)).Width);
        }
        FirstVisibleColumn = Math.Clamp(FirstVisibleColumn, 0, Math.Max(0, columns - 1));
        if (!initialRevealed && items.Count > 0)
        {
            RevealColumn(Math.Clamp(InitialIndex, 0, items.Count - 1) / RowCount);
            initialRevealed = true;
        }
        int focused = items.FindIndex(item => item.Button.IsFocused);
        if (focused >= 0)
        {
            RevealColumn(focused / RowCount);
        }
        ArrangeItems();
    }

    private float ColumnWidth(int column) => nameWidths[column] + NameQuantityGap + quantityWidths[column];

    private int LastVisibleColumn()
    {
        float width = 0;
        int last = FirstVisibleColumn - 1;
        for (int column = FirstVisibleColumn; column < nameWidths.Length; column++)
        {
            float next = ColumnWidth(column) + (column == FirstVisibleColumn ? 0 : ColumnGap);
            if (column > FirstVisibleColumn && width + next > Bounds.Width)
            {
                break;
            }
            width += next;
            last = column;
        }
        return last;
    }

    private void RevealColumn(int column)
    {
        int previous = FirstVisibleColumn;
        if (column < FirstVisibleColumn)
        {
            FirstVisibleColumn = column;
        }
        while (column > LastVisibleColumn())
        {
            FirstVisibleColumn++;
        }
        if (previous != FirstVisibleColumn)
        {
            FirstColumnChanged?.Invoke(FirstVisibleColumn);
        }
    }

    public void EnsureVisible(UiElement element)
    {
        int index = items.FindIndex(item => ReferenceEquals(item.Button, element));
        if (index >= 0 && nameWidths.Length > 0)
        {
            RevealColumn(index / RowCount);
            ArrangeItems();
        }
    }

    private void ArrangeItems()
    {
        int last = LastVisibleColumn();
        float x = Bounds.X;
        for (int column = 0; column < nameWidths.Length; column++)
        {
            bool visible = column >= FirstVisibleColumn && column <= last;
            for (int row = 0; row < RowCount && column * RowCount + row < items.Count; row++)
            {
                Item item = items[column * RowCount + row];
                item.Button.Visible = visible;
                if (!visible)
                {
                    continue;
                }
                UiRect cell = new(x, Bounds.Y + row * RowHeight, ColumnWidth(column), RowHeight);
                item.Button.Arrange(cell);
                item.Name.Arrange(new UiRect(x, cell.Y, nameWidths[column], RowHeight));
                item.Quantity.Arrange(new UiRect(x + nameWidths[column] + NameQuantityGap, cell.Y, quantityWidths[column], RowHeight));
            }
            if (visible)
            {
                x += ColumnWidth(column) + ColumnGap;
            }
        }
    }

    public override bool TryNavigate(UiElement origin, UiKey key, out UiElement? target)
    {
        target = null;
        int index = items.FindIndex(item => ReferenceEquals(item.Button, origin));
        if (index < 0 || key is not (UiKey.Up or UiKey.Down or UiKey.Left or UiKey.Right))
        {
            return false;
        }
        int next = key switch
        {
            UiKey.Up => index - 1,
            UiKey.Down => index + 1,
            UiKey.Left => index - RowCount,
            UiKey.Right when (index / RowCount + 1) * RowCount < items.Count => Math.Min(index + RowCount, items.Count - 1),
            _ => -1
        };
        if (next >= 0 && next < items.Count)
        {
            target = items[next].Button;
            EnsureVisible(target);
        }
        return true;
    }
}
