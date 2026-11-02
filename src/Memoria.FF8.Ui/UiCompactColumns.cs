namespace Memoria.FF8.Ui;

/// <summary>Column-major content with measured column widths, fixed row spacing and a centered group.</summary>
public sealed class UiCompactColumns : UiElement
{
    public float RowHeight { get; set; } = 28;
    public float ColumnGap { get; set; } = 24;

    public override void Arrange(UiRect bounds)
    {
        Bounds = bounds;
        UiElement[] items = Children.Where(child => child.Visible).ToArray();
        if (items.Length == 0)
        {
            return;
        }
        int rows = Math.Max(1, (int)(bounds.Height / Math.Max(1, RowHeight)));
        int columns = (items.Length + rows - 1) / rows;
        // Balance the columns while retaining the smallest number that fits vertically.
        rows = (items.Length + columns - 1) / columns;
        float[] widths = new float[columns];
        for (int index = 0; index < items.Length; index++)
        {
            widths[index / rows] = Math.Max(widths[index / rows], items[index].Measure(new UiSize(float.PositiveInfinity, RowHeight)).Width);
        }
        float totalWidth = widths.Sum() + ColumnGap * (columns - 1);
        float x = bounds.X + (bounds.Width - totalWidth) / 2;
        float y = bounds.Y + (bounds.Height - rows * RowHeight) / 2;
        for (int column = 0; column < columns; column++)
        {
            for (int row = 0; row < rows && column * rows + row < items.Length; row++)
            {
                items[column * rows + row].Arrange(new UiRect(x, y + row * RowHeight, widths[column], RowHeight));
            }
            x += widths[column] + ColumnGap;
        }
    }
}
