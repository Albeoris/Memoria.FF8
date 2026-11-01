namespace Memoria.FF8.Ui;

/// <summary>A clipped vertical viewport for retained controls, with a persistent scroll indicator.</summary>
public sealed class UiScrollPanel : UiElement
{
    private float contentHeight;
    private float offset;

    public UiScrollPanel()
    {
        Focusable = true;
    }

    public float ContentHeight
    {
        get => contentHeight;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (!float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            contentHeight = value;
            Arrange(Bounds);
        }
    }

    public float Offset
    {
        get => offset;
        set
        {
            if (!float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            float next = Math.Clamp(value, 0, MaximumOffset);
            if (next == offset)
            {
                return;
            }
            offset = next;
            ArrangeContent();
            OffsetChanged?.Invoke(offset);
        }
    }

    public float MaximumOffset => Math.Max(0, ContentHeight - Bounds.Height);
    public float ScrollbarWidth { get; set; } = 10;
    public Action<float>? OffsetChanged { get; set; }

    public void ScrollBy(float amount) => Offset += amount;

    public override bool Key(UiKey key)
    {
        if (MaximumOffset <= 0 || key is not (UiKey.PageUp or UiKey.PageDown))
        {
            return false;
        }
        ScrollBy((key == UiKey.PageUp ? -1 : 1) * Bounds.Height * 0.8f);
        return true;
    }

    public bool EnsureVisible(UiElement element)
    {
        if (!IsDescendant(element) || !element.Visible || Bounds.IsEmpty)
        {
            return false;
        }
        float previous = Offset;
        if (element.Bounds.Y < Bounds.Y)
        {
            Offset -= Bounds.Y - element.Bounds.Y;
        }
        else if (element.Bounds.Bottom > Bounds.Bottom)
        {
            Offset += Math.Min(element.Bounds.Bottom - Bounds.Bottom, element.Bounds.Y - Bounds.Y);
        }
        return previous != Offset;
    }

    public override void Arrange(UiRect bounds)
    {
        base.Arrange(bounds);
        float previous = offset;
        offset = Math.Clamp(offset, 0, MaximumOffset);
        ArrangeContent();
        if (previous != offset)
        {
            OffsetChanged?.Invoke(offset);
        }
    }

    private void ArrangeContent()
    {
        float scrollbar = MaximumOffset > 0 ? Math.Clamp(ScrollbarWidth, 0, Bounds.Width) : 0;
        UiRect content = new(Bounds.X, Bounds.Y - Offset, Math.Max(0, Bounds.Width - scrollbar), Math.Max(Bounds.Height, ContentHeight));
        foreach (UiElement child in Children)
        {
            UiLayout.ArrangeChild(child, content);
        }
    }

    public override void Draw(IUiCanvas canvas, UiTheme theme)
    {
        base.Draw(canvas, theme);
        if (!Visible || Bounds.IsEmpty || MaximumOffset <= 0)
        {
            return;
        }
        float width = Math.Clamp(ScrollbarWidth, 0, Bounds.Width);
        UiRect track = new(Bounds.Right - width, Bounds.Y, width, Bounds.Height);
        float height = Math.Min(Bounds.Height, Math.Max(12, Bounds.Height * Bounds.Height / ContentHeight));
        float y = Bounds.Y + (Bounds.Height - height) * Offset / MaximumOffset;
        canvas.PushClip(Bounds);
        try
        {
            canvas.Fill(track, theme.BorderDark);
            canvas.Fill(new UiRect(track.X + Math.Min(2, width / 2), y, Math.Max(0, width - 4), height), theme.Foreground);
        }
        finally
        {
            canvas.PopClip();
        }
    }

    internal override void DrawAdornments(IUiCanvas canvas, UiTheme theme)
    {
        canvas.PushClip(Bounds);
        try
        {
            base.DrawAdornments(canvas, theme);
        }
        finally
        {
            canvas.PopClip();
        }
    }

    public override UiElement? HitTest(float x, float y)
    {
        if (!Visible || !Enabled || !Bounds.Contains(x, y))
        {
            return null;
        }
        if (MaximumOffset > 0 && x >= Bounds.Right - Math.Clamp(ScrollbarWidth, 0, Bounds.Width))
        {
            return this;
        }
        return base.HitTest(x, y);
    }

    public override void Pointer(float x, float y)
    {
        if (MaximumOffset > 0 && Bounds.Contains(x, y) && x >= Bounds.Right - Math.Clamp(ScrollbarWidth, 0, Bounds.Width))
        {
            float height = Math.Min(Bounds.Height, Math.Max(12, Bounds.Height * Bounds.Height / ContentHeight));
            float travel = Bounds.Height - height;
            Offset = travel <= 0 ? 0 : (y - Bounds.Y - height / 2) / travel * MaximumOffset;
        }
    }

    private bool IsDescendant(UiElement element)
    {
        return Children.Any(child => ReferenceEquals(child, element) || Contains(child, element));
    }

    private static bool Contains(UiElement parent, UiElement element) => parent.Children.Any(child => ReferenceEquals(child, element) || Contains(child, element));
}
