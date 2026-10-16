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
    void Text(string text, float fontSize, UiRect bounds, UiColor color);
    void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment);
    void MenuPointer(UiRect bounds);
    void Image(UiImageSource source, UiRect bounds);
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
