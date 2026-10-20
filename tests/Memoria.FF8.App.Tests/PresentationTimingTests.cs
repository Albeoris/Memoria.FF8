using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class PresentationTimingTests
{
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
