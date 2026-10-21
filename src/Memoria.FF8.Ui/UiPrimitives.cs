namespace Memoria.FF8.Ui;

/// <summary>A half-open rectangle: left/top are included; right/bottom are excluded.</summary>
public readonly record struct UiRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(float x, float y)
    {
        return !IsEmpty && x >= X && y >= Y && x < Right && y < Bottom;
    }

    public UiRect Inset(float amount)
    {
        return new UiRect(X + amount, Y + amount, Math.Max(0, Width - amount * 2), Math.Max(0, Height - amount * 2));
    }

    public UiRect Intersect(UiRect other)
    {
        float left = Math.Max(X, other.X);
        float top = Math.Max(Y, other.Y);
        return new UiRect(left, top, Math.Max(0, Math.Min(Right, other.Right) - left), Math.Max(0, Math.Min(Bottom, other.Bottom) - top));
    }
}

public readonly record struct UiColor(byte R, byte G, byte B, byte A = 255);

public readonly record struct UiSize(float Width, float Height);

public readonly record struct UiThickness(float Left, float Top, float Right, float Bottom)
{
    public UiThickness(float all) : this(all, all, all, all)
    {
    }

    public UiThickness(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical)
    {
    }

    public float Horizontal => Left + Right;
    public float Vertical => Top + Bottom;

    public static implicit operator UiThickness(float all)
    {
        return new UiThickness(all);
    }
}

public enum UiAlignment
{
    Stretch,
    Start,
    Center,
    End
}

/// <summary>Single rows scroll horizontally; prose wraps and scrolls vertically when needed.</summary>
public enum UiTextFlow
{
    Auto,
    SingleLine,
    Wrap
}

public readonly record struct UiAnchors(float Left, float Top, float Right, float Bottom)
{
    public static UiAnchors Full => new(0, 0, 1, 1);
}

public enum UiGridUnit
{
    Pixel,
    Star,
    Auto
}

public readonly record struct UiGridLength
{
    private UiGridLength(float value, UiGridUnit unit)
    {
        if (!float.IsFinite(value) || value < 0 || unit == UiGridUnit.Star && value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = value;
        Unit = unit;
    }

    public float Value { get; }
    public UiGridUnit Unit { get; }
    public static UiGridLength Auto => new(0, UiGridUnit.Auto);

    public static UiGridLength Pixel(float pixels)
    {
        return new UiGridLength(pixels, UiGridUnit.Pixel);
    }

    public static UiGridLength Star(float weight = 1)
    {
        return new UiGridLength(weight, UiGridUnit.Star);
    }
}

/// <summary>Renderer-neutral drawing. Clip rectangles must intersect the current clip.</summary>
public interface IUiCanvas
{
    void Fill(UiRect bounds, UiColor color);
    /// <summary>Procedural paper, evaluated at the output resolution by the renderer.</summary>
    void Paper(UiRect bounds, UiColor color)
    {
        Fill(bounds, color);
    }
    void Text(string text, float fontSize, UiRect bounds, UiColor color);
    /// <summary>Positions text horizontally using the canvas's own glyph measurements.</summary>
    void AlignedText(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment horizontalAlignment)
    {
        if (horizontalAlignment is UiAlignment.Start or UiAlignment.Stretch)
        {
            Text(text, fontSize, bounds, color);
            return;
        }
        UiSize measured = MeasureText(text, fontSize, bounds.Width);
        float offset = Math.Max(0, bounds.Width - measured.Width);
        if (horizontalAlignment == UiAlignment.Center)
        {
            offset /= 2;
        }
        Text(text, fontSize, new UiRect(bounds.X + offset, bounds.Y, bounds.Width - offset, bounds.Height), color);
    }

    /// <summary>Measures the same wrapped glyph bounds used by Text, in canvas coordinates.</summary>
    UiSize MeasureText(string text, float fontSize, float maximumWidth)
    {
        throw new NotSupportedException("This canvas does not support text measurement.");
    }
    void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment);
    void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment, UiTextFlow flow)
    {
        Text(text, fontSize, bounds, color, verticalAlignment);
    }
    /// <summary>Vertically centers the visible glyphs and outlines them with the given radius.</summary>
    void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                if (x != 0 || y != 0)
                {
                    Text(text, fontSize, new UiRect(bounds.X + x * thickness, bounds.Y + y * thickness, bounds.Width, bounds.Height), outline, UiAlignment.Center);
                }
            }
        }
        Text(text, fontSize, bounds, color, UiAlignment.Center);
    }
    void MenuPointer(UiRect bounds);
    void Image(UiImageSource source, UiRect bounds);
    /// <summary>Maps artwork to the complete destination, including nonuniform animation scaling.</summary>
    void ImageStretched(UiImageSource source, UiRect bounds)
    {
        Image(source, bounds);
    }
    void Text(string text, float fontSize, UiRect bounds, UiColor color, string? fontFamily)
    {
        Text(text, fontSize, bounds, color);
    }
    void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness);
    void PushClip(UiRect bounds);
    void PopClip();
}

public enum UiKey
{
    Up,
    Down,
    Left,
    Right,
    Confirm,
    Cancel,
    Tab,
    PageUp,
    PageDown
}
