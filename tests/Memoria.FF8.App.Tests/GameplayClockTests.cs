using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplayClockTests
{
    [Theory]
    [InlineData(50, 2, 0.5, 1)]
    [InlineData(100, 3, 1, 3)]
    [InlineData(150, 4, 1.5, 6)]
    [InlineData(300, 10, 3, 30)]
    public void Battle_speed_multiplies_fast_forward_even_for_a_field_scripted_interface(int percent, int fastForward, double normal, double accelerated)
    {
        GameplayClock clock = new();
        clock.Configure(fastForward, percent);
        TimeSpan elapsed = TimeSpan.FromMilliseconds(100);
        Assert.Equal(normal, clock.EffectiveSpeed(SceneKind.Field, scriptedInterface: true, battle: true));
        Assert.Equal(elapsed * normal, clock.Advance(elapsed, SceneKind.Title, battle: true));
        clock.ToggleSpeed();
        Assert.Equal(accelerated, clock.EffectiveSpeed(SceneKind.WorldMap, battle: true));
        Assert.Equal(elapsed * accelerated, clock.Advance(elapsed, SceneKind.Field, battle: true));
        Assert.Equal((double)fastForward, clock.EffectiveSpeed(SceneKind.Field));
        Assert.Equal(1d, clock.EffectiveSpeed(SceneKind.Movie));
    }

    [Fact]
    public void Changing_preferences_preserves_toggle_and_accumulated_time()
    {
        GameplayClock clock = new();
        clock.ToggleSpeed();
        clock.Advance(TimeSpan.FromSeconds(1), SceneKind.Field);
        clock.Configure(10, 50);
        Assert.Equal(10, clock.SpeedMultiplier);
        Assert.Equal(TimeSpan.FromSeconds(3), clock.Total);
        Assert.Equal(TimeSpan.FromSeconds(5), clock.Advance(TimeSpan.FromSeconds(1), SceneKind.Field, battle: true));
        clock.ToggleSpeed();
        Assert.Equal(1, clock.SpeedMultiplier);
        Assert.Equal(0.5, clock.EffectiveSpeed(SceneKind.Field, battle: true));
    }

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
