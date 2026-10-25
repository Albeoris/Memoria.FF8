using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class PresentationTimingTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Native_battle_clock_is_independent_of_display_cap_and_cap_changes(int displayCap)
    {
        PresentationTiming timing = new();
        ManualTimeProvider costClock = new();
        Memoria.FF8.Runtime.NativeBattleGraphicsPresentationStep native = new(0, 0, false);
        long callbacks = 0;
        long previous = 0;
        for (int display = 1; display <= 252; display++)
        {
            int cap = displayCap == -1 ? display < 84 ? 30 : display < 168 ? 60 : 120 : displayCap;
            // The display schedule may change; accumulated elapsed time remains authoritative.
            long timestamp = (long)display * TimeSpan.TicksPerSecond / 120;
            timing.Accumulate(TimeSpan.FromTicks(timestamp - previous));
            previous = timestamp;
            timing.RunUpdates(TimeSpan.FromSeconds(1d / (cap == 0 ? 120 : cap)), () => PresentationTiming.UpdateStep(cap, Memoria.FF8.Runtime.NativeBattleGraphicsPresentationClock.CallbacksPerSecond), step =>
            {
                native = Memoria.FF8.Runtime.NativeBattleGraphicsPresentationClock.Advance(native.Subticks, native.HalfSubtick, step);
                callbacks += native.Callbacks;
                return true;
            }, costClock);
        }
        var expected = Memoria.FF8.Runtime.NativeBattleGraphicsPresentationClock.Advance(0, false, timing.SimulationTotal);
        Assert.Equal(32, callbacks);
        Assert.Equal(expected, new Memoria.FF8.Runtime.NativeBattleGraphicsPresentationStep(callbacks, native.Subticks, native.HalfSubtick));
        Assert.Equal(PresentationTiming.SimulationStep * 126, timing.SimulationTotal);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(0)]
    public void Slow_updates_yield_each_display_without_losing_the_remaining_callbacks(int frameLimit)
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        timing.Accumulate(TimeSpan.FromMilliseconds(200));
        TimeSpan budget = frameLimit == 0 ? PresentationTiming.SimulationStep : TimeSpan.FromSeconds(1d / frameLimit);
        List<TimeSpan> callbacks = [];
        for (int display = 1; display <= 12; display++)
        {
            int before = callbacks.Count;
            timing.RunUpdates(budget, () => PresentationTiming.SimulationStep, step =>
            {
                Assert.Equal(PresentationTiming.SimulationStep, step);
                callbacks.Add(timing.SimulationTotal);
                clock.Advance(budget + TimeSpan.FromMilliseconds(1));
                return true;
            }, clock);
            Assert.Equal(before + 1, callbacks.Count);
            Assert.Equal(PresentationTiming.SimulationStep * display, callbacks[^1]);
        }
        Assert.False(timing.TryStep());
    }

    [Fact]
    public void Alternating_expensive_and_light_callbacks_leave_a_draw_between_updates_without_consuming_deferred_steps()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        TimeSpan budget = PresentationTiming.SimulationStep;
        timing.Accumulate(PresentationTiming.SimulationStep * 8);
        int updates = 0;
        List<string> dispatch = [];
        for (int display = 1; display <= 8; display++)
        {
            timing.RunUpdates(budget, () => PresentationTiming.SimulationStep, step =>
            {
                dispatch.Add("update");
                updates++;
                Assert.Equal(PresentationTiming.SimulationStep * updates, timing.SimulationTotal);
                clock.Advance(TimeSpan.FromMilliseconds(updates % 2 == 1 ? 25 : 3));
                return true;
            }, clock);
            dispatch.Add("draw");
            Assert.Equal(display, updates);
            Assert.Equal(PresentationTiming.SimulationStep * display, timing.SimulationTotal);
            clock.Advance(TimeSpan.FromMilliseconds(1));
        }
        Assert.Equal(Enumerable.Range(0, 8).SelectMany(_ => new[] { "update", "draw" }), dispatch);
        Assert.False(timing.TryStep());
    }

    [Fact]
    public void Two_light_callbacks_do_not_forget_the_preceding_expensive_callback()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        timing.Accumulate(PresentationTiming.SimulationStep * 9);
        int updates = 0;
        for (int display = 1; display <= 9; display++)
        {
            timing.RunUpdates(PresentationTiming.SimulationStep, () => PresentationTiming.SimulationStep, step =>
            {
                updates++;
                clock.Advance(TimeSpan.FromMilliseconds(updates % 3 == 1 ? 25 : 3));
                return true;
            }, clock);
            Assert.Equal(display, updates);
            clock.Advance(TimeSpan.FromMilliseconds(1));
        }
        Assert.Equal(PresentationTiming.SimulationStep * 9, timing.SimulationTotal);
        Assert.False(timing.TryStep());
    }

    [Fact]
    public void Expensive_history_expires_by_elapsed_time_and_remaining_steps_catch_up_within_budget()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        timing.Accumulate(PresentationTiming.SimulationStep * 12);
        List<int> updatesPerDisplay = [];
        int updates = 0;
        for (int display = 0; display < 4; display++)
        {
            if (display == 2)
            {
                clock.Advance(PresentationTiming.MaximumElapsed);
            }
            int before = updates;
            TimeSpan started = clock.GetElapsedTime(0);
            timing.RunUpdates(PresentationTiming.SimulationStep, () => PresentationTiming.SimulationStep, step =>
            {
                updates++;
                clock.Advance(TimeSpan.FromMilliseconds(updates == 1 ? 25 : 3));
                return true;
            }, clock);
            updatesPerDisplay.Add(updates - before);
            if (display >= 2)
            {
                Assert.True(clock.GetElapsedTime(0) - started <= PresentationTiming.SimulationStep);
            }
        }
        Assert.Equal(new[] { 1, 1, 5, 5 }, updatesPerDisplay);
        Assert.Equal(PresentationTiming.SimulationStep * 12, timing.SimulationTotal);
        Assert.False(timing.TryStep());
    }

    [Fact]
    public void Recent_cost_expires_at_the_wall_time_boundary_even_without_intervening_updates()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        timing.Accumulate(PresentationTiming.SimulationStep * 5);
        int updates = 0;
        timing.RunUpdates(PresentationTiming.SimulationStep, () => PresentationTiming.SimulationStep, step =>
        {
            updates++;
            clock.Advance(TimeSpan.FromMilliseconds(25));
            return true;
        }, clock);
        clock.Advance(PresentationTiming.MaximumElapsed - TimeSpan.FromMilliseconds(1));
        timing.RunUpdates(PresentationTiming.SimulationStep, () => PresentationTiming.SimulationStep, step =>
        {
            updates++;
            return true;
        }, clock);
        Assert.Equal(2, updates);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        timing.RunUpdates(PresentationTiming.SimulationStep, () => PresentationTiming.SimulationStep, step =>
        {
            updates++;
            return true;
        }, clock);
        Assert.Equal(5, updates);
        Assert.Equal(PresentationTiming.SimulationStep * 5, timing.SimulationTotal);
        Assert.False(timing.TryStep());
    }

    [Fact]
    public void Capture_fences_and_callback_rate_changes_preserve_unconsumed_time()
    {
        PresentationTiming timing = new();
        ManualTimeProvider clock = new();
        timing.Accumulate(TimeSpan.FromMilliseconds(100));
        TimeSpan next = PresentationTiming.SimulationStep;
        int callbacks = 0;
        timing.RunUpdates(TimeSpan.FromMilliseconds(16), () => next, step =>
        {
            callbacks++;
            next = TimeSpan.FromSeconds(1d / 90);
            return false;
        }, clock);
        Assert.Equal(1, callbacks);
        timing.RunUpdates(TimeSpan.FromMilliseconds(16), () => next, step =>
        {
            Assert.Equal(next, step);
            callbacks++;
            return true;
        }, clock);
        Assert.Equal(8, callbacks);
        Assert.Equal(PresentationTiming.SimulationStep + next * 7, timing.SimulationTotal);
    }

    [Fact]
    public void Changing_the_cost_clock_preserves_steps_without_reusing_incomparable_timestamps()
    {
        PresentationTiming timing = new();
        ManualTimeProvider firstClock = new();
        timing.Accumulate(PresentationTiming.SimulationStep * 6);
        int updates = 0;
        timing.RunUpdates(PresentationTiming.SimulationStep, () => PresentationTiming.SimulationStep, step =>
        {
            updates++;
            firstClock.Advance(TimeSpan.FromMilliseconds(25));
            return true;
        }, firstClock);
        ManualTimeProvider nextClock = new();
        timing.RunUpdates(PresentationTiming.SimulationStep, () => PresentationTiming.SimulationStep, step =>
        {
            updates++;
            nextClock.Advance(TimeSpan.FromMilliseconds(3));
            return true;
        }, nextClock);
        Assert.Equal(6, updates);
        Assert.Equal(PresentationTiming.SimulationStep * 6, timing.SimulationTotal);
        Assert.False(timing.TryStep());
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return timestamp;
        }

        public void Advance(TimeSpan elapsed)
        {
            timestamp += elapsed.Ticks;
        }
    }

    [Theory]
    [InlineData(15, 90, 60)]
    [InlineData(30, 90, 60)]
    [InlineData(60, 90, 60)]
    [InlineData(90, 90, 90)]
    [InlineData(120, 90, 90)]
    [InlineData(144, 90, 90)]
    [InlineData(0, 90, 90)]
    [InlineData(120, 30, 60)]
    [InlineData(0, 0, 60)]
    public void Native_battle_host_steps_can_present_each_accelerated_callback(int frameLimit, int callbacks, int expectedRate)
    {
        TimeSpan step = PresentationTiming.UpdateStep(frameLimit, callbacks);
        Assert.Equal(TimeSpan.FromSeconds(1d / expectedRate), step);
        PresentationTiming timing = new();
        timing.Accumulate(step * expectedRate);
        int updates = 0;
        while (timing.TryStep(step))
        {
            updates++;
        }
        // Accumulation is bounded to a quarter second, even at higher battle update rates.
        Assert.Equal(Math.Min(expectedRate, PresentationTiming.MaximumElapsed.Ticks / step.Ticks), updates);
        Assert.Equal(step * updates, timing.SimulationTotal);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    [InlineData(1000)]
    public void Presentation_rate_keeps_sixty_simulation_steps_per_second(int frames)
    {
        PresentationTiming timing = new();
        int steps = 0;
        long previous = 0;
        for (int frame = 1; frame <= frames; frame++)
        {
            long current = TimeSpan.TicksPerSecond * frame / frames;
            timing.Accumulate(TimeSpan.FromTicks(current - previous));
            previous = current;
            while (timing.TryStep())
            {
                steps++;
            }
        }
        Assert.Equal(60, steps);
        Assert.Equal(PresentationTiming.SimulationStep * 60, timing.SimulationTotal);
    }

    [Fact]
    public void Switching_rates_and_unlimited_jitter_preserves_fractional_time()
    {
        PresentationTiming timing = new();
        TimeSpan elapsed = TimeSpan.Zero;
        int steps = 0;
        foreach (int milliseconds in new[] { 4, 7, 5, 17, 33, 67, 1, 3, 13, 50 })
        {
            TimeSpan interval = TimeSpan.FromMilliseconds(milliseconds);
            elapsed += interval;
            timing.Accumulate(interval);
            while (timing.TryStep())
            {
                steps++;
                Assert.Equal(PresentationTiming.SimulationStep * steps, timing.SimulationTotal);
            }
        }
        Assert.Equal(elapsed.Ticks / PresentationTiming.SimulationStep.Ticks, steps);
    }

    [Fact]
    public void Long_stall_has_bounded_catchup_and_no_later_debt()
    {
        PresentationTiming timing = new();
        timing.Accumulate(TimeSpan.FromSeconds(30));
        int steps = 0;
        while (timing.TryStep())
        {
            steps++;
        }
        Assert.Equal(15, steps);
        timing.Accumulate(TimeSpan.Zero);
        Assert.False(timing.TryStep());
        timing.Accumulate(PresentationTiming.SimulationStep);
        Assert.True(timing.TryStep());
        Assert.False(timing.TryStep());
    }

    [Fact]
    public void Pause_updates_do_not_bank_time_for_resume()
    {
        PresentationTiming timing = new();
        for (int frame = 0; frame < 120; frame++)
        {
            timing.Accumulate(PresentationTiming.SimulationStep);
            Assert.True(timing.TryStep());
            Assert.False(timing.TryStep());
        }
        // The game consumes these steps while paused, exactly as its existing fixed loop did.
        timing.Accumulate(PresentationTiming.SimulationStep);
        Assert.True(timing.TryStep());
        Assert.False(timing.TryStep());
        Assert.Equal(PresentationTiming.SimulationStep * 121, timing.SimulationTotal);
    }

    [Fact]
    public void Presentation_fence_retains_remaining_steps_with_bounded_debt()
    {
        PresentationTiming timing = new();
        timing.Accumulate(PresentationTiming.SimulationStep * 2);
        Assert.True(timing.TryStep());
        // A capture publishes this step before the next host update consumes the remainder.
        Assert.Equal(PresentationTiming.SimulationStep, timing.SimulationTotal);
        Assert.True(timing.TryStep());
        Assert.False(timing.TryStep());
        timing.Accumulate(TimeSpan.FromMilliseconds(200));
        timing.Accumulate(TimeSpan.FromMilliseconds(200));
        int steps = 0;
        while (timing.TryStep())
        {
            steps++;
        }
        Assert.Equal(15, steps);
    }

    [Theory]
    [InlineData(false, false, false, 60)]
    [InlineData(false, false, true, 30)]
    [InlineData(false, true, false, 120)]
    [InlineData(false, true, true, 120)]
    [InlineData(true, false, true, 0)]
    [InlineData(true, true, true, 0)]
    public void Scene_limits_keep_world_independent_with_menu_and_battle_precedence(bool menu, bool battle, bool worldMap, int expected)
    {
        UiPreferences preferences = new() { FieldFps = 60, WorldFps = 30, BattleFps = 120, MenuFps = 0 };
        Assert.Equal(expected, PresentationTiming.FrameLimit(preferences, menu, battle, worldMap));
    }
}
