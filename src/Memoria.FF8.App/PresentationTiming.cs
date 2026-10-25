using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal sealed class PresentationTiming
{
    public static readonly TimeSpan SimulationStep = TimeSpan.FromSeconds(1d / 60);
    public static readonly TimeSpan MaximumElapsed = TimeSpan.FromMilliseconds(250);
    private TimeSpan accumulated;
    private readonly Queue<(long Completed, TimeSpan Duration)> recentUpdateCosts = [];
    private TimeProvider? costClock;

    public TimeSpan SimulationTotal { get; private set; }

    public void DiscardPendingTime()
    {
        accumulated = TimeSpan.Zero;
    }

    public static int FrameLimit(UiPreferences preferences, bool menu, bool battle, bool worldMap = false)
    {
        return menu ? preferences.MenuFps : battle ? preferences.BattleFps : worldMap ? preferences.WorldFps : preferences.FieldFps;
    }

    public static TimeSpan UpdateStep(int frameLimit, double nativeCallbackRate)
    {
        double rate = Math.Max(60, frameLimit == 0 ? nativeCallbackRate : Math.Min(frameLimit, nativeCallbackRate));
        return TimeSpan.FromSeconds(1d / rate);
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
        return TryStep(SimulationStep);
    }

    public void RunUpdates(TimeSpan workBudget, Func<TimeSpan> nextStep, Func<TimeSpan, bool> update, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(workBudget, TimeSpan.Zero);
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        if (!ReferenceEquals(costClock, clock))
        {
            recentUpdateCosts.Clear();
            costClock = clock;
        }
        long started = clock.GetTimestamp();
        bool updated = false;
        while (true)
        {
            if (updated)
            {
                TimeSpan elapsed = clock.GetElapsedTime(started);
                TimeSpan reserve = RecentUpdateReserve(clock);
                // Leave room for a recent expensive callback before admitting more catch-up work.
                // The first update always runs; deferred steps remain available after Draw and fresh input.
                if (elapsed >= workBudget || reserve > workBudget - elapsed)
                {
                    break;
                }
            }
            TimeSpan step = nextStep();
            if (!TryStep(step))
            {
                break;
            }
            long updateStarted = clock.GetTimestamp();
            bool keepUpdating = update(step);
            long completed = clock.GetTimestamp();
            TimeSpan duration = clock.GetElapsedTime(updateStarted, completed);
            if (duration > TimeSpan.Zero)
            {
                ExpireUpdateCosts(clock, completed);
                recentUpdateCosts.Enqueue((completed, duration));
            }
            updated = true;
            if (!keepUpdating || clock.GetElapsedTime(started) >= workBudget)
            {
                // Retain outstanding simulation time while allowing presentation and fresh input.
                break;
            }
        }
    }

    private TimeSpan RecentUpdateReserve(TimeProvider clock)
    {
        long now = clock.GetTimestamp();
        ExpireUpdateCosts(clock, now);
        TimeSpan reserve = TimeSpan.Zero;
        foreach (var sample in recentUpdateCosts)
        {
            if (sample.Duration > reserve)
            {
                reserve = sample.Duration;
            }
        }
        return reserve;
    }

    private void ExpireUpdateCosts(TimeProvider clock, long now)
    {
        while (recentUpdateCosts.TryPeek(out var sample) && clock.GetElapsedTime(sample.Completed, now) >= MaximumElapsed)
        {
            recentUpdateCosts.Dequeue();
        }
    }

    public bool TryStep(TimeSpan step)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(step, TimeSpan.Zero);
        if (accumulated < step)
        {
            return false;
        }
        accumulated -= step;
        SimulationTotal += step;
        return true;
    }
}
