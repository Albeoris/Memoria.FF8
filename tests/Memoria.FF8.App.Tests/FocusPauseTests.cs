using System.Reflection;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.App.Tests;

public sealed class FocusPauseTests
{
    [Fact]
    public void Closing_the_window_cannot_resume_disposed_outputs()
    {
        RecreateGame game = CreateGame();
        game.SetWindowActive(false);
        game.Dispose();
        game.SetWindowActive(true);
        game.SetWindowActive(false);
    }

    [Fact]
    public void Initial_window_activity_is_sampled_before_the_first_update()
    {
        using RecreateGame game = CreateGame();
        Assert.False(Get<bool>(game, "windowActivityInitialized"));
        bool paused = (bool)Invoke(game, "UpdateFocusPause")!;
        Assert.True(Get<bool>(game, "windowActivityInitialized"));
        Assert.Equal(!game.IsActive, paused);
        Assert.Equal(!game.IsActive, Get<bool>(game, "focusPaused"));
        Assert.Equal(0, Get<int>(game, "updateFrames"));
    }

    [Fact]
    public void Background_host_does_not_update_or_draw_and_resume_discards_elapsed_time()
    {
        using RecreateGame game = CreateGame();
        PresentationTiming timing = Get<PresentationTiming>(game, "presentationTiming");
        timing.Accumulate(PresentationTiming.SimulationStep);
        Assert.True(timing.TryStep());
        TimeSpan initial = timing.SimulationTotal;
        timing.Accumulate(TimeSpan.FromMilliseconds(200));

        game.SetWindowActive(false);
        for (int frame = 0; frame < 3; frame++)
        {
            Invoke(game, "Update", new GameTime(TimeSpan.FromHours(1), TimeSpan.FromSeconds(30)));
            Assert.False((bool)Invoke(game, "BeginDraw")!);
        }
        Assert.Equal(0, Get<int>(game, "updateFrames"));
        Assert.Equal(initial, timing.SimulationTotal);
        Assert.False(timing.TryStep());

        game.SetWindowActive(true);
        Invoke(game, "Update", new GameTime(TimeSpan.FromHours(1), TimeSpan.FromSeconds(30)));
        Assert.Equal(0, Get<int>(game, "updateFrames"));
        Assert.Equal(initial, timing.SimulationTotal);
        Assert.False(timing.TryStep());
        timing.Accumulate(PresentationTiming.SimulationStep);
        Assert.True(timing.TryStep());
        Assert.False(timing.TryStep());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Focus_pause_is_idempotent_and_does_not_clear_manual_pause(bool manuallyPaused)
    {
        using RecreateGame game = CreateGame();
        typeof(RecreateGame).GetField("userPaused", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, manuallyPaused);
        game.SetWindowActive(false);
        game.SetWindowActive(false);
        Assert.True(Get<bool>(game, "uiMovieSuspended"));
        game.SetWindowActive(true);
        game.SetWindowActive(true);
        Assert.Equal(manuallyPaused, Get<bool>(game, "userPaused"));
        Assert.Equal(manuallyPaused, Get<bool>(game, "uiMovieSuspended"));
    }

    [Fact]
    public void Explicit_control_host_can_continue_without_window_focus()
    {
        using RecreateGame game = CreateGame(controlDirectory: "controlled-host");
        PresentationTiming timing = Get<PresentationTiming>(game, "presentationTiming");
        timing.Accumulate(PresentationTiming.SimulationStep);
        game.SetWindowActive(false);
        Assert.False(Get<bool>(game, "focusPaused"));
        Assert.True(timing.TryStep());
    }

    private static RecreateGame CreateGame(string? controlDirectory = null)
    {
        LaunchOptions options = new() { MuteAudio = true, ControlDirectory = controlDirectory };
        return new RecreateGame(options, new GameSession(new GameState()));
    }

    private static T Get<T>(RecreateGame game, string name)
    {
        return (T)typeof(RecreateGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(game)!;
    }

    private static object? Invoke(RecreateGame game, string name, params object[] arguments)
    {
        return typeof(RecreateGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, arguments);
    }
}
