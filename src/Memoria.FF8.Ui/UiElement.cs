namespace Memoria.FF8.Ui;

public class UiElement
{
    public UiRect Bounds { get; set; }
    public string? Id { get; set; }
    public IList<UiElement> Children { get; } = new List<UiElement>();
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public bool Focusable { get; set; }
    public bool IsFocused { get; internal set; }

    public virtual void Arrange(UiRect bounds)
    {
        Bounds = bounds;
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

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        if (DrawBackground)
        {
            theme.Panel(canvas, Bounds, Caption);
        }
    }
}

public sealed class UiLabel : UiElement
{
    public string Text { get; set; } = string.Empty;
    public float FontSize { get; set; } = 16;
    public bool Muted { get; set; }

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

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        theme.Panel(canvas, Bounds);
        if (IsFocused && Enabled)
        {
            canvas.Fill(Bounds.Inset(3), theme.Highlight);
        }

        if (IsFocused && Enabled)
        {
            theme.Pointer(canvas, Bounds.X + 7, Bounds.Y + (Bounds.Height - 10) / 2);
        }

        canvas.Text(Text, theme.FontSize, new UiRect(Bounds.X + 24, Bounds.Y + 8, Math.Max(0, Bounds.Width - 32), Math.Max(0, Bounds.Height - 16)), Enabled ? theme.Foreground : theme.Muted);
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
