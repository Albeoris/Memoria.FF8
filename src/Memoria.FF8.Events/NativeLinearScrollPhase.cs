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
        if (state.Mode != 4 || state.NativeLinearVersion != 1 || state.Status is not (0 or 1 or 2) || state.Duration is not short duration || duration < 0 || state.X is null || state.Y is null)
        {
            return false;
        }
        if (state.Status == 0)
        {
            // The command preserves previous/counter until post-VM475DF0.
            return true;
        }
        if (state.Counter is not short counter || state.PreviousX is not short previousX || state.PreviousY is not short previousY || counter < 0 || counter > duration || state.Status == 2 && counter != duration || state.Status == 1 && counter == 0)
        {
            return false;
        }
        short elapsed = (short)(state.Status == 2 ? counter : counter - 1);
        return state.X == Interpolate(previousX, state.TargetX, duration, elapsed) && state.Y == Interpolate(previousY, state.TargetY, duration, elapsed);
    }
}
