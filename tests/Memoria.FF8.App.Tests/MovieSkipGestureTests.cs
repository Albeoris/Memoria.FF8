namespace Memoria.FF8.App.Tests;

public sealed class MovieSkipGestureTests
{
    [Fact]
    public void Idle_movie_starts_without_a_hint_or_progress()
    {
        MovieSkipGesture gesture = new();

        Assert.False(gesture.Visible);
        Assert.Equal(0f, gesture.Progress);
        Assert.False(gesture.Update(TimeSpan.FromSeconds(10), cancelHeld: false));
        Assert.False(gesture.Visible);
        Assert.Equal(0f, gesture.Progress);
    }

    [Fact]
    public void Continuous_hold_skips_at_exactly_one_second_and_never_earlier()
    {
        MovieSkipGesture gesture = new();

        Assert.False(gesture.Update(TimeSpan.FromSeconds(0.5), cancelHeld: true));
        Assert.True(gesture.Visible);
        Assert.Equal(0.5f, gesture.Progress);
        Assert.False(gesture.Update(TimeSpan.FromTicks(TimeSpan.FromSeconds(0.5).Ticks - 1), cancelHeld: true));
        Assert.InRange(gesture.Progress, 0f, 1f);
        Assert.True(gesture.Update(TimeSpan.FromTicks(1), cancelHeld: true));
        Assert.Equal(1f, gesture.Progress);
    }

    [Fact]
    public void Sixty_hertz_updates_reach_the_threshold_on_the_60th_frame()
    {
        MovieSkipGesture gesture = new();
        TimeSpan frame = TimeSpan.FromTicks(166667);

        for (int index = 0; index < 59; index++)
        {
            Assert.False(gesture.Update(frame, cancelHeld: true));
            Assert.InRange(gesture.Progress, 0f, 1f);
        }

        Assert.True(gesture.Update(frame, cancelHeld: true));
        Assert.Equal(1f, gesture.Progress);
    }

    [Fact]
    public void Releasing_cancel_discards_hold_progress()
    {
        MovieSkipGesture gesture = new();

        Assert.False(gesture.Update(TimeSpan.FromSeconds(0.75), cancelHeld: true));
        Assert.False(gesture.Update(TimeSpan.Zero, cancelHeld: false));
        Assert.True(gesture.Visible);
        Assert.Equal(0f, gesture.Progress);
        Assert.False(gesture.Update(TimeSpan.FromSeconds(0.75), cancelHeld: true));
        Assert.Equal(0.75f, gesture.Progress);
        Assert.True(gesture.Update(TimeSpan.FromSeconds(0.25), cancelHeld: true));
    }

    [Fact]
    public void Hint_remains_visible_for_three_seconds_after_release()
    {
        MovieSkipGesture gesture = new();

        Assert.False(gesture.Update(TimeSpan.FromSeconds(0.25), cancelHeld: true));
        Assert.False(gesture.Update(TimeSpan.Zero, cancelHeld: false));
        Assert.False(gesture.Update(TimeSpan.FromTicks(TimeSpan.FromSeconds(3).Ticks - 1), cancelHeld: false));
        Assert.True(gesture.Visible);
        Assert.Equal(0f, gesture.Progress);
        Assert.False(gesture.Update(TimeSpan.FromTicks(1), cancelHeld: false));
        Assert.False(gesture.Visible);
    }

    [Fact]
    public void Repeated_taps_refresh_the_hint_timeout_without_accumulating_progress()
    {
        MovieSkipGesture gesture = new();

        for (int index = 0; index < 4; index++)
        {
            Assert.False(gesture.Update(TimeSpan.FromSeconds(0.25), cancelHeld: true));
            Assert.False(gesture.Update(TimeSpan.Zero, cancelHeld: false));
            Assert.False(gesture.Update(TimeSpan.FromSeconds(2), cancelHeld: false));
            Assert.True(gesture.Visible);
            Assert.Equal(0f, gesture.Progress);
        }

        Assert.False(gesture.Update(TimeSpan.FromSeconds(1), cancelHeld: false));
        Assert.False(gesture.Visible);
    }

    [Fact]
    public void Completing_a_hold_emits_only_one_skip_until_reset()
    {
        MovieSkipGesture gesture = new();

        Assert.True(gesture.Update(TimeSpan.FromSeconds(10), cancelHeld: true));
        Assert.Equal(1f, gesture.Progress);
        Assert.False(gesture.Update(TimeSpan.FromSeconds(3), cancelHeld: true));
        Assert.False(gesture.Update(TimeSpan.FromSeconds(3), cancelHeld: false));
        Assert.False(gesture.Update(TimeSpan.FromSeconds(3), cancelHeld: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reset_clears_stale_state_and_allows_a_new_movie_hold(bool completed)
    {
        MovieSkipGesture gesture = new();

        Assert.Equal(completed, gesture.Update(TimeSpan.FromSeconds(completed ? 1 : 0.75), cancelHeld: true));
        gesture.Reset();

        Assert.False(gesture.Visible);
        Assert.Equal(0f, gesture.Progress);
        Assert.False(gesture.Update(TimeSpan.FromSeconds(1), cancelHeld: false));
        Assert.False(gesture.Visible);
        Assert.False(gesture.Update(TimeSpan.FromSeconds(0.75), cancelHeld: true));
        Assert.Equal(0.75f, gesture.Progress);
        Assert.True(gesture.Update(TimeSpan.FromSeconds(0.25), cancelHeld: true));
    }
}
