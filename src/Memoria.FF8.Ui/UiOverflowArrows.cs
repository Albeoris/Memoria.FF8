using System.Diagnostics;

namespace Memoria.FF8.Ui;

/// <summary>Reserves centered overflow indicators above and below a retained content area.</summary>
public sealed class UiOverflowArrows : UiElement
{
    private readonly Stopwatch animation = Stopwatch.StartNew();

    public bool ShowUp { get; set; }
    public bool ShowDown { get; set; }
    public float ArrowHeight { get; set; } = 12;
    public Func<double>? AnimationSeconds { get; set; }

    public UiRect ContentBounds
    {
        get
        {
            float band = Math.Clamp(ArrowHeight, 0, Bounds.Height / 2);
            return new UiRect(Bounds.X, Bounds.Y + band, Bounds.Width, Math.Max(0, Bounds.Height - band * 2));
        }
    }

    public override UiSize Measure(UiSize available)
    {
        float band = Math.Clamp(ArrowHeight, 0, available.Height / 2);
        UiSize content = base.Measure(new UiSize(available.Width, Math.Max(0, available.Height - band * 2)));
        return ResolveDesiredSize(content.Width, content.Height + band * 2, available);
    }

    public override void Arrange(UiRect bounds)
    {
        Bounds = bounds;
        foreach (UiElement child in Children)
        {
            UiLayout.ArrangeChild(child, ContentBounds);
        }
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        double seconds = AnimationSeconds?.Invoke() ?? animation.Elapsed.TotalSeconds;
        UiColor color = ((long)(seconds * 60) & 2) != 0 ? theme.Foreground : theme.Muted;
        float band = ContentBounds.Y - Bounds.Y;
        if (ShowUp)
        {
            DrawArrow(canvas, new UiRect(Bounds.X, Bounds.Y, Bounds.Width, band), false, color);
        }
        if (ShowDown)
        {
            DrawArrow(canvas, new UiRect(Bounds.X, ContentBounds.Bottom, Bounds.Width, band), true, color);
        }
    }

    private static void DrawArrow(IUiCanvas canvas, UiRect bounds, bool down, UiColor color)
    {
        float height = Math.Min(bounds.Height * 0.7f, bounds.Width / 2);
        float top = bounds.Y + (bounds.Height - height) / 2;
        float center = bounds.X + bounds.Width / 2;
        int bands = Math.Max(1, (int)MathF.Ceiling(height));
        for (int index = 0; index < bands; index++)
        {
            float fraction = (index + 1f) / bands;
            float halfWidth = height * (down ? 1 - (float)index / bands : fraction);
            canvas.Fill(new UiRect(center - halfWidth, top + height * index / bands, halfWidth * 2, height / bands), color);
        }
    }
}
