using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class PresentationTiming
{
    public static readonly TimeSpan SimulationStep = TimeSpan.FromSeconds(1d / 60);
    public static readonly TimeSpan MaximumElapsed = TimeSpan.FromMilliseconds(250);
    private TimeSpan accumulated;
    private readonly Queue<(long Completed, TimeSpan Duration)> recentUpdateCosts = [];
    private TimeProvider? costClock;
    private TimeProvider presentationClock = TimeProvider.System;
    private object? presentationOwner;
    private object? presentationFrameOwner;
    private bool presentationFrameCanAdvance;
    private (long Callback, long Generation, ushort? Effect)? presentationSource;
    private long presentationAnchor;
    private bool presentationRunning;
    private double presentationPhase;
    private float lastSampledPhase;
    private double presentationSpeed = 1;

    public TimeSpan SimulationTotal { get; private set; }

    public void DiscardPendingTime()
    {
        accumulated = TimeSpan.Zero;
        FreezePresentationFrame();
    }

    public void BeginPresentationFrame(object? owner, bool advancing, TimeProvider? timeProvider = null)
    {
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        if (!ReferenceEquals(clock, presentationClock) || !ReferenceEquals(owner, presentationFrameOwner))
        {
            ResetPresentationSource();
        }
        presentationClock = clock;
        presentationFrameOwner = owner;
        presentationFrameCanAdvance = advancing && owner is not null;
        if (!presentationFrameCanAdvance)
        {
            FreezePresentationFrame();
        }
    }

    public void ObservePresentationSource(object owner, NativeBattlePreparedPresentation? source, double speed = 1)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!double.IsFinite(speed) || speed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }
        if (speed == 0)
        {
            FreezePresentationFrame();
            return;
        }
        if (source is null || source.PilotEffect is not (2 or 3 or 144))
        {
            ResetPresentationSource();
            return;
        }
        if (!presentationFrameCanAdvance || !ReferenceEquals(owner, presentationFrameOwner))
        {
            return;
        }
        var key = (source.Callback, source.PilotGeneration, source.PilotEffect);
        if (!ReferenceEquals(owner, presentationOwner) || presentationSource != key)
        {
            // The completed source becomes available here, after its native producer returns.
            // Work done before publication is not playback progress for this interval.
            presentationOwner = owner;
            presentationSource = key;
            presentationPhase = 0;
            lastSampledPhase = 0;
        }
        else
        {
            presentationPhase = CurrentPresentationPhase();
        }
        presentationAnchor = presentationClock.GetTimestamp();
        presentationSpeed = speed;
        presentationRunning = true;
    }

    public void FreezePresentationFrame()
    {
        // Freeze the last actually drawn phase, excluding unsampled work and blocked time.
        presentationPhase = lastSampledPhase;
        presentationRunning = false;
        presentationFrameCanAdvance = false;
    }

    public float SamplePresentationPhase(object owner, NativeBattlePreparedPresentation? source, bool advancing, bool smooth = true)
    {
        if (!smooth || source is null || !ReferenceEquals(owner, presentationOwner) || presentationSource != (source.Callback, source.PilotGeneration, source.PilotEffect))
        {
            ResetPresentationSource();
            return 0;
        }
        if (!advancing)
        {
            FreezePresentationFrame();
        }
        lastSampledPhase = (float)CurrentPresentationPhase();
        return lastSampledPhase;
    }

    private double CurrentPresentationPhase()
    {
        double elapsed = presentationRunning ? Math.Max(0, presentationClock.GetElapsedTime(presentationAnchor).TotalSeconds) : 0;
        return Math.Clamp(presentationPhase + elapsed * NativeBattleGraphicsPresentationClock.CallbacksPerSecond * presentationSpeed, 0, 1);
    }

    private void ResetPresentationSource()
    {
        presentationOwner = null;
        presentationSource = null;
        presentationRunning = false;
        presentationPhase = 0;
        lastSampledPhase = 0;
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
                // Reserve the amortized recent cost so an expensive callback does not block cheap catch-up steps for the whole window.
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
        long totalTicks = 0;
        foreach (var sample in recentUpdateCosts)
        {
            totalTicks += sample.Duration.Ticks;
        }
        return recentUpdateCosts.Count == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(totalTicks / recentUpdateCosts.Count);
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
