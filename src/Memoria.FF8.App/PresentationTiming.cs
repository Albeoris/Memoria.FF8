using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal sealed class PresentationTiming
{
    public static readonly TimeSpan SimulationStep = TimeSpan.FromSeconds(1d / 60);
    public static readonly TimeSpan MaximumElapsed = TimeSpan.FromMilliseconds(250);
    private TimeSpan accumulated;

    public TimeSpan SimulationTotal { get; private set; }

    public static int FrameLimit(UiPreferences preferences, bool menu, bool battle)
    {
        return menu ? preferences.MenuFps : battle ? preferences.BattleFps : preferences.FieldFps;
    }

    public void Accumulate(TimeSpan elapsed)
    {
        // A stalled renderer cannot create an unbounded simulation catch-up burst.
        if (elapsed < TimeSpan.Zero)
        {
            return;
        }
        TimeSpan boundedElapsed = elapsed > MaximumElapsed ? MaximumElapsed : elapsed;
        accumulated += boundedElapsed;
        if (accumulated > MaximumElapsed)
        {
            accumulated = MaximumElapsed;
        }
    }

    public bool TryStep()
    {
        if (accumulated < SimulationStep)
        {
            return false;
        }
        accumulated -= SimulationStep;
        SimulationTotal += SimulationStep;
        return true;
    }
}
