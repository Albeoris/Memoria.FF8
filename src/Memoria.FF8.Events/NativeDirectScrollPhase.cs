namespace Memoria.FF8.Events;

/// <summary>Original475DF0 status0/mode3 publication, shared by the real step and detached next-draw admission.</summary>
public static class NativeDirectScrollPhase
{
    public static BackgroundScrollPosition Commit(BackgroundScrollPosition state)
    {
        if (state.Mode != 3 || state.Status != 0)
        {
            throw new ArgumentException("Direct scroll publication requires its pending mode3/status0 record.", nameof(state));
        }
        return state with { PreviousX = state.X, PreviousY = state.Y, Counter = 0, X = state.TargetX, Y = state.TargetY, Status = 2 };
    }
}
