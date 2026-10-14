namespace Memoria.FF8.Ui;

public enum UiOrientation
{
    Horizontal,
    Vertical
}

/// <summary>Equal-sized stack children in absolute design coordinates.</summary>
public sealed class UiStack : UiElement
{
    public UiOrientation Orientation { get; set; } = UiOrientation.Vertical;
    public float Gap { get; set; } = 4;
    public float Padding { get; set; }

    public override void Arrange(UiRect bounds)
    {
        base.Arrange(bounds);
        if (Children.Count == 0)
        {
            return;
        }

        UiRect content = bounds.Inset(Math.Max(0, Padding));
        float gap = Math.Max(0, Gap);
        float available = Orientation == UiOrientation.Vertical ? content.Height : content.Width;
        float size = Math.Max(0, (available - gap * (Children.Count - 1)) / Children.Count);
        for (int index = 0; index < Children.Count; index++)
        {
            UiRect childBounds = Orientation == UiOrientation.Vertical
                ? new UiRect(content.X, content.Y + index * (size + gap), content.Width, size)
                : new UiRect(content.X + index * (size + gap), content.Y, size, content.Height);
            Children[index].Arrange(childBounds);
        }
    }
}

public sealed class UiGrid : UiElement
{
    private int _columns = 2;

    public int Columns
    {
        get => _columns;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _columns = value;
        }
    }

    public float Gap { get; set; } = 4;
    public float Padding { get; set; }

    public override void Arrange(UiRect bounds)
    {
        base.Arrange(bounds);
        if (Children.Count == 0)
        {
            return;
        }

        UiRect content = bounds.Inset(Math.Max(0, Padding));
        int rows = (Children.Count + Columns - 1) / Columns;
        float gap = Math.Max(0, Gap);
        float width = Math.Max(0, (content.Width - gap * (Columns - 1)) / Columns);
        float height = Math.Max(0, (content.Height - gap * (rows - 1)) / rows);
        for (int index = 0; index < Children.Count; index++)
        {
            Children[index].Arrange(new UiRect(content.X + index % Columns * (width + gap), content.Y + index / Columns * (height + gap), width, height));
        }
    }
}
