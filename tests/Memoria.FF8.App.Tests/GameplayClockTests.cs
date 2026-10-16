using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplayClockTests
{
    [Fact]
    public void Field_and_world_share_scaled_elapsed_and_render_time_while_movies_keep_real_time()
    {
        GameplayClock clock = new();
        TimeSpan elapsed = TimeSpan.FromMilliseconds(100);
        Assert.Equal(elapsed, clock.Advance(elapsed, SceneKind.Field));
        clock.ToggleSpeed();
        Assert.Equal(TimeSpan.FromMilliseconds(300), clock.Advance(elapsed, SceneKind.Field));
        Assert.Equal(TimeSpan.FromMilliseconds(300), clock.Advance(elapsed, SceneKind.WorldMap));
        Assert.Equal(elapsed, clock.Advance(elapsed, SceneKind.Movie));
        Assert.Equal(elapsed, clock.Advance(elapsed, SceneKind.Title));
        Assert.Equal(3, clock.SpeedMultiplier);
        Assert.Equal(TimeSpan.FromMilliseconds(300), clock.Advance(elapsed, SceneKind.Field));
        clock.ToggleSpeed();
        Assert.Equal(elapsed, clock.Advance(elapsed, SceneKind.WorldMap));
        Assert.Equal(TimeSpan.FromMilliseconds(1300), clock.Total);
    }

    [Fact]
    public void Scripted_interface_keeps_its_callback_and_repeat_delays_when_speed_is_toggled()
    {
        GameplayClock clock = new();
        TimeSpan elapsed = TimeSpan.FromMilliseconds(100);
        Assert.Equal(elapsed, clock.Advance(elapsed, SceneKind.Field, scriptedInterface: true));
        clock.ToggleSpeed();
        Assert.Equal(elapsed, clock.Advance(elapsed, SceneKind.Field, scriptedInterface: true));
        Assert.Equal(3, clock.SpeedMultiplier);
        Assert.Equal(TimeSpan.FromMilliseconds(300), clock.Advance(elapsed, SceneKind.Field));
        Assert.Equal(TimeSpan.FromMilliseconds(300), clock.Advance(elapsed, SceneKind.WorldMap));
        Assert.Equal(TimeSpan.FromMilliseconds(800), clock.Total);
    }

    [Fact]
    public void Toggling_preserves_fractional_simulation_time_without_a_clock_jump()
    {
        GameplayClock clock = new();
        TimeSpan elapsed = TimeSpan.FromTicks(166667);
        Assert.Equal(elapsed, clock.Advance(elapsed, SceneKind.Field));
        clock.ToggleSpeed();
        Assert.Equal(elapsed, clock.Total);
        Assert.Equal(TimeSpan.FromTicks(500001), clock.Advance(elapsed, SceneKind.Field));
        clock.ToggleSpeed();
        Assert.Equal(TimeSpan.FromTicks(666668), clock.Total);
        Assert.Equal(elapsed, clock.Advance(elapsed, SceneKind.Field));
        Assert.Equal(TimeSpan.FromTicks(833335), clock.Total);
    }
}
