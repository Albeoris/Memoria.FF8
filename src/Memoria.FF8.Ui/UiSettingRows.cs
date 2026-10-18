using System.Globalization;

namespace Memoria.FF8.Ui;

/// <summary>A label and value sharing one row, with a common proportional column boundary.</summary>
public abstract class UiSettingRow : UiElement
{
    private float _labelWidthRatio = 0.45f;

    protected UiSettingRow()
    {
        Focusable = true;
    }

    public string Text { get; set; } = string.Empty;
    public float FontSize { get; set; }
    public float PointerSize { get; set; } = 10;
    public float TextInset { get; set; } = 24;
    public float RightInset { get; set; } = 8;
    public float LabelWidthRatio
    {
        get => _labelWidthRatio;
        set
        {
            if (!float.IsFinite(value) || value < 0 || value > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _labelWidthRatio = value;
        }
    }

    public UiRect LabelBounds
    {
        get
        {
            float inset = Math.Clamp(TextInset, 0, Bounds.Width * LabelWidthRatio);
            return new UiRect(Bounds.X + inset, Bounds.Y, Math.Max(0, Bounds.Width * LabelWidthRatio - inset), Bounds.Height);
        }
    }

    public UiRect ValueBounds => new(Bounds.X + Bounds.Width * LabelWidthRatio, Bounds.Y, Math.Max(0, Bounds.Width * (1 - LabelWidthRatio) - Math.Max(0, RightInset)), Bounds.Height);

    public override UiSize Measure(UiSize available)
    {
        float size = FontSize > 0 ? FontSize : 16;
        return ResolveDesiredSize(240, size * 1.5f, available);
    }

    protected void DrawLabel(IUiCanvas canvas, UiTheme theme)
    {
        canvas.Text(Text, FontSize > 0 ? FontSize : theme.FontSize, LabelBounds, Enabled ? theme.Foreground : theme.Muted, UiAlignment.Center);
    }

    protected override void DrawPointer(IUiCanvas canvas, UiTheme theme)
    {
        theme.PointerBefore(canvas, LabelBounds.X, Bounds.Y + (Bounds.Height - PointerSize) / 2, PointerSize);
    }
}

/// <summary>A discrete value with all choices visible in aligned, equally spaced columns.</summary>
public sealed class UiChoiceRow : UiSettingRow
{
    private int _selectedIndex;
    private int _maxVisibleItems = 3;

    public IList<string> Items { get; set; } = new List<string>();
    public int MaxVisibleItems
    {
        get => _maxVisibleItems;
        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _maxVisibleItems = value;
        }
    }

    public int SelectedIndex
    {
        get => Items.Count == 0 ? -1 : Math.Clamp(_selectedIndex, 0, Items.Count - 1);
        set => _selectedIndex = Items.Count == 0 ? 0 : Math.Clamp(value, 0, Items.Count - 1);
    }

    public Action<int>? SelectionChanged { get; set; }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        DrawLabel(canvas, theme);
        UiRect values = ValueBounds;
        if (Items.Count == 0 || values.IsEmpty)
        {
            return;
        }

        if (Items.Count > MaxVisibleItems)
        {
            float inset = Math.Min(20, values.Width / 4);
            UiRect option = new(values.X + inset, values.Y, Math.Max(0, values.Width - inset * 2), values.Height);
            canvas.Text(Items[SelectedIndex], FontSize > 0 ? FontSize : theme.FontSize, option, Enabled ? theme.Foreground : theme.Muted, UiAlignment.Center);
            DrawArrow(canvas, values.X + inset / 2, values.Y + values.Height / 2, -1, Enabled && SelectedIndex > 0 ? theme.Foreground : theme.Muted);
            DrawArrow(canvas, values.Right - inset / 2, values.Y + values.Height / 2, 1, Enabled && SelectedIndex < Items.Count - 1 ? theme.Foreground : theme.Muted);
            return;
        }

        float width = values.Width / Items.Count;
        for (int index = 0; index < Items.Count; index++)
        {
            UiRect option = new(values.X + index * width, values.Y, width, values.Height);
            canvas.Text(Items[index], FontSize > 0 ? FontSize : theme.FontSize, option, Enabled && index == SelectedIndex ? theme.Foreground : theme.Muted, UiAlignment.Center);
        }
    }

    public override bool Key(UiKey key)
    {
        if (!Enabled || Items.Count == 0 || key is not (UiKey.Left or UiKey.Right or UiKey.Confirm))
        {
            return false;
        }

        int selected = key == UiKey.Confirm ? (SelectedIndex + 1) % Items.Count : SelectedIndex + (key == UiKey.Left ? -1 : 1);
        SetSelection(selected);
        return true;
    }

    public override void Pointer(float x, float y)
    {
        UiRect values = ValueBounds;
        if (!Enabled || Items.Count == 0 || !values.Contains(x, y))
        {
            return;
        }

        if (Items.Count > MaxVisibleItems)
        {
            SetSelection(SelectedIndex + (x < values.X + values.Width / 2 ? -1 : 1));
        }
        else
        {
            SetSelection((int)((x - values.X) / values.Width * Items.Count));
        }
    }

    private static void DrawArrow(IUiCanvas canvas, float x, float y, int direction, UiColor color)
    {
        canvas.Line(x - direction * 2, y - 3, x + direction * 2, y, color, 1);
        canvas.Line(x + direction * 2, y, x - direction * 2, y + 3, color, 1);
    }

    private void SetSelection(int selected)
    {
        int previous = SelectedIndex;
        SelectedIndex = selected;
        if (previous != SelectedIndex)
        {
            SelectionChanged?.Invoke(SelectedIndex);
        }
    }
}

/// <summary>An inline stepped slider with a beveled purple bar and a persistent numeric value.</summary>
public sealed class UiNumericSlider : UiSlider
{
    public float ValueWidth { get; set; } = 40;
    public float ValueGap { get; set; } = 8;
    public float TrackHeight { get; set; } = 10;
    public string ValueFormat { get; set; } = "0";

    private UiRect NumberBounds
    {
        get
        {
            UiRect values = ValueBounds;
            float width = Math.Clamp(ValueWidth, 0, values.Width);
            return new UiRect(values.Right - width, values.Y, width, values.Height);
        }
    }

    protected override UiRect Track
    {
        get
        {
            UiRect values = ValueBounds;
            float height = Math.Clamp(TrackHeight, 0, values.Height);
            return new UiRect(values.X, values.Y + (values.Height - height) / 2, Math.Max(0, values.Width - NumberBounds.Width - Math.Max(0, ValueGap)), height);
        }
    }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        DrawLabel(canvas, theme);
        UiRect track = Track;
        if (!track.IsEmpty)
        {
            DrawBar(canvas, track, theme);
        }

        canvas.Text(Value.ToString(ValueFormat, CultureInfo.InvariantCulture), FontSize > 0 ? FontSize : theme.FontSize, NumberBounds, Enabled ? theme.Foreground : theme.Muted, UiAlignment.Center);
    }

    protected override void DrawPointer(IUiCanvas canvas, UiTheme theme)
    {
        theme.PointerBefore(canvas, LabelBounds.X, Bounds.Y + (Bounds.Height - PointerSize) / 2, PointerSize);
    }

    private void DrawBar(IUiCanvas canvas, UiRect track, UiTheme theme)
    {
        canvas.Fill(track, theme.BorderDark);
        float bevel = Math.Min(3, Math.Min(track.Width, track.Height) / 2);
        UiRect frame = track.Inset(Math.Min(1, bevel));
        if (!frame.IsEmpty)
        {
            canvas.Fill(frame, theme.BorderLight);
            float edge = Math.Min(1, Math.Min(frame.Width, frame.Height) / 2);
            canvas.Fill(new UiRect(frame.X, frame.Y, frame.Width, edge), new UiColor(248, 248, 248));
            canvas.Fill(new UiRect(frame.X, frame.Y, edge, frame.Height), new UiColor(248, 248, 248));
            canvas.Fill(new UiRect(frame.X, frame.Bottom - edge, frame.Width, edge), new UiColor(112, 112, 128));
            canvas.Fill(new UiRect(frame.Right - edge, frame.Y, edge, frame.Height), new UiColor(112, 112, 128));
        }

        UiRect well = track.Inset(bevel);
        canvas.Fill(well, theme.BorderDark);
        UiRect inner = well.Inset(Math.Min(1, Math.Min(well.Width, well.Height) / 2));
        canvas.Fill(inner, new UiColor(24, 16, 48));
        float ratio = Max == Min ? 0 : (Value - Min) / (Max - Min);
        UiRect fill = new(inner.X, inner.Y, inner.Width * ratio, inner.Height);
        if (!fill.IsEmpty)
        {
            const int bands = 16;
            for (int index = 0; index < bands; index++)
            {
                float position = (float)index / (bands - 1);
                UiColor color = position < 0.3f ? Blend(theme.SliderLight, theme.SliderFill, position / 0.3f) : position < 0.75f ? Blend(theme.SliderFill, theme.SliderDark, (position - 0.3f) / 0.45f) : Blend(theme.SliderDark, theme.SliderLight, (position - 0.75f) / 0.25f);
                float top = fill.Y + fill.Height * index / bands;
                float bottom = fill.Y + fill.Height * (index + 1) / bands;
                canvas.Fill(new UiRect(fill.X, top, fill.Width, bottom - top), Enabled ? color : theme.Muted);
            }
        }
    }

    private static UiColor Blend(UiColor from, UiColor to, float ratio)
    {
        return new UiColor((byte)MathF.Round(from.R + (to.R - from.R) * ratio), (byte)MathF.Round(from.G + (to.G - from.G) * ratio), (byte)MathF.Round(from.B + (to.B - from.B) * ratio));
    }

    public override void Pointer(float x, float y)
    {
        if (ValueBounds.Contains(x, y) && x < Track.Right)
        {
            base.Pointer(x, y);
        }
    }
}
