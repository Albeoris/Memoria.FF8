namespace Memoria.FF8.Ui;

/// <summary>Retains overflow animation across rebuilt views, using the presentation clock.</summary>
public sealed class UiTextOverflow
{
    private readonly Dictionary<(UiRect Bounds, float Size), Entry> entries = [];
    private long frame;
    private double seconds;

    public void BeginFrame(TimeSpan time)
    {
        foreach (var key in entries.Where(pair => pair.Value.Frame != frame).Select(pair => pair.Key).ToArray())
        {
            entries.Remove(key);
        }
        frame++;
        seconds = time.TotalSeconds;
    }

    public UiTextViewport Layout(string text, float size, UiRect bounds, string? fontFamily, Func<float, UiSize> measure)
    {
        var key = (bounds, size);
        if (!entries.TryGetValue(key, out Entry? entry) || entry.Text != text || entry.FontFamily != fontFamily)
        {
            UiSize content = measure(bounds.Width);
            float wrapWidth = bounds.Width;
            bool horizontal = false;
            if (content.Height > bounds.Height && !text.Contains('\n') && !text.Contains('\r'))
            {
                UiSize line = measure(float.PositiveInfinity);
                if (line.Width > bounds.Width)
                {
                    content = line;
                    wrapWidth = float.PositiveInfinity;
                    horizontal = true;
                }
            }
            entry = new Entry(text, fontFamily, content, wrapWidth, horizontal, seconds);
            entries[key] = entry;
        }
        entry.Frame = frame;
        float distance = 0;
        if (entry.Horizontal)
        {
            distance = entry.Content.Width - bounds.Width;
        }
        else if (text.Contains('\n') || text.Contains('\r'))
        {
            distance = entry.Content.Height - bounds.Height;
        }
        float offset = Offset(seconds - entry.Start, distance, size);
        return new UiTextViewport(entry.Horizontal ? offset : 0, entry.Horizontal ? 0 : offset, entry.WrapWidth, entry.Content);
    }

    /// <summary>Pause at both ends, then restart; speed scales with the text rather than the output resolution.</summary>
    public static float Offset(double elapsedSeconds, float distance, float fontSize)
    {
        if (distance <= 0 || fontSize <= 0)
        {
            return 0;
        }
        const double startPause = 1.5;
        const double endPause = 2;
        double speed = fontSize * 1.5;
        double travel = distance / speed;
        double phase = Math.Max(0, elapsedSeconds) % (startPause + travel + endPause);
        return (float)Math.Clamp((phase - startPause) * speed, 0, distance);
    }

    private sealed class Entry(string text, string? fontFamily, UiSize content, float wrapWidth, bool horizontal, double start)
    {
        public string Text { get; } = text;
        public string? FontFamily { get; } = fontFamily;
        public UiSize Content { get; } = content;
        public float WrapWidth { get; } = wrapWidth;
        public bool Horizontal { get; } = horizontal;
        public double Start { get; } = start;
        public long Frame { get; set; }
    }
}

public readonly record struct UiTextViewport(float OffsetX, float OffsetY, float WrapWidth, UiSize Content);
