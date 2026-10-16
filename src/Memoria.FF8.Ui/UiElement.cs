namespace Memoria.FF8.Ui;

public class UiElement
{
    public UiRect Bounds { get; protected set; }
    public string? Id { get; set; }
    public IList<UiElement> Children { get; } = new List<UiElement>();
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public bool Focusable { get; set; }
    public bool IsFocused { get; internal set; }
    public Action<bool>? FocusChanged { get; set; }
    public UiThickness Margin { get; set; }
    public UiAlignment HorizontalAlignment { get; set; } = UiAlignment.Stretch;
    public UiAlignment VerticalAlignment { get; set; } = UiAlignment.Stretch;
    public float? Width { get; set; }
    public float? Height { get; set; }
    public int GridRow { get; set; }
    public int GridColumn { get; set; }
    public int GridRowSpan { get; set; } = 1;
    public int GridColumnSpan { get; set; } = 1;
    public UiAnchors Anchors { get; set; } = UiAnchors.Full;

    public virtual UiSize Measure(UiSize available)
    {
        float width = 0;
        float height = 0;
        foreach (UiElement child in Children.Where(child => child.Visible))
        {
            UiSize desired = UiLayout.MeasureOuter(child, available);
            width = Math.Max(width, desired.Width);
            height = Math.Max(height, desired.Height);
        }

        return ResolveDesiredSize(width, height, available);
    }

    protected UiSize ResolveDesiredSize(float width, float height, UiSize available)
    {
        return new UiSize(Math.Clamp(Width ?? width, 0, Math.Max(0, available.Width)), Math.Clamp(Height ?? height, 0, Math.Max(0, available.Height)));
    }

    public virtual void Arrange(UiRect bounds)
    {
        Bounds = bounds;
        foreach (UiElement child in Children)
        {
            UiLayout.ArrangeChild(child, bounds);
        }
    }

    public virtual void Draw(IUiCanvas canvas, UiTheme theme)
    {
        if (!Visible || Bounds.IsEmpty)
        {
            return;
        }

        canvas.PushClip(Bounds);
        try
        {
            DrawSelf(canvas, theme);
            foreach (UiElement child in Children)
            {
                child.Draw(canvas, theme);
            }
        }
        finally
        {
            canvas.PopClip();
        }
    }

    protected virtual void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
    }

    internal virtual void DrawAdornments(IUiCanvas canvas, UiTheme theme)
    {
        if (!Visible || Bounds.IsEmpty)
        {
            return;
        }

        foreach (UiElement child in Children)
        {
            child.DrawAdornments(canvas, theme);
        }
    }

    public virtual UiElement? HitTest(float x, float y)
    {
        if (!Visible || !Enabled || !Bounds.Contains(x, y))
        {
            return null;
        }

        for (int index = Children.Count - 1; index >= 0; index--)
        {
            UiElement? hit = Children[index].HitTest(x, y);
            if (hit != null)
            {
                return hit;
            }
        }

        return this;
    }

    public virtual bool Key(UiKey key)
    {
        return false;
    }

    public virtual void Pointer(float x, float y)
    {
    }
}

public sealed class UiPanel : UiElement
{
    public string? Caption { get; set; }
    public bool DrawBackground { get; set; } = true;
    public UiThickness Padding { get; set; }
    public float CaptionHeight { get; set; } = 32;
    public float CaptionFontSize { get; set; }

    public override UiSize Measure(UiSize available)
    {
        float caption = string.IsNullOrEmpty(Caption) ? 0 : Math.Max(0, CaptionHeight);
        UiSize inner = new(Math.Max(0, available.Width - Padding.Horizontal), Math.Max(0, available.Height - Padding.Vertical - caption));
        UiSize content = base.Measure(inner);
        return ResolveDesiredSize(content.Width + Padding.Horizontal, content.Height + Padding.Vertical + caption, available);
    }

    public override void Arrange(UiRect bounds)
    {
        Bounds = bounds;
        UiRect content = UiLayout.Inset(bounds, Padding);
        float caption = string.IsNullOrEmpty(Caption) ? 0 : Math.Min(content.Height, Math.Max(0, CaptionHeight));
        content = new UiRect(content.X, content.Y + caption, content.Width, content.Height - caption);
        foreach (UiElement child in Children)
        {
            UiLayout.ArrangeChild(child, content);
        }
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        if (DrawBackground)
        {
            theme.Panel(canvas, Bounds);
        }
    }

    internal override void DrawAdornments(IUiCanvas canvas, UiTheme theme)
    {
        if (!Visible || Bounds.IsEmpty)
        {
            return;
        }

        if (DrawBackground)
        {
            theme.Caption(canvas, Bounds, Caption, CaptionFontSize);
        }
        base.DrawAdornments(canvas, theme);
    }
}

public sealed class UiLabel : UiElement
{
    public string Text { get; set; } = string.Empty;
    public float FontSize { get; set; } = 16;
    public bool Muted { get; set; }

    public override UiSize Measure(UiSize available)
    {
        string[] lines = Text.Split('\n');
        return ResolveDesiredSize(lines.Max(line => line.Length) * FontSize * 0.6f, lines.Length * FontSize * 1.5f, available);
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        canvas.Text(Text, FontSize, Bounds, Muted || !Enabled ? theme.Muted : theme.Foreground);
    }
}

public sealed class UiButton : UiElement
{
    public UiButton()
    {
        Focusable = true;
    }

    public string Text { get; set; } = string.Empty;
    public Action? Click { get; set; }
    public bool DrawBackground { get; set; } = true;
    public bool HighlightSelection { get; set; } = true;
    public float FontSize { get; set; }
    public float PointerSize { get; set; } = 10;

    public override UiSize Measure(UiSize available)
    {
        return ResolveDesiredSize(Math.Max(80, Text.Length * 10 + 32), 40, available);
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        if (DrawBackground)
        {
            theme.Panel(canvas, Bounds);
        }
        if (IsFocused && Enabled && HighlightSelection)
        {
            canvas.Fill(Bounds.Inset(3), theme.Highlight);
        }

        if (IsFocused && Enabled)
        {
            theme.Pointer(canvas, Bounds.X + 7, Bounds.Y + (Bounds.Height - PointerSize) / 2, PointerSize);
        }

        canvas.Text(Text, FontSize > 0 ? FontSize : theme.FontSize, new UiRect(Bounds.X + 24, Bounds.Y, Math.Max(0, Bounds.Width - 32), Bounds.Height), Enabled ? theme.Foreground : theme.Muted, UiAlignment.Center);
    }

    public override bool Key(UiKey key)
    {
        if (!Enabled || key != UiKey.Confirm)
        {
            return false;
        }

        Click?.Invoke();
        return true;
    }

    public override void Pointer(float x, float y)
    {
        if (Enabled && Bounds.Contains(x, y))
        {
            Click?.Invoke();
        }
    }
}
