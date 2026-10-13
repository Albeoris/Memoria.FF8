namespace Memoria.FF8.Events;

public static class NativeLinearScrollPhase
{
    public static short Interpolate(short previous, short target, short duration, short counter)
    {
        // Native475E60 signed IMUL/IDIV, followed by WORD storage.
        return duration == 0 ? previous : (short)(previous + counter * (target - previous) / duration);
    }

    public static bool IsValid(BackgroundScrollPosition state)
    {
        if (state.Mode != 4 || state.NativeLinearVersion != 1 || state.Status is not (0 or 1 or 2) || state.Duration < 0)
        {
            return false;
        }
        if (state.Status == 0)
        {
            // The command preserves previous/counter until post-VM475DF0.
            return true;
        }
        if (state.Counter < 0 || state.Counter > state.Duration || state.Status == 2 && state.Counter != state.Duration || state.Status == 1 && state.Counter == 0)
        {
            return false;
        }
        short elapsed = (short)(state.Status == 2 ? state.Counter : state.Counter - 1);
        return state.X == Interpolate(state.PreviousX, state.TargetX, state.Duration, elapsed) && state.Y == Interpolate(state.PreviousY, state.TargetY, state.Duration, elapsed);
    }
}
