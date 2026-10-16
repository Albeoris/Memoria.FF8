namespace Memoria.FF8.Ui;

public sealed class UiToggle : UiElement
{
    public UiToggle()
    {
        Focusable = true;
    }

    public string Text { get; set; } = string.Empty;
    public bool Value { get; set; }
    public Action<bool>? ValueChanged { get; set; }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        if (IsFocused && Enabled)
        {
            canvas.Fill(Bounds.Inset(1), theme.Highlight);
            theme.Pointer(canvas, Bounds.X + 4, Bounds.Y + (Bounds.Height - 10) / 2);
        }

        UiColor color = Enabled ? theme.Foreground : theme.Muted;
        canvas.Text(Text, theme.FontSize, new UiRect(Bounds.X + 22, Bounds.Y, Math.Max(0, Bounds.Width - 94), Bounds.Height), color, UiAlignment.Center);
        canvas.Text(Value ? "On" : "Off", theme.FontSize, new UiRect(Bounds.Right - 65, Bounds.Y, 60, Bounds.Height), color, UiAlignment.Center);
    }

    public override bool Key(UiKey key)
    {
        if (!Enabled || key is not (UiKey.Confirm or UiKey.Left or UiKey.Right))
        {
            return false;
        }

        SetValue(key == UiKey.Left ? false : key == UiKey.Right || !Value);
        return true;
    }

    public override void Pointer(float x, float y)
    {
        if (Enabled && Bounds.Contains(x, y))
        {
            SetValue(!Value);
        }
    }

    private void SetValue(bool value)
    {
        if (Value == value)
        {
            return;
        }

        Value = value;
        ValueChanged?.Invoke(value);
    }
}

public class UiSlider : UiSettingRow
{
    private float _min;
    private float _max = 1;
    private float _step = 0.1f;
    private float _value;

    public UiSlider()
    {
        Focusable = true;
    }

    public float Min
    {
        get => _min;
        set
        {
            if (!float.IsFinite(value) || value > _max)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _min = value;
            _value = Math.Clamp(_value, _min, _max);
        }
    }

    public float Max
    {
        get => _max;
        set
        {
            if (!float.IsFinite(value) || value < _min)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _max = value;
            _value = Math.Clamp(_value, _min, _max);
        }
    }

    public float Step
    {
        get => _step;
        set
        {
            if (!float.IsFinite(value) || value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _step = value;
        }
    }

    public float Value
    {
        get => _value;
        set
        {
            if (!float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _value = Math.Clamp(value, Min, Max);
        }
    }

    public Action<float>? ValueChanged { get; set; }
    protected virtual UiRect Track => new(Bounds.X + 24, Bounds.Y + Bounds.Height * 0.7f, Math.Max(0, Bounds.Width - 38), 4);

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        UiColor color = Enabled ? theme.Foreground : theme.Muted;
        if (IsFocused && Enabled)
        {
            theme.Pointer(canvas, Bounds.X + 4, Bounds.Y + 7);
        }

        canvas.Text(Text, theme.FontSize, new UiRect(Bounds.X + 24, Bounds.Y + 1, Math.Max(0, Bounds.Width - 38), Bounds.Height * 0.6f), color);
        UiRect track = Track;
        canvas.Fill(track, theme.BorderDark);
        float ratio = Max == Min ? 0 : (Value - Min) / (Max - Min);
        canvas.Fill(new UiRect(track.X, track.Y, track.Width * ratio, track.Height), theme.Muted);
        canvas.Fill(new UiRect(track.X + Math.Max(0, track.Width - 6) * ratio, track.Y - 3, Math.Min(6, track.Width), 10), color);
    }

    public override bool Key(UiKey key)
    {
        if (!Enabled || key is not (UiKey.Left or UiKey.Right))
        {
            return false;
        }

        SetValue(Value + (key == UiKey.Left ? -Step : Step));
        return true;
    }

    public override void Pointer(float x, float y)
    {
        if (!Enabled || !Bounds.Contains(x, y) || Track.Width <= 0)
        {
            return;
        }

        float value = Min + Math.Clamp((x - Track.X) / Track.Width, 0, 1) * (Max - Min);
        SetValue(Min + MathF.Round((value - Min) / Step) * Step);
    }

    private void SetValue(float value)
    {
        float previous = Value;
        Value = value;
        if (previous != Value)
        {
            ValueChanged?.Invoke(Value);
        }
    }
}

public sealed class UiProgressBar : UiElement
{
    public float Min { get; set; }
    public float Max { get; set; } = 1;
    public float Value { get; set; }
    public string? Text { get; set; }

    protected override void DrawSelf(IUiCanvas canvas, UiTheme theme)
    {
        UiRect track = Bounds.Inset(1);
        canvas.Fill(Bounds, theme.BorderDark);
        canvas.Fill(track, new UiColor(60, 60, 60));
        float ratio = float.IsFinite(Value) && float.IsFinite(Min) && float.IsFinite(Max) && Max > Min ? Math.Clamp((Value - Min) / (Max - Min), 0, 1) : 0;
        canvas.Fill(new UiRect(track.X, track.Y, track.Width * ratio, track.Height), Enabled ? theme.Highlight : theme.Muted);
        if (!string.IsNullOrEmpty(Text))
        {
            canvas.Text(Text, theme.FontSize, track, theme.Foreground);
        }
    }
}
