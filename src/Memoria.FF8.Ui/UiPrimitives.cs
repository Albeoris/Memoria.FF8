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

/// <summary>Renderer-neutral drawing. Clip rectangles must intersect the current clip.</summary>
public interface IUiCanvas
{
    void Fill(UiRect bounds, UiColor color);
    void Text(string text, float fontSize, UiRect bounds, UiColor color);
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
