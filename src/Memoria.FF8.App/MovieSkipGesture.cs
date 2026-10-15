namespace Memoria.FF8;

/// <summary>Presentation-only hold gesture; the accepted skip uses the ordinary game action.</summary>
internal sealed class MovieSkipGesture
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(3);
    private TimeSpan held;
    private TimeSpan remaining;
    private bool completed;

    public bool Visible => remaining > TimeSpan.Zero;
    public float Progress => (float)(held.TotalSeconds / Duration.TotalSeconds);

    public bool Update(TimeSpan elapsed, bool cancelHeld)
    {
        if (completed)
        {
            return false;
        }
        if (cancelHeld)
        {
            remaining = Duration;
            held = TimeSpan.FromTicks(Math.Min(Duration.Ticks, held.Ticks + elapsed.Ticks));
            if (held >= Duration)
            {
                completed = true;
                return true;
            }
        }
        else
        {
            held = TimeSpan.Zero;
            remaining = TimeSpan.FromTicks(Math.Max(0, remaining.Ticks - elapsed.Ticks));
        }
        return false;
    }

    public void Reset()
    {
        held = TimeSpan.Zero;
        remaining = TimeSpan.Zero;
        completed = false;
    }
}
