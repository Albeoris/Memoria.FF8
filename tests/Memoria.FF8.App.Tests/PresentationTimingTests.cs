using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class PresentationTimingTests
{
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

    [Fact]
    public void Menu_limit_overrides_battle_and_field_limits()
    {
        UiPreferences preferences = new() { FieldFps = 30, BattleFps = 60, MenuFps = 0 };
        Assert.Equal(30, PresentationTiming.FrameLimit(preferences, menu: false, battle: false));
        Assert.Equal(60, PresentationTiming.FrameLimit(preferences, menu: false, battle: true));
        Assert.Equal(0, PresentationTiming.FrameLimit(preferences, menu: true, battle: true));
    }
}
