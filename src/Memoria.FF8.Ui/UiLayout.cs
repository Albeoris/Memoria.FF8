namespace Memoria.FF8.Ui;

public enum UiOrientation
{
    Horizontal,
    Vertical
}

internal static class UiLayout
{
    public static UiRect Inset(UiRect bounds, UiThickness thickness)
    {
        return new UiRect(bounds.X + thickness.Left, bounds.Y + thickness.Top, Math.Max(0, bounds.Width - thickness.Horizontal), Math.Max(0, bounds.Height - thickness.Vertical));
    }

    public static UiSize MeasureOuter(UiElement child, UiSize available)
    {
        UiSize desired = child.Measure(new UiSize(Math.Max(0, available.Width - child.Margin.Horizontal), Math.Max(0, available.Height - child.Margin.Vertical)));
        return new UiSize(desired.Width + child.Margin.Horizontal, desired.Height + child.Margin.Vertical);
    }

    public static void ArrangeChild(UiElement child, UiRect slot)
    {
        UiRect inner = Inset(slot, child.Margin);
        bool needsDesiredSize = child.Width.HasValue || child.Height.HasValue || child.HorizontalAlignment != UiAlignment.Stretch || child.VerticalAlignment != UiAlignment.Stretch;
        UiSize desired = needsDesiredSize ? child.Measure(new UiSize(inner.Width, inner.Height)) : new UiSize(inner.Width, inner.Height);
        float width = child.Width.HasValue || child.HorizontalAlignment != UiAlignment.Stretch ? desired.Width : inner.Width;
        float height = child.Height.HasValue || child.VerticalAlignment != UiAlignment.Stretch ? desired.Height : inner.Height;
        float x = Align(inner.X, inner.Width, width, child.HorizontalAlignment);
        float y = Align(inner.Y, inner.Height, height, child.VerticalAlignment);
        child.Arrange(new UiRect(x, y, width, height));
    }

    private static float Align(float origin, float available, float size, UiAlignment alignment)
    {
        return alignment switch
        {
            UiAlignment.Center => origin + (available - size) / 2,
            UiAlignment.End => origin + available - size,
            _ => origin
        };
    }
}

public sealed class UiStack : UiElement
{
    public UiOrientation Orientation { get; set; } = UiOrientation.Vertical;
    public float Gap { get; set; } = 4;
    public UiThickness Padding { get; set; }

    public override UiSize Measure(UiSize available)
    {
        UiSize inner = new(Math.Max(0, available.Width - Padding.Horizontal), Math.Max(0, available.Height - Padding.Vertical));
        UiSize[] sizes = Children.Where(child => child.Visible).Select(child => UiLayout.MeasureOuter(child, inner)).ToArray();
        float gaps = Math.Max(0, Gap) * Math.Max(0, sizes.Length - 1);
        float width = Orientation == UiOrientation.Horizontal ? sizes.Sum(size => size.Width) + gaps : sizes.Select(size => size.Width).DefaultIfEmpty().Max();
        float height = Orientation == UiOrientation.Vertical ? sizes.Sum(size => size.Height) + gaps : sizes.Select(size => size.Height).DefaultIfEmpty().Max();
        return ResolveDesiredSize(width + Padding.Horizontal, height + Padding.Vertical, available);
    }

    public override void Arrange(UiRect bounds)
    {
        Bounds = bounds;
        UiElement[] children = Children.Where(child => child.Visible).ToArray();
        if (children.Length == 0)
        {
            return;
        }

        UiRect content = UiLayout.Inset(bounds, Padding);
        float gap = Math.Max(0, Gap);
        float available = Orientation == UiOrientation.Vertical ? content.Height : content.Width;
        float size = Math.Max(0, (available - gap * (children.Length - 1)) / children.Length);
        for (int index = 0; index < children.Length; index++)
        {
            UiRect childBounds = Orientation == UiOrientation.Vertical
                ? new UiRect(content.X, content.Y + index * (size + gap), content.Width, size)
                : new UiRect(content.X + index * (size + gap), content.Y, size, content.Height);
            UiLayout.ArrangeChild(children[index], childBounds);
        }
    }
}

public sealed class UiGrid : UiElement
{
    private int _columns = 2;
    private int _rows;
    public IList<UiGridLength> RowDefinitions { get; } = new List<UiGridLength>();
    public IList<UiGridLength> ColumnDefinitions { get; } = new List<UiGridLength>();

    public int Columns
    {
        get => _columns;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _columns = value;
        }
    }

    public int Rows
    {
        get => _rows;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _rows = value;
        }
    }

    public float Gap { get; set; } = 4;
    public UiThickness Padding { get; set; }

    public override UiSize Measure(UiSize available)
    {
        GetDimensions(out int rows, out int columns, out bool flow);
        UiSize inner = new(Math.Max(0, available.Width - Padding.Horizontal), Math.Max(0, available.Height - Padding.Vertical));
        float[] widths = SizeTracks(columns, columns, ColumnDefinitions, inner, true, flow, true);
        float[] heights = SizeTracks(rows, columns, RowDefinitions, inner, false, flow, true, SizeTracks(columns, columns, ColumnDefinitions, inner, true, flow, false));
        return ResolveDesiredSize(widths.Sum() + Math.Max(0, Gap) * (columns - 1) + Padding.Horizontal, heights.Sum() + Math.Max(0, Gap) * (rows - 1) + Padding.Vertical, available);
    }

    public override void Arrange(UiRect bounds)
    {
        Bounds = bounds;
        GetDimensions(out int rows, out int columns, out bool flow);
        UiRect content = UiLayout.Inset(bounds, Padding);
        UiSize available = new(content.Width, content.Height);
        float[] widths = SizeTracks(columns, columns, ColumnDefinitions, available, true, flow, false);
        float[] heights = SizeTracks(rows, columns, RowDefinitions, available, false, flow, false, widths);
        float gap = Math.Max(0, Gap);
        for (int index = 0; index < Children.Count; index++)
        {
            UiElement child = Children[index];
            GetPlacement(child, index, columns, flow, out int row, out int column, out int rowSpan, out int columnSpan);
            ValidatePlacement(row, rowSpan, rows, nameof(child.GridRow));
            ValidatePlacement(column, columnSpan, columns, nameof(child.GridColumn));
            float x = content.X + widths.Take(column).Sum() + gap * column;
            float y = content.Y + heights.Take(row).Sum() + gap * row;
            float width = widths.Skip(column).Take(columnSpan).Sum() + gap * (columnSpan - 1);
            float height = heights.Skip(row).Take(rowSpan).Sum() + gap * (rowSpan - 1);
            UiLayout.ArrangeChild(child, new UiRect(x, y, width, height));
        }
    }

    private void GetDimensions(out int rows, out int columns, out bool flow)
    {
        flow = RowDefinitions.Count == 0 && ColumnDefinitions.Count == 0 && Rows == 0 && Children.All(child => child.GridRow == 0 && child.GridColumn == 0 && child.GridRowSpan == 1 && child.GridColumnSpan == 1);
        columns = ColumnDefinitions.Count > 0 ? ColumnDefinitions.Count : flow ? Columns : Math.Max(Columns, Children.Select(child => child.GridColumn + child.GridColumnSpan).DefaultIfEmpty(1).Max());
        rows = RowDefinitions.Count > 0 ? RowDefinitions.Count : Rows > 0 ? Rows : flow ? Math.Max(1, (Children.Count + columns - 1) / columns) : Math.Max(1, Children.Select(child => child.GridRow + child.GridRowSpan).DefaultIfEmpty(1).Max());
    }

    private float[] SizeTracks(int count, int columns, IList<UiGridLength> definitions, UiSize available, bool horizontal, bool flow, bool measuring, float[]? columnWidths = null)
    {
        float[] sizes = new float[count];
        UiGridLength[] tracks = Enumerable.Range(0, count).Select(index => definitions.Count == 0 ? UiGridLength.Star() : definitions[index]).ToArray();
        for (int index = 0; index < count; index++)
        {
            sizes[index] = tracks[index].Unit == UiGridUnit.Pixel ? tracks[index].Value : 0;
        }

        for (int index = 0; index < Children.Count; index++)
        {
            UiElement child = Children[index];
            GetPlacement(child, index, columns, flow, out int row, out int column, out int rowSpan, out int columnSpan);
            int start = horizontal ? column : row;
            int span = horizontal ? columnSpan : rowSpan;
            ValidatePlacement(start, span, count, horizontal ? nameof(child.GridColumn) : nameof(child.GridRow));
            if (!child.Visible)
            {
                continue;
            }

            int flexible = Enumerable.Range(start, span).Count(track => tracks[track].Unit == UiGridUnit.Auto || measuring && tracks[track].Unit == UiGridUnit.Star);
            if (flexible == 0)
            {
                continue;
            }

            UiSize childAvailable = columnWidths == null ? available : new UiSize(columnWidths.Skip(column).Take(columnSpan).Sum() + Math.Max(0, Gap) * (columnSpan - 1), available.Height);
            UiSize desired = UiLayout.MeasureOuter(child, childAvailable);
            float wanted = horizontal ? desired.Width : desired.Height;
            float existing = sizes.Skip(start).Take(span).Sum() + Math.Max(0, Gap) * (span - 1);
            if (wanted > existing && flexible > 0)
            {
                float addition = (wanted - existing) / flexible;
                for (int track = start; track < start + span; track++)
                {
                    if (tracks[track].Unit == UiGridUnit.Auto || measuring && tracks[track].Unit == UiGridUnit.Star)
                    {
                        sizes[track] += addition;
                    }
                }
            }
        }

        if (!measuring)
        {
            float space = Math.Max(0, (horizontal ? available.Width : available.Height) - Math.Max(0, Gap) * (count - 1));
            float committed = sizes.Sum();
            if (committed > space && committed > 0)
            {
                for (int index = 0; index < count; index++)
                {
                    sizes[index] *= space / committed;
                }
            }

            float stars = tracks.Where(track => track.Unit == UiGridUnit.Star).Sum(track => track.Value);
            float remaining = Math.Max(0, space - sizes.Sum());
            for (int index = 0; index < count; index++)
            {
                if (tracks[index].Unit == UiGridUnit.Star && stars > 0)
                {
                    sizes[index] = remaining * tracks[index].Value / stars;
                }
            }
        }

        return sizes;
    }

    private static void GetPlacement(UiElement child, int index, int columns, bool flow, out int row, out int column, out int rowSpan, out int columnSpan)
    {
        row = flow ? index / columns : child.GridRow;
        column = flow ? index % columns : child.GridColumn;
        rowSpan = child.GridRowSpan;
        columnSpan = child.GridColumnSpan;
    }

    private static void ValidatePlacement(int start, int span, int count, string name)
    {
        if (start < 0 || span < 1 || start + span > count)
        {
            throw new ArgumentOutOfRangeException(name, "Grid cells and spans must fit their track definitions.");
        }
    }
}

public sealed class UiAnchorPanel : UiElement
{
    public UiThickness Padding { get; set; }

    public override void Arrange(UiRect bounds)
    {
        Bounds = bounds;
        UiRect content = UiLayout.Inset(bounds, Padding);
        foreach (UiElement child in Children)
        {
            UiAnchors anchors = child.Anchors;
            if (!float.IsFinite(anchors.Left) || !float.IsFinite(anchors.Top) || !float.IsFinite(anchors.Right) || !float.IsFinite(anchors.Bottom) || anchors.Right < anchors.Left || anchors.Bottom < anchors.Top)
            {
                throw new ArgumentOutOfRangeException(nameof(child.Anchors), "Anchors must be finite ordered parent-relative edges.");
            }

            UiRect slot = new(content.X + content.Width * anchors.Left, content.Y + content.Height * anchors.Top, content.Width * (anchors.Right - anchors.Left), content.Height * (anchors.Bottom - anchors.Top));
            UiLayout.ArrangeChild(child, slot);
        }
    }
}
